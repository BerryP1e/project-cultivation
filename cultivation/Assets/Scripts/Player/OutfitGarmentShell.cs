using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

/// <summary>沿袍面真实拓扑传递拉力；开襟边界没有跨口连线。只作用于外袍，不驱动人体。</summary>
public sealed class OutfitGarmentShell : IDisposable
{
    struct Link { public int a,b; public float length,aShare,bShare; public bool bend; }
    readonly int[] canonical;
    readonly Vector3[] rest, positions, previous, targets, outward;
    readonly float[] freedom;
    readonly bool[] contact;
    readonly Link[] links;
    readonly float[] posedLengths,faceAreas;
    readonly Vector3[] faceCrosses;
    readonly Vector3[] bootAxes=new Vector3[4];
    readonly float[] bootDenominators=new float[4],bootBaseSquared=new float[4],bootMaximum=new float[4];
    readonly Vector3[] bootNormals;
    readonly bool[] bootNormalReady;
    readonly int[] faces;
    readonly Vector3[] offsets,neighborOffsets;
    readonly int[] neighborCounts;
    readonly float[] contactSpacing;
    readonly bool fullShell;
    readonly float[] inner;
    OutfitGarmentJob job;
    bool jobReady;
    bool initialized;
    Matrix4x4 lastWaist;
    public int 细腿接触次数 { get; private set; }
    public int 鞋靴接触次数 { get; private set; }
    public float 最大鞋靴推出距离 { get; private set; }

