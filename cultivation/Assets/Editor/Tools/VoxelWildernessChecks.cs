using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Formal scene regressions, restoring the world even if a check fails.</summary>
public static class VoxelWildernessChecks
{
    [MenuItem("修仙/体素/验证整张野外地面与树木（Play）")]
    public static void 验证()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter formal wilderness Play first.");
        var pilot=UnityEngine.Object.FindObjectOfType<VoxelMapPilot>();
        var manifest=Resources.Load<VoxelMapPilotSettings>("Voxel/MapPilots/Sect_Wilderness");
        Require(pilot && pilot.地面 && manifest,"Missing environment installation");
        Require(pilot.替换数量==manifest.替换项.Length,"Incomplete map manifest");
        bool wasActive=pilot.正在替换;pilot.切换试点(true);pilot.还原岩石();
        var terrain=Terrain.activeTerrain;var original=pilot.地面.原地形;
        var baseline=original.GetHoles(0,0,original.holesResolution,original.holesResolution);
        double peak=0;int units=0;
        try
        {
            Require(pilot.已激活数量==2,"Pristine world eagerly instantiated");
            // Both axes meet at a patch corner. Four local patches must agree on the common sampling lattice.
            var point=terrain.transform.position+new Vector3(103.125f,0,175);
            point.y=terrain.SampleHeight(point)+terrain.transform.position.y;
            pilot.破坏球(point,.85f);Drain(pilot,ref peak,ref units);
            Require(pilot.地面.活动地块数>=4,"Corner cut did not allocate all neighbouring patches");
            Require(terrain.terrainData!=original,"Terrain holes must use a private copy");
            Require(Floor(point).point.y<point.y-.5f,"Pit did not replace original collision");
            // Repeated deeper cuts stop above the protected floor rather than opening a void.
            for(int i=1;i<=5;i++){pilot.破坏球(point-Vector3.up*i*.65f,.85f);Drain(pilot,ref peak,ref units);}
            float depth=point.y-Floor(point).point.y;
            Require(depth>2.8f && depth<4.05f,"Invalid protected ground depth: "+depth);
            // A later patch allocation inherits cuts recorded in its halo.
            var adjacent=point+Vector3.right*3.4f;adjacent.y=terrain.SampleHeight(adjacent)+terrain.transform.position.y;
            pilot.破坏球(adjacent,.7f);Drain(pilot,ref peak,ref units);
            CheckSeams(pilot);
            int patches=pilot.地面.活动地块数;
            pilot.破坏球(Vector3.one*10000,.5f);
            Require(pilot.地面.活动地块数==patches && !pilot.等待更新,"Outside cut allocated work");
            var tree=manifest.替换项.First(e=>e.树木 && e.源网格.name.Contains("Green_001"));
            var source=Locate(tree);var data=tree.数据;
            float slice=tree.预期包围盒.min.y+tree.预期包围盒.size.y*.18f;
            var points=new System.Collections.Generic.List<Vector3>();
            for(int z=0;z<data.尺寸.z;z++)for(int y=0;y<data.尺寸.y;y++)for(int x=0;x<data.尺寸.x;x++)
            {
                if(data.实体[data.索引(x,y,z)]==0)continue;
                var p=source.TransformPoint(data.原点+new Vector3(x+.5f,y+.5f,z+.5f)*data.格子边长);
                if(Mathf.Abs(p.y-slice)<.12f)points.Add(p);
            }
            Require(points.Count>0,"Tree slice missed trunk");
            var center=Vector3.zero;foreach(var p in points)center+=p;center/=points.Count;
            float radius=points.Max(p=>new Vector2(p.x-center.x,p.z-center.z).magnitude)+.35f;
            Debug.Log($"TREE_SUPPORT_TEST center={center} radius={radius:F2} bounds={tree.预期包围盒}");
            pilot.破坏球(center,radius);Drain(pilot,ref peak,ref units);
            var body=source.GetComponentInChildren<VoxelDestructible>();
            Require(body && body.剩余体素<data.实体数量*.6f,"Severed crown retained unsupported trunk");
            CheckStagedMeshes(body);
            var resource=source;for(int i=0;i<tree.资源上溯层数;i++)resource=resource.parent;
            var leaves=resource.GetComponentsInChildren<Renderer>().Where(r=>!r.GetComponentInParent<VoxelDestructible>() && !r.transform.IsChildOf(source)).ToArray();
            Require(leaves.Length>0 && leaves.All(r=>!r.enabled),"Severed tree retained floating crown");
            pilot.切换试点(false);
            Require(terrain.terrainData==original && source.GetComponent<MeshRenderer>().enabled && leaves.All(r=>r.enabled),"Environment rollback failed");
            var after=original.GetHoles(0,0,original.holesResolution,original.holesResolution);
            Require(baseline.Cast<bool>().SequenceEqual(after.Cast<bool>()),"Source TerrainData holes mutated");
            Require(manifest.替换项.Select(e=>e.数据).Distinct().All(d=>d.实体.Count(v=>v!=0)==d.实体数量),"Source voxel bake mutated");
            Debug.Log($"WILDERNESS_CHECK_PASS registered={pilot.替换数量} lazy=True groundDepth={depth:F2}m seam=True treeSupport=True crownRollback=True originalAssets=True workUnits={units} peakScheduler={peak:F2}ms");
        }
        finally {pilot.还原岩石();pilot.切换试点(wasActive);}
    }
    static Transform Locate(VoxelMapPilotSettings.Entry e)
    {var t=GameObject.Find(e.根名称).transform;foreach(int index in e.子索引)t=t.GetChild(index);return t;}
    static RaycastHit Floor(Vector3 point)
    {
        var hits=Physics.RaycastAll(point+Vector3.up*5,Vector3.down,15,~0,QueryTriggerInteraction.Ignore);
        var ground=hits.Where(h=>h.collider is TerrainCollider || h.collider.GetComponentInParent<VoxelDestructible>()?.数据.地形源).OrderBy(h=>h.distance).ToArray();
        Require(ground.Length>0,"No voxel ground collision");return ground[0];
    }
    static void Drain(VoxelMapPilot pilot,ref double peak,ref int units)
    {
        int guard=0;
        while(pilot.等待更新 && guard++<100000)
        {int count=pilot.更新局部();Require(count<=32,"Shared work safety cap exceeded");peak=Math.Max(peak,pilot.最近更新毫秒);units+=count;
         if(pilot.最近更新毫秒>8)Debug.Log($"VOXEL_SLOW_UNIT {pilot.最近阶段} {pilot.最近更新毫秒:F2}ms");}
        Require(!pilot.等待更新,$"Environment queue did not finish: ground={pilot.地面.待建地块数} stage={pilot.地面.最近阶段} bodies={string.Join(",",pilot.体素对象.Select(b=>b.等待块))}");Physics.SyncTransforms();
    }
    static void CheckSeams(VoxelMapPilot pilot)
    {
        var points=new System.Collections.Generic.Dictionary<Vector3,Vector3>();int matches=0;
        foreach(var body in UnityEngine.Object.FindObjectsOfType<VoxelDestructible>().Where(b=>b.数据.地形源))foreach(var filter in body.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;
            for(int i=0;i<vertices.Length;i++)
            {
                var world=filter.transform.TransformPoint(vertices[i]);
                var key=new Vector3(Mathf.Round(world.x*10000),Mathf.Round(world.y*10000),Mathf.Round(world.z*10000));
                var normal=filter.transform.TransformDirection(normals[i]);
                if(points.TryGetValue(key,out var previous)){Require((previous-normal).sqrMagnitude<.0001f,"Ground seam normals disagree");matches++;}
                else points.Add(key,normal);
            }
        }
        Require(matches>100,"Not enough shared boundary vertices");
    }
    static void CheckStagedMeshes(VoxelDestructible body)
    {
        var data=body.数据;var cells=new byte[data.实体.Length];var distances=body.复制距离();
        for(int z=0;z<data.尺寸.z;z++)for(int y=0;y<data.尺寸.y;y++)for(int x=0;x<data.尺寸.x;x++)
            cells[data.索引(x,y,z)]=(byte)(body.取实体(x,y,z)?1:0);
        foreach(var key in data.分块坐标)
        {
            var expected=VoxelChunkMesher.生成(data,cells,key,distances);
            try
            {
                var actual=body.取块网格(key);var av=actual.vertices;var ev=expected.vertices;var an=actual.normals;var en=expected.normals;
                Require(av.Length==ev.Length,"Staged tree mesh has stale vertices");
                for(int i=0;i<av.Length;i++)Require((av[i]-ev[i]).sqrMagnitude<1e-12f && (an[i]-en[i]).sqrMagnitude<1e-8f,"Staged tree has stale border normals");
                Require(actual.triangles.SequenceEqual(expected.triangles) && actual.uv.SequenceEqual(expected.uv),"Staged tree topology/UV differs from full rebuild");
            }
            finally {UnityEngine.Object.Destroy(expected);}
        }
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
