using System.Collections.Generic;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

#if ASP_COLLECTION_VERSION
namespace ASP.Scripts.Editor
{
    public class SmoothNormalTool : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            ModelImporter model = assetImporter as ModelImporter;
            if (model.userData == "ASPSmoothModel_SmoothRef")
            {
                //model.importNormals = ModelImporterNormals.Calculate;
                //model.normalCalculationMode = ModelImporterNormalCalculationMode.AngleWeighted;
               // model.normalSmoothingAngle = 180;
            }
        }

        void OnPostprocessModel(GameObject target)
        { 
            var model = assetImporter as ModelImporter;
            
            if (model.userData != "ASPSmoothModel_BakeTarget" || model.name.Contains("@smoothRef"))
            {
                return;
            }

            var src = model.assetPath;
            var smoothedModelDst = Path.GetDirectoryName(src) + "/" + target.name + "@smoothRef" + Path.GetExtension(src);
            var smoothedModel = AssetDatabase.LoadAssetAtPath<GameObject>(smoothedModelDst);
            if (smoothedModel == null)
            {
                Debug.LogWarning("no smooth normal reference file for " + model.assetPath);
                return;
            }

            
            var targetMesh = new Dictionary<string, Mesh>();
            var smoothedMesh = new Dictionary<string, Mesh>();
            targetMesh = GetMesh(target);
            smoothedMesh = GetMesh(smoothedModel);
            
            if (smoothedModel != null)
            {
                AssetImporter smoothRefAssetImporter = AssetImporter.GetAtPath(smoothedModelDst);
                if (smoothRefAssetImporter is UnityEditor.ModelImporter smoothRefModelImpoter)
                {
                    smoothRefModelImpoter.importBlendShapes =  model.importBlendShapes;
                    smoothRefModelImpoter.importBlendShapeNormals = model.importBlendShapeNormals;
                    smoothRefModelImpoter.importNormals = model.importNormals;
                    smoothRefModelImpoter.normalCalculationMode = model.normalCalculationMode;
                    smoothRefModelImpoter.normalSmoothingAngle = model.normalSmoothingAngle;
                    smoothRefModelImpoter.normalSmoothingSource = model.normalSmoothingSource;
                    smoothRefModelImpoter.weldVertices = model.weldVertices;
                    smoothRefModelImpoter.SaveAndReimport();
                }
            }

            foreach (var item in targetMesh)
            {
                var m = item.Value;
                m.SetUVs(4, ComputeSmoothedNormalByJob(smoothedMesh[item.Key], m));
            }
            
            if (smoothedModel != null)
            {
              //  AssetDatabase.DeleteAsset(smoothedModelDst);
            }
        }

        private static Dictionary<string, Mesh> GetMesh(GameObject go)
        {
            var dic = new Dictionary<string, Mesh>();
            foreach (var item in go.GetComponentsInChildren<MeshFilter>())
            {
                dic.Add(item.sharedMesh.name, item.sharedMesh);
            }

            foreach (var item in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                dic.Add(item.sharedMesh.name, item.sharedMesh);
            return dic;
        }

        private Vector3[] ComputeSmoothedNormalByJob(Mesh smoothedMesh, Mesh originalMesh)
        {
            var smoothedVertexCount = smoothedMesh.vertexCount;
            var originalVertexCount = originalMesh.vertexCount;
            //Debug.Log("svc = "+smoothedVertexCount);
            //Debug.Log("ovc = "+originalVertexCount);
            var smoothedMeshVertices = new List<Vector3>();
            var smoothedMeshNormals = new List<Vector3>();
            smoothedMesh.GetVertices(smoothedMeshVertices);
            smoothedMesh.RecalculateNormals();
            smoothedMesh.GetNormals(smoothedMeshNormals);
            var smoothedMeshVerticesArray = smoothedMeshVertices.ToArray();
            var smoothedMeshNormalsArray = smoothedMeshNormals.ToArray();
            // CollectNormalJob Data
            var vertexNativeArray = new NativeArray<Vector3>(smoothedMeshVerticesArray, Allocator.Persistent);
            var normalsNativeArray = new NativeArray<Vector3>(smoothedMeshNormalsArray, Allocator.Persistent);
            var smoothedNormalsNativeArray = new NativeArray<Vector3>(smoothedVertexCount, Allocator.Persistent);
            var resultParallel =
                new NativeParallelMultiHashMap<Vector3, Vector3>(originalVertexCount * 30,Allocator.Persistent);
            
            // NormalBakeJob Data
            var normalsO = new NativeArray<Vector3>(originalMesh.normals, Allocator.Persistent);
            var vertex0 = new NativeArray<Vector3>(originalMesh.vertices, Allocator.Persistent);
            var tangents = new NativeArray<Vector4>(originalMesh.tangents, Allocator.Persistent);
            var bakedNormals = new NativeArray<Vector3>(originalVertexCount, Allocator.Persistent);
            if (smoothedMesh.vertices.Length != originalMesh.vertices.Length)
            {
                Debug.LogWarning("vertices count are not equal for smooth ref and origin model, baking result will not correct!");
            }
            
            var collectNormalJob = new CollectNormalJob
            {
                Vertices = vertexNativeArray,
                Normals = normalsNativeArray,
                Result = resultParallel.AsParallelWriter(),
            };
            var jobHandle = collectNormalJob.Schedule(smoothedVertexCount, 1);
            var bakeNormalJob = new BakeNormalJob
            {
                Vertices = vertexNativeArray,
                Normals = normalsO,
                Tangents = tangents,
                BakedNormals = bakedNormals,
                Result = resultParallel,
            };
            jobHandle = bakeNormalJob.Schedule(originalVertexCount, 1, jobHandle);
            jobHandle.Complete();
            var bakedNormalManaged = bakedNormals.ToArray();

            normalsNativeArray.Dispose();
            vertexNativeArray.Dispose();
            smoothedNormalsNativeArray.Dispose();
            resultParallel.Dispose();
            normalsO.Dispose();
            vertex0.Dispose();
            tangents.Dispose();
            bakedNormals.Dispose();
            return bakedNormalManaged;
        }

        [BurstCompile]
        public struct CollectNormalJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> Normals;
            [ReadOnly] public NativeArray<Vector3> Vertices;
            
            [NativeDisableContainerSafetyRestriction]
            public NativeParallelMultiHashMap<Vector3, Vector3>.ParallelWriter Result;

            void IJobParallelFor.Execute(int index)
            {
                Result.Add(Vertices[index], Normals[index]);
            }
        }

        [BurstCompile]
        public struct BakeNormalJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> Vertices;
            [ReadOnly] public NativeArray<Vector3> Normals;
            [ReadOnly] public NativeArray<Vector4> Tangents;
            
            [NativeDisableContainerSafetyRestriction] [ReadOnly]
            public NativeParallelMultiHashMap<Vector3, Vector3> Result;

            public NativeArray<Vector3> BakedNormals;

            void IJobParallelFor.Execute(int index)
            {
                if (index >= Vertices.Length)
                {
                    BakedNormals[index] = Normals[index];
                    return;
                }

                Vector3 smoothedNormals = Vector3.zero;
                if (Result.TryGetFirstValue(Vertices[index], out Vector3 value, out var iterator))
                {
                    var count = 0;
                    do
                    {
                        count++;
                        smoothedNormals += value;
                    } while (Result.TryGetNextValue(out value, ref iterator));
                }

                smoothedNormals = smoothedNormals.normalized;

                var binormal = (Vector3.Cross(Normals[index], Tangents[index]) * Tangents[index].w).normalized;

                var tbn = new Matrix4x4(
                    Tangents[index],
                    binormal,
                    Normals[index],
                    Vector4.zero);
                tbn = tbn.transpose;
                var bakedNormal = tbn.MultiplyVector(smoothedNormals).normalized;
                BakedNormals[index] = bakedNormal;
            }
        }
    }
}
#endif
