using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Integration checks for source rollback, instance isolation, global budget and removed collision.</summary>
public static class VoxelMapPilotChecks
{
    [MenuItem("修仙/体素/验证正式地图试点（Play）")]
    public static void 验证()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Sect_Wilderness Play first.");
        var pilot=UnityEngine.Object.FindObjectOfType<VoxelMapPilot>();
        var manifest=Resources.Load<VoxelMapPilotSettings>("Voxel/MapPilots/Sect_Wilderness");
        if(!pilot || !manifest || pilot.替换数量!=manifest.替换项.Length)throw new InvalidOperationException("Whole-map manifest was not completely installed.");
        bool wasActive=pilot.正在替换;pilot.切换试点(true);pilot.还原岩石();
        var bodies=pilot.体素对象;var a=bodies[0];var b=bodies[1];
        var terrain=Terrain.activeTerrain;var originalTerrainData=terrain?terrain.terrainData:null;
        double peak=0;int total=0;
        try
        {
            var data=a.数据;
            int sample=Array.FindIndex(data.实体,v=>v!=0);
            int x=sample%data.尺寸.x,y=(sample/data.尺寸.x)%data.尺寸.y,z=sample/(data.尺寸.x*data.尺寸.y);
            var local=data.原点+new Vector3(x+.5f,y+.5f,z+.5f)*data.格子边长;
            var center=a.transform.TransformPoint(local);
            a.破坏球(center,.25f);
            if(a.剩余体素>=data.实体数量 || b.剩余体素!=data.实体数量 || b.等待块!=0)throw new Exception("Shared bake or other instance mutated.");
            while(a.等待块+b.等待块>0)
            {int updated=pilot.更新局部();if(updated>32 || updated==0)throw new Exception("Global chunk budget failed.");total+=updated;peak=Math.Max(peak,pilot.最近更新毫秒);}
            a.破坏球(center,.25f);
            if(a.等待块!=0)throw new Exception("Identical cut queued again.");
            pilot.破坏球(new Vector3(10000,10000,10000),.5f);
            if(a.等待块+b.等待块!=0)throw new Exception("Out-of-range cut queued.");
            // Erase one rock completely: the old capsule must not remain as an invisible obstacle.
            var middle=a.transform.TransformPoint(data.原点+(Vector3)data.尺寸*data.格子边长*.5f);
            a.破坏球(middle,20);
            while(a.等待块+b.等待块>0)
            {int updated=pilot.更新局部();if(updated>32 || updated==0)throw new Exception("Global erase budget failed.");total+=updated;peak=Math.Max(peak,pilot.最近更新毫秒);}
            Physics.SyncTransforms();
            if(a.剩余体素!=0 || a.GetComponentsInChildren<MeshCollider>().Any(c=>c.enabled || c.sharedMesh))throw new Exception("Erased rock retained collision.");
            if(a.transform.parent.GetComponent<Collider>().enabled)throw new Exception("Source collision leaked into voxel mode.");
            if(b.累计重建块!=0 || b.剩余体素!=data.实体数量)throw new Exception("Untouched rock rebuilt.");
            if(data.实体.Count(v=>v!=0)!=data.实体数量)throw new Exception("Immutable bake changed.");
            pilot.切换试点(false);
            foreach(var body in bodies)
                if(body.gameObject.activeSelf || !body.transform.parent.GetComponent<Collider>().enabled || !body.transform.parent.GetComponent<MeshRenderer>().enabled)
                    throw new Exception("Source rollback failed.");
            if(terrain && terrain.terrainData!=originalTerrainData)throw new Exception("Terrain asset replaced by rock pilot.");
            Debug.Log($"VOXEL_MAP_CHECK_PASS registered={pilot.替换数量} sharedBake=True instanceIsolation=True noGhostCollision=True rollback=True totalChunks={total} peakBatch={peak:F2}ms; terrain unchanged.");
        }
        finally {pilot.还原岩石();pilot.切换试点(wasActive);}
    }
}