    public OutfitGarmentShell(Vector3[] vertices,int[] triangles,int[] same,float[] hem,float[] progress,float[] limits,Vector3[] radial,bool fullShell=false,float[] inner=null)
    {
        this.fullShell=fullShell;
        this.inner=inner!=null&&inner.Length==vertices.Length?inner:null;
        faces=triangles;
        offsets=new Vector3[vertices.Length];neighborOffsets=new Vector3[vertices.Length];neighborCounts=new int[vertices.Length];contactSpacing=new float[vertices.Length];
        bootNormals=new Vector3[vertices.Length*4];bootNormalReady=new bool[bootNormals.Length];
        faceCrosses=new Vector3[triangles.Length/3];faceAreas=new float[faceCrosses.Length];
        canonical=same;rest=vertices;outward=radial;positions=new Vector3[rest.Length];previous=new Vector3[rest.Length];targets=new Vector3[rest.Length];freedom=new float[rest.Length];contact=new bool[rest.Length];
        for(int i=0;i<rest.Length;i++){
            // 保留已校正的后片作为衣型支撑；前襟和侧片通过原有缝线连续接在后片上。
            float front=fullShell?1:1-Mathf.SmoothStep(0,1,Mathf.Clamp01(radial[i].x/.7f));
            freedom[i]=limits[i]>.00001f?Mathf.SmoothStep(0,1,hem[i])*Mathf.Lerp(.08f,1,progress[i])*front:0;
            contact[i]=limits[i]>.00001f&&hem[i]>.95f;
        }
        var edges=new Dictionary<ulong,int>();var constraints=new List<Link>();
        void Add(int a,int b,bool bend){
            if(a==b||freedom[a]+freedom[b]<.00001f)return;
            float length=Vector3.Distance(rest[a],rest[b]);
            if(length>.00001f){float sum=freedom[a]+freedom[b];constraints.Add(new Link{a=a,b=b,length=length,bend=bend,aShare=freedom[a]/sum,bShare=freedom[b]/sum});}
        }
        for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++){
            int a=canonical[triangles[t+e]],b=canonical[triangles[t+(e+1)%3]],opposite=canonical[triangles[t+(e+2)%3]];
            ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
            if(edges.TryGetValue(key,out int other))Add(other,opposite,true);
            else{edges.Add(key,opposite);Add(a,b,false);}
        }
        links=constraints.ToArray();
        posedLengths=new float[links.Length];
        foreach(var link in links)if(!link.bend){neighborCounts[link.a]++;neighborCounts[link.b]++;}
    }

    public void Reset(){initialized=false;}

    public void Solve(Vector3[] points,Matrix4x4 mesh,Matrix4x4 waist,Matrix4x4 waistSkin,Vector3[] starts,Vector3[] ends,float[] radii,float scale,Vector3 velocity,float dt,Vector3[] legStarts=null,Vector3[] legEnds=null,float legRadius=.009f,float[] bootRadii=null,bool enclosed=false,bool shoesOnly=false,Vector3[] cuffStarts=null,Vector3[] cuffEnds=null,float[] cuffRadii=null,bool solidLegs=false)
    {
        if(!jobReady){
          try{
            job.canonical=new NativeArray<int>(canonical??Array.Empty<int>(),Allocator.Persistent);
            job.rest=new NativeArray<Vector3>(rest??Array.Empty<Vector3>(),Allocator.Persistent);
            job.positions=new NativeArray<Vector3>(positions??Array.Empty<Vector3>(),Allocator.Persistent);
            job.previous=new NativeArray<Vector3>(previous??Array.Empty<Vector3>(),Allocator.Persistent);
            job.targets=new NativeArray<Vector3>(targets??Array.Empty<Vector3>(),Allocator.Persistent);
            job.outward=new NativeArray<Vector3>(outward??Array.Empty<Vector3>(),Allocator.Persistent);
            job.freedom=new NativeArray<float>(freedom??Array.Empty<float>(),Allocator.Persistent);
            job.contact=new NativeArray<bool>(contact??Array.Empty<bool>(),Allocator.Persistent);
            var kernelLinks=new OutfitGarmentJob.Link[links.Length];
            for(int e=0;e<links.Length;e++){var v=links[e];kernelLinks[e]=new OutfitGarmentJob.Link{a=v.a,b=v.b,length=v.length,aShare=v.aShare,bShare=v.bShare,bend=v.bend};}
            job.links=new NativeArray<OutfitGarmentJob.Link>(kernelLinks,Allocator.Persistent);
            job.posedLengths=new NativeArray<float>(posedLengths??Array.Empty<float>(),Allocator.Persistent);
            job.faceAreas=new NativeArray<float>(faceAreas??Array.Empty<float>(),Allocator.Persistent);
            job.faceCrosses=new NativeArray<Vector3>(faceCrosses??Array.Empty<Vector3>(),Allocator.Persistent);
            job.bootAxes=new NativeArray<Vector3>(bootAxes??Array.Empty<Vector3>(),Allocator.Persistent);
            job.bootDenominators=new NativeArray<float>(bootDenominators??Array.Empty<float>(),Allocator.Persistent);
            job.bootBaseSquared=new NativeArray<float>(bootBaseSquared??Array.Empty<float>(),Allocator.Persistent);
            job.bootMaximum=new NativeArray<float>(bootMaximum??Array.Empty<float>(),Allocator.Persistent);
            job.bootNormals=new NativeArray<Vector3>(bootNormals??Array.Empty<Vector3>(),Allocator.Persistent);
            job.bootNormalReady=new NativeArray<bool>(bootNormalReady??Array.Empty<bool>(),Allocator.Persistent);
            job.faces=new NativeArray<int>(faces??Array.Empty<int>(),Allocator.Persistent);
            job.offsets=new NativeArray<Vector3>(offsets??Array.Empty<Vector3>(),Allocator.Persistent);
            job.neighborOffsets=new NativeArray<Vector3>(neighborOffsets??Array.Empty<Vector3>(),Allocator.Persistent);
            job.neighborCounts=new NativeArray<int>(neighborCounts??Array.Empty<int>(),Allocator.Persistent);
            job.contactSpacing=new NativeArray<float>(contactSpacing??Array.Empty<float>(),Allocator.Persistent);
            job.inner=new NativeArray<float>(inner??Array.Empty<float>(),Allocator.Persistent);
            job.points=new NativeArray<Vector3>(points??Array.Empty<Vector3>(),Allocator.Persistent);
            job.starts=new NativeArray<Vector3>(starts??Array.Empty<Vector3>(),Allocator.Persistent);
            job.ends=new NativeArray<Vector3>(ends??Array.Empty<Vector3>(),Allocator.Persistent);
            job.radii=new NativeArray<float>(radii??Array.Empty<float>(),Allocator.Persistent);
            job.legStarts=new NativeArray<Vector3>(legStarts??Array.Empty<Vector3>(),Allocator.Persistent);
            job.legEnds=new NativeArray<Vector3>(legEnds??Array.Empty<Vector3>(),Allocator.Persistent);
            job.bootRadii=new NativeArray<float>(bootRadii??Array.Empty<float>(),Allocator.Persistent);
            job.cuffStarts=new NativeArray<Vector3>(cuffStarts??Array.Empty<Vector3>(),Allocator.Persistent);
            job.cuffEnds=new NativeArray<Vector3>(cuffEnds??Array.Empty<Vector3>(),Allocator.Persistent);
            job.cuffRadii=new NativeArray<float>(cuffRadii??Array.Empty<float>(),Allocator.Persistent);
            job.stats=new NativeArray<float>(3,Allocator.Persistent);
            job.fullShell=fullShell;jobReady=true;
          }catch{job.Dispose();job=default;throw;}
        }
        if(points!=null)job.points.CopyFrom(points);
        if(starts!=null)job.starts.CopyFrom(starts);
        if(ends!=null)job.ends.CopyFrom(ends);
        if(radii!=null)job.radii.CopyFrom(radii);
        if(legStarts!=null)job.legStarts.CopyFrom(legStarts);
        if(legEnds!=null)job.legEnds.CopyFrom(legEnds);
        if(bootRadii!=null)job.bootRadii.CopyFrom(bootRadii);
        if(cuffStarts!=null)job.cuffStarts.CopyFrom(cuffStarts);
        if(cuffEnds!=null)job.cuffEnds.CopyFrom(cuffEnds);
        if(cuffRadii!=null)job.cuffRadii.CopyFrom(cuffRadii);
        job.mesh=mesh;job.inverse=mesh.inverse;job.waist=waist;job.waistSkin=waistSkin;job.carried=waist*(initialized&&dt>0?lastWaist:waist).inverse;
        job.initialized=initialized;job.scale=scale;job.velocity=velocity;job.dt=dt;job.legRadius=legRadius;job.enclosed=enclosed;job.shoesOnly=shoesOnly;job.solidLegs=solidLegs;
        job.Run();
        job.points.CopyTo(points);initialized=true;lastWaist=waist;
        细腿接触次数=(int)job.stats[0];鞋靴接触次数=(int)job.stats[1];最大鞋靴推出距离=job.stats[2];
    }

    public void Dispose(){if(jobReady){job.Dispose();jobReady=false;}initialized=false;}
}
