using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Independent reference remesh catches missing chunk-border invalidation.</summary>
public static class VoxelPrototypeChecks
{
    [MenuItem("修仙/实验/验证体素斜面精度")]
    public static void 验证斜面()
    {
        var data=ScriptableObject.CreateInstance<VoxelVolumeAsset>();
        var meshes=new List<Mesh>();
        try
        {
            data.尺寸=new Vector3Int(24,24,24);data.格子边长=.12f;data.分块边长=8;
            int length=24*24*24;data.实体=new byte[length];data.距离=new float[length];data.颜色=new Color32[length];
            var normal=new Vector3(.37f,1,.23f).normalized;float offset=1.7f;
            for(int z=0;z<24;z++)for(int y=0;y<24;y++)for(int x=0;x<24;x++)
            {
                int i=data.索引(x,y,z);var p=new Vector3(x+.5f,y+.5f,z+.5f)*data.格子边长;
                data.距离[i]=offset-Vector3.Dot(normal,p);data.实体[i]=(byte)(data.距离[i]>0?1:0);data.颜色[i]=Color.gray;
            }
            int checkedVertices=0;float maxError=0;
            for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++)
            {
                var mesh=VoxelSmoothMesher.生成(data,data.实体,new Vector3Int(x,y,z));meshes.Add(mesh);
                var vertices=mesh.vertices;var normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=vertices[i];float margin=data.格子边长*3;
                    if(p.x<margin || p.y<margin || p.z<margin || p.x>2.88f-margin || p.y>2.88f-margin || p.z>2.88f-margin)continue;
                    float error=Mathf.Abs(Vector3.Dot(normal,p)-offset);maxError=Mathf.Max(maxError,error);
                    if(error>1e-5f || Vector3.Dot(normals[i],normal)<.99999f)throw new Exception("Sloped plane acquired grid waves.");
                    checkedVertices++;
                }
            }
            if(checkedVertices<100)throw new Exception("Insufficient plane coverage.");
            Debug.Log($"VOXEL_PLANE_PASS vertices={checkedVertices} maxError={maxError:G6}m; position and normals remain planar across chunks.");
        }
        finally {foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(data);}
    }

    [MenuItem("修仙/实验/验证体素局部更新（Play）")]
    public static void 验证()
    {
        if(!Application.isPlaying) throw new InvalidOperationException("Enter the voxel prototype Play scene first.");
        var sample=UnityEngine.Object.FindObjectOfType<VoxelDestructible>();
        if(!sample || !sample.数据) throw new InvalidOperationException("No voxel sample found.");
        var data=sample.数据;
        var go=new GameObject("VoxelValidation");go.transform.position=new Vector3(1000,0,0);
        var referenceMeshes=new List<Mesh>();
        try
        {
            var body=go.AddComponent<VoxelDestructible>();body.数据=data;body.初始化();
            if(body.累计重建块!=0) throw new Exception("Initialization rebuilt baked geometry.");
            var before=new Dictionary<Vector3Int,Mesh>();foreach(var key in data.分块坐标)before.Add(key,body.取块网格(key));
            var occupied=new List<Vector3Int>();
            for(int z=0;z<data.尺寸.z;z++)for(int y=0;y<data.尺寸.y;y++)for(int x=0;x<data.尺寸.x;x++)
                if(data.实体[data.索引(x,y,z)]!=0)occupied.Add(new Vector3Int(x,y,z));
            int cuts=data.实体.Length>30000?5:1;
            for(int i=0;i<cuts;i++)
            {
                var sampleCell=occupied[occupied.Count*(i+1)/(cuts+1)];
                var local=data.原点+((Vector3)sampleCell+Vector3.one*.5f)*data.格子边长;
                body.破坏球(go.transform.TransformPoint(local),data.格子边长*(cuts==1?1.5f:3.2f));
            }
            int queued=body.等待块;
            while(body.等待块>0) if(body.更新局部()>body.每帧最多更新块)throw new Exception("Chunk budget exceeded.");
            var cells=new byte[data.实体.Length];int solid=0;
            for(int z=0;z<data.尺寸.z;z++)for(int y=0;y<data.尺寸.y;y++)for(int x=0;x<data.尺寸.x;x++)
                if(body.取实体(x,y,z)){cells[data.索引(x,y,z)]=1;solid++;}
            if(solid!=body.剩余体素 || solid>=data.实体数量) throw new Exception("No valid destruction or incorrect occupancy count.");
            int changed=0;
            var distances=body.复制距离();
            foreach(var key in data.分块坐标)
            {
                var actual=body.取块网格(key);
                if(actual!=before[key]) changed++;
                var expected=VoxelChunkMesher.生成(data,cells,key,distances);referenceMeshes.Add(expected);
                var av=actual.vertices;var ev=expected.vertices;var an=actual.normals;var en=expected.normals;
                var au=actual.uv;var eu=expected.uv;var ac=actual.colors32;var ec=expected.colors32;
                if(av.Length!=ev.Length)throw new Exception("Boundary vertex count mismatch: "+key);
                for(int i=0;i<av.Length;i++)
                    if((av[i]-ev[i]).sqrMagnitude>1e-10f || (an[i]-en[i]).sqrMagnitude>1e-8f)
                        throw new Exception("Boundary position/normal mismatch: "+key);
                if(au.Length!=eu.Length || ac.Length!=ec.Length)throw new Exception("Surface data count mismatch: "+key);
                for(int i=0;i<au.Length;i++)if((au[i]-eu[i]).sqrMagnitude>1e-10f)throw new Exception("UV mismatch: "+key);
                for(int i=0;i<ac.Length;i++)if(!ac[i].Equals(ec[i]))throw new Exception("Surface colour mismatch: "+key);
                var at=actual.triangles;var et=expected.triangles;
                if(at.Length!=et.Length)throw new Exception("Topology count mismatch: "+key);
                for(int i=0;i<at.Length;i++)if(at[i]!=et[i])throw new Exception("Topology mismatch: "+key);
            }
            if(changed!=queued || changed>=data.分块坐标.Length)throw new Exception("Destruction was not local.");
            int original=0;foreach(byte b in data.实体)if(b!=0)original++;
            if(original!=data.实体数量)throw new Exception("Source bake modified.");
            body.初始化();
            if(body.剩余体素!=data.实体数量 || body.累计重建块!=0)throw new Exception("Reset failed.");
            Debug.Log($"VOXEL_CHECK_PASS smooth={data.平滑表面} local={changed}/{data.分块坐标.Length}; vertex/normal/topology reference comparison and immutable bake/reset passed.");
        }
        finally
        {
            go.SetActive(false);
            UnityEngine.Object.Destroy(go);
            foreach(var mesh in referenceMeshes)UnityEngine.Object.Destroy(mesh);
        }
    }
}
