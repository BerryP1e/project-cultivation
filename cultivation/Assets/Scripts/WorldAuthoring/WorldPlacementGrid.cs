using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultivation.WorldAuthoring
{
    public sealed class WorldPlacementGrid : IDisposable
    {
        GameObject root;
        Mesh mesh;
        Material material;
        Vector3 previous=new Vector3(99999,0,0);
        float previousStep;
        public void Show(WorldMapRenderer map,Vector3 centre,float step)
        {
            if(!root){root=new GameObject("Placement grid",typeof(MeshFilter),typeof(MeshRenderer));mesh=new Mesh {name="Placement grid lines"};root.GetComponent<MeshFilter>().sharedMesh=mesh;material=new Material(Shader.Find("Sprites/Default"));root.GetComponent<MeshRenderer>().sharedMaterial=material;root.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;root.layer=2;}
            root.SetActive(true);centre.x=Mathf.Round(centre.x/step)*step;centre.z=Mathf.Round(centre.z/step)*step;
            if(Vector3.Distance(previous,centre)<.01f&&Mathf.Abs(step-previousStep)<.001f)return;previous=centre;previousStep=step;
            int reach=Mathf.Clamp(Mathf.CeilToInt(10/step),4,20);var vertices=new List<Vector3>();var colours=new List<Color>();var indices=new List<int>();
            for(int axis=0;axis<2;axis++)for(int line=-reach;line<=reach;line++)for(int segment=-reach;segment<reach;segment++)
            {
                var a=centre+new Vector3((axis==0?line:segment)*step,0,(axis==0?segment:line)*step);var b=a+(axis==0?Vector3.forward:Vector3.right)*step;
                if(!map.Data.HasGround(a.x,a.z)||!map.Data.HasGround(b.x,b.z)||WorldPlacementRules.OnPondBank(map.Data,a.x,a.z,.05f)||WorldPlacementRules.OnPondBank(map.Data,b.x,b.z,.05f))continue;
                a.y=map.GroundHeight(a.x,a.z)+.035f;b.y=map.GroundHeight(b.x,b.z)+.035f;
                float fade=1-Mathf.Max(Mathf.Abs(line),Mathf.Abs(segment))/(float)(reach+1);var color=line==0?new Color(.45f,.88f,.73f,fade*.50f):new Color(.84f,.88f,.83f,fade*.24f);
                indices.Add(vertices.Count);vertices.Add(a);colours.Add(color);indices.Add(vertices.Count);vertices.Add(b);colours.Add(color);
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colours);mesh.SetIndices(indices,MeshTopology.Lines,0);mesh.RecalculateBounds();
        }
        public void Hide(){if(root)root.SetActive(false);previous=new Vector3(99999,0,0);}
        public void Dispose(){if(root)UnityEngine.Object.Destroy(root);if(mesh)UnityEngine.Object.Destroy(mesh);if(material)UnityEngine.Object.Destroy(material);}
    }
}
