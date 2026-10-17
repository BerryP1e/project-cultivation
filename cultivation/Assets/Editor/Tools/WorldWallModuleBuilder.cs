using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Cultivation.WorldAuthoring;

public static class WorldWallModuleBuilder
{
    const int Revision=1;
    public static bool IsModule(string path){string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();if(path.Contains("_AsianArchitecture")&&name.Contains("door"))return false;return name.Contains("wall_")||name.Contains("fence_");}
    sealed class Part{public Vector3[] positions;public Vector2[] uv;public int[][] indices;public Material[] materials;public string name;}
    static Part Read(MeshFilter filter)
    {
        var mesh=filter.sharedMesh;using(var all=MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var data=all[0];var native=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);data.GetVertices(native);var positions=native.ToArray();native.Dispose();
            for(int i=0;i<positions.Length;i++)positions[i]=filter.transform.localToWorldMatrix.MultiplyPoint3x4(positions[i]);
            var uv=new Vector2[data.vertexCount];if(mesh.HasVertexAttribute(VertexAttribute.TexCoord0)){var buffer=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp);data.GetUVs(0,buffer);uv=buffer.ToArray();buffer.Dispose();}
            var indices=new int[data.subMeshCount][];for(int submesh=0;submesh<indices.Length;submesh++){var buffer=new NativeArray<int>(data.GetSubMesh(submesh).indexCount,Allocator.Temp);data.GetIndices(buffer,submesh);indices[submesh]=buffer.ToArray();buffer.Dispose();}
            return new Part {positions=positions,uv=uv,indices=indices,materials=filter.GetComponent<Renderer>().sharedMaterials,name=filter.name};
        }
    }
    static List<Vector3[]> Slice(IEnumerable<Part> parts,float y)
    {
        var lines=new List<Vector3[]>();bool wall=parts.Any(p=>p.materials.Any(m=>m&&m.name.ToLowerInvariant().Contains("wall")));
        foreach(var part in parts)for(int sub=0;sub<part.indices.Length;sub++)
        {
            if(wall&&(sub>=part.materials.Length||!part.materials[sub]||!part.materials[sub].name.ToLowerInvariant().Contains("wall")))continue;
            var indices=part.indices[sub];for(int i=0;i<indices.Length;i+=3)
            {
                var crossings=new List<Vector3>(2);
                for(int edge=0;edge<3;edge++){var a=part.positions[indices[i+edge]];var b=part.positions[indices[i+(edge+1)%3]];if((a.y<y)!=(b.y<y))crossings.Add(Vector3.Lerp(a,b,(y-a.y)/(b.y-a.y)));}
                if(crossings.Count==2&&Vector3.Distance(crossings[0],crossings[1])>.001f)lines.Add(crossings.ToArray());
            }
        }
        return lines;
    }
    static float CentreLine(List<Vector3[]> lines,bool alongX,float fallback)
    {
        var groups=new Dictionary<int,float>();foreach(var line in lines){float a=alongX?line[0].x:line[0].z,b=alongX?line[1].x:line[1].z;if(Mathf.Abs(a-b)>.003f)continue;int key=Mathf.RoundToInt((a+b)*500);float length=Vector3.Distance(line[0],line[1]);groups[key]=(groups.TryGetValue(key,out float value)?value:0)+length;}
        var best=groups.OrderByDescending(x=>x.Value).Take(2).ToArray();return best.Length==0?fallback:best.Average(x=>x.Key/1000f);
    }
    public static WorldModuleProfile Prepare(string path,GameObject source)
    {
        if(!IsModule(path))return null;string directory=WorldMapEditorBuilder.Root+"/Modules";Directory.CreateDirectory(directory);if(!AssetDatabase.IsValidFolder(directory))AssetDatabase.ImportAsset(directory,ImportAssetOptions.ForceSynchronousImport);
        string id=AssetDatabase.AssetPathToGUID(path),assetPath=directory+"/"+id+".asset",hash=AssetDatabase.GetAssetDependencyHash(path).ToString();
        var profile=AssetDatabase.LoadAssetAtPath<WorldModuleProfile>(assetPath);if(profile&&profile.revision==Revision&&profile.sourceHash==hash&&profile.prefab)return profile;
        if(!profile){profile=ScriptableObject.CreateInstance<WorldModuleProfile>();AssetDatabase.CreateAsset(profile,assetPath);}
        var parts=source.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh&&f.GetComponent<Renderer>()).Select(Read).ToArray();if(parts.Length==0)return null;
        var points=parts.SelectMany(p=>p.positions).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
        var lines=Slice(parts,bounds.min.y+bounds.size.y*.4f);var cut=lines.SelectMany(x=>x).ToArray();if(cut.Length==0)cut=points;
        float minX=cut.Min(x=>x.x),maxX=cut.Max(x=>x.x),minZ=cut.Min(x=>x.z),maxZ=cut.Max(x=>x.z);
        string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();bool corner=name.Contains("corner"),city=name.Contains("city"),gate=name.Contains("gate"),fence=name.Contains("fence");
        float pitch=city?12:gate?6:fence?Mathf.Max(2,Mathf.Round(bounds.size.x/2)*2):4;
        float cx=corner?CentreLine(lines,true,maxX-.10f):(minX+maxX)*.5f,cz=corner?CentreLine(lines,false,maxZ-.10f):(minZ+maxZ)*.5f;
        float sx=corner?2/Mathf.Max(.1f,cx-minX):pitch/Mathf.Max(.1f,maxX-minX),sz=corner?2/Mathf.Max(.1f,cz-minZ):1;
        profile.points=corner?new[]{new Vector3(-2,0,0),new Vector3(0,0,-2)}:new[]{new Vector3(-pitch*.5f,0,0),new Vector3(pitch*.5f,0,0)};
        profile.directions=corner?new[]{Vector3.left,Vector3.back}:new[]{Vector3.left,Vector3.right};profile.family=path.Contains("_AsianArchitecture")?"asian-wall":city?"city":fence?(name.Contains("bamboo")?"bamboo":"temple-fence"):"garden";
        profile.groundHalfWidth=Mathf.Clamp(corner?.18f:(maxZ-minZ)*.5f,.08f,city?2.5f:.55f);
        var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject(source.name+" Grid module");SceneManager.MoveGameObjectToScene(root,preview);
        try
        {
            for(int partIndex=0;partIndex<parts.Length;partIndex++)
            {
                var part=parts[partIndex];var vertices=part.positions.Select(p=>{var value=new Vector3((p.x-cx)*sx,p.y-bounds.min.y,(p.z-cz)*sz);value.x=corner?Mathf.Max(-2,value.x):Mathf.Clamp(value.x,-pitch*.5f,pitch*.5f);if(corner)value.z=Mathf.Max(-2,value.z);return value;}).ToArray();
                string meshPath=directory+"/"+id+"_"+partIndex+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,meshPath);}else mesh.Clear();
                mesh.name=Path.GetFileNameWithoutExtension(meshPath);mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vertices;mesh.uv=part.uv;mesh.subMeshCount=part.indices.Length;
                for(int sub=0;sub<part.indices.Length;sub++){var clean=new List<int>();var indices=part.indices[sub];for(int i=0;i<indices.Length;i+=3)if(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).sqrMagnitude>1e-12f){clean.Add(indices[i]);clean.Add(indices[i+1]);clean.Add(indices[i+2]);}mesh.SetTriangles(clean,sub);}
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
                var go=new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(root.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=part.materials;go.GetComponent<MeshCollider>().sharedMesh=mesh;
            }
            profile.prefab=PrefabUtility.SaveAsPrefabAsset(root,directory+"/"+id+".prefab");profile.revision=Revision;profile.sourceHash=hash;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);return profile;
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
    }
}
