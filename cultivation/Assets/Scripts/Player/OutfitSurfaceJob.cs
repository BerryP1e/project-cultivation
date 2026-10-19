using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>布料蒙皮、局部接触及美术法线传递；与袍面约束使用相同顺序和精度。</summary>
public sealed class OutfitSurfaceJob : IDisposable
{
    SurfaceJob job;

    public OutfitSurfaceJob(AppearanceClothProfile profile,Vector3[] rest,Vector3[] normals,BoneWeight[] weights,
        Vector3[] outward,Vector3[] hemOutward,float[] hem,float[] progress,float[] phase,int[] counts,int[] triangles,int boneCount)
    {
        try{
        job.rest=new NativeArray<Vector3>(rest,Allocator.Persistent);job.restNormals=new NativeArray<Vector3>(normals,Allocator.Persistent);
        job.weights=new NativeArray<BoneWeight>(weights,Allocator.Persistent);job.outward=new NativeArray<Vector3>(outward,Allocator.Persistent);
        job.hemOutward=new NativeArray<Vector3>(hemOutward,Allocator.Persistent);job.hem=new NativeArray<float>(hem,Allocator.Persistent);
        job.progress=new NativeArray<float>(progress,Allocator.Persistent);job.phase=new NativeArray<float>(phase,Allocator.Persistent);
        job.freedom=new NativeArray<float>(profile.布料顶点约束,Allocator.Persistent);job.closed=new NativeArray<float>(profile.闭襟覆盖,Allocator.Persistent);
        job.canonical=new NativeArray<int>(profile.布料同点,Allocator.Persistent);job.counts=new NativeArray<int>(counts,Allocator.Persistent);
        job.triangles=new NativeArray<int>(triangles,Allocator.Persistent);job.transforms=new NativeArray<Matrix4x4>(boneCount,Allocator.Persistent);
        job.points=new NativeArray<Vector3>(rest.Length,Allocator.Persistent);job.normals=new NativeArray<Vector3>(rest.Length,Allocator.Persistent);
        job.targets=new NativeArray<Vector3>(rest.Length,Allocator.Persistent);job.offsets=new NativeArray<Vector3>(rest.Length,Allocator.Persistent);
        job.normalSums=new NativeArray<Vector3>(rest.Length,Allocator.Persistent);job.seams=new NativeArray<Vector3>(rest.Length,Allocator.Persistent);
        job.starts=new NativeArray<Vector3>(9,Allocator.Persistent);job.ends=new NativeArray<Vector3>(9,Allocator.Persistent);
        job.radii=new NativeArray<float>(9,Allocator.Persistent);job.collisionBones=new NativeArray<int>(new[]{1,3,4,12,13,31,32,55,56},Allocator.Persistent);
        job.thinStarts=new NativeArray<Vector3>(6,Allocator.Persistent);job.thinEnds=new NativeArray<Vector3>(6,Allocator.Persistent);
        job.pose=profile.姿态空间随动;job.shoesOnly=profile.仅保留原鞋;job.waveAmplitude=profile.衣摆波幅;job.thinRadius=profile.细腿半径;
        job.contactTransforms=new NativeArray<Matrix4x4>(9,Allocator.Persistent);
        }catch{job.Dispose();throw;}
    }

    public void 准备(Vector3[] points,Matrix4x4[] transforms,Matrix4x4 mesh,Vector3[] starts,Vector3[] ends,float[] radii,
        Vector3[] thinStarts,Vector3[] thinEnds,float scale,Vector3 inertia,Vector3 velocity,float dt,float clock)
    {
        job.transforms.CopyFrom(transforms);job.starts.CopyFrom(starts);job.ends.CopyFrom(ends);job.radii.CopyFrom(radii);
        job.thin=thinStarts!=null;if(job.thin){job.thinStarts.CopyFrom(thinStarts);job.thinEnds.CopyFrom(thinEnds);}
        job.mesh=mesh;job.inverse=mesh.inverse;job.scale=scale;job.inertia=inertia;job.velocity=velocity;job.dt=dt;job.clock=clock;
        for(int c=0;c<9;c++)job.contactTransforms[c]=mesh*transforms[job.collisionBones[c]];
        new PrepareJob{surface=job}.Schedule(points.Length,128).Complete();job.points.CopyTo(points);
    }

    public void 接缝(Vector3[] points){job.points.CopyFrom(points);job.stage=1;job.Run();job.points.CopyTo(points);}
    public void 完成(Vector3[] points,Vector3[] normals){job.points.CopyFrom(points);job.stage=2;job.Run();job.normals.CopyTo(normals);}
    public void 重置(){for(int i=0;i<job.offsets.Length;i++)job.offsets[i]=Vector3.zero;}
    public void Dispose(){job.Dispose();}

    [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
    struct PrepareJob : IJobParallelFor
    {
        public SurfaceJob surface;
        public void Execute(int index){surface.Prepare(index);}
    }

    [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
    struct SurfaceJob : IJob,IDisposable
    {
        [ReadOnly] public NativeArray<Vector3> rest,restNormals,outward,hemOutward,starts,ends,thinStarts,thinEnds;
        public NativeArray<Vector3> points,normals,targets,offsets,normalSums,seams;
        [ReadOnly] public NativeArray<BoneWeight> weights;
        [ReadOnly] public NativeArray<float> hem,progress,phase,freedom,closed,radii;
        [ReadOnly] public NativeArray<int> canonical,counts,triangles,collisionBones;
        [ReadOnly] public NativeArray<Matrix4x4> transforms,contactTransforms;
        public bool pose,shoesOnly,thin;
        public int stage;
        public float scale,dt,clock,waveAmplitude,thinRadius;
        public Vector3 inertia,velocity;
        public Matrix4x4 mesh,inverse;

        void Proxy(int i,int c,out Vector3 a,out Vector3 b,out float radius)
        {
            bool useThin=thin&&closed.Length==points.Length&&closed[i]>.5f&&c>=1&&c<=4;
            a=useThin?thinStarts[c-1]:starts[c];b=useThin?thinEnds[c-1]:ends[c];radius=(useThin?thinRadius+.004f:radii[c]+.008f)*scale;
        }
        static Vector3 Outside(Vector3 p,Vector3 a,Vector3 b,float radius,Vector3 reference)
        {
            var axis=b-a;var closest=a+axis*Mathf.Clamp01(Vector3.Dot(p-a,axis)/Mathf.Max(.000001f,axis.sqrMagnitude));
            var offset=p-closest;float distance=offset.magnitude;if(distance>=radius)return p;
            var fallback=Vector3.Cross(axis,Vector3.right).normalized;if(fallback.sqrMagnitude<.5f)fallback=Vector3.forward;
            if(reference.sqrMagnitude>.5f)fallback=reference.normalized;
            var normal=distance>.00001f?Vector3.Slerp(fallback,offset/distance,Mathf.SmoothStep(0,1,distance/(radius*.8f))).normalized:fallback;
            return closest+normal*radius;
        }
        Vector3 Direction(Vector3 n,BoneWeight w)=>
            (transforms[w.boneIndex0].MultiplyVector(n)*w.weight0+transforms[w.boneIndex1].MultiplyVector(n)*w.weight1+
            transforms[w.boneIndex2].MultiplyVector(n)*w.weight2+transforms[w.boneIndex3].MultiplyVector(n)*w.weight3).normalized;

        public void Execute()
        {
            if(stage==1){
                for(int i=0;i<seams.Length;i++)seams[i]=Vector3.zero;
                for(int i=0;i<points.Length;i++)seams[canonical[i]]+=points[i];
                for(int i=0;i<points.Length;i++)points[i]=seams[canonical[i]]/counts[canonical[i]];return;
            }
            if(stage==2){
                if(!pose)return;
                for(int i=0;i<normalSums.Length;i++)normalSums[i]=Vector3.zero;
                for(int t=0;t<triangles.Length;t+=3){
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    var before=Vector3.Cross(targets[b]-targets[a],targets[c]-targets[a]);var after=Vector3.Cross(points[b]-points[a],points[c]-points[a]);float area=before.magnitude;
                    if(area<1e-8f||after.sqrMagnitude<1e-16f)continue;var rotation=Quaternion.FromToRotation(before,after);
                    normalSums[a]+=rotation*normals[a]*area;normalSums[b]+=rotation*normals[b]*area;normalSums[c]+=rotation*normals[c]*area;
                }
                for(int i=0;i<points.Length;i++)if(normalSums[i].sqrMagnitude>1e-16f)normals[i]=normalSums[i].normalized;return;
            }
            for(int i=0;i<points.Length;i++)Prepare(i);
        }

        public void Prepare(int i)
        {
                float blend=dt>0?1-Mathf.Exp(-12*dt):1;
                float wind=Mathf.Lerp(.15f,1,Mathf.Clamp01(velocity.magnitude/5));
                var w=weights[i];var p=rest[i];
                var raw=transforms[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+transforms[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+
                    transforms[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+transforms[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                targets[i]=points[i]=raw;normals[i]=Direction(restNormals[i],w);
                float distance=freedom[i];if(distance<=.00001f)return;float freedomWeight=Mathf.Clamp01(distance/.012f);
                if(pose&&hem[i]>.00001f){
                    float wave=(Mathf.Sin(clock*7-progress[i]*6+phase[i]*2)+.35f*Mathf.Sin(clock*11-progress[i]*8-phase[i]*3))/1.35f;
                    float amplitude=waveAmplitude*Mathf.Pow(progress[i],2.5f)*hem[i]*hem[i]*wind;
                    var world=mesh.MultiplyPoint3x4(raw);float clearance=float.PositiveInfinity;
                    for(int c=0;c<9;c++){
                        if(shoesOnly&&hem[i]>.00001f&&c>=1&&c<=4)continue;
                        Proxy(i,c,out var a,out var b,out float radius);var axis=b-a;var closest=a+axis*Mathf.Clamp01(Vector3.Dot(world-a,axis)/Mathf.Max(.000001f,axis.sqrMagnitude));
                        clearance=Mathf.Min(clearance,Vector3.Distance(world,closest)-radius);
                    }
                    amplitude=Mathf.Min(amplitude,Mathf.Max(0,clearance)*.6f/Mathf.Max(scale,.00001f));raw+=Direction(hemOutward[i],w)*(wave*amplitude);
                }
                var worldPoint=mesh.MultiplyPoint3x4(raw);if(!pose)worldPoint+=Vector3.down*distance*.18f*scale+inertia*freedomWeight;
                for(int c=0;c<9;c++){
                    if(shoesOnly&&hem[i]>.00001f&&c>=1&&c<=4)continue;
                    Proxy(i,c,out var a,out var b,out float radius);var normal=contactTransforms[c].MultiplyVector(outward[i*9+c]).normalized;
                    worldPoint=Outside(worldPoint,a,b,radius,normal);
                }
                var offset=Vector3.ClampMagnitude(inverse.MultiplyPoint3x4(worldPoint)-raw,distance);offsets[i]=Vector3.Lerp(offsets[i],offset,blend);points[i]=raw+offsets[i];
        }
        public void Dispose()
        {
            if(rest.IsCreated)rest.Dispose();if(restNormals.IsCreated)restNormals.Dispose();if(outward.IsCreated)outward.Dispose();if(hemOutward.IsCreated)hemOutward.Dispose();
            if(points.IsCreated)points.Dispose();if(normals.IsCreated)normals.Dispose();if(targets.IsCreated)targets.Dispose();if(offsets.IsCreated)offsets.Dispose();if(normalSums.IsCreated)normalSums.Dispose();if(seams.IsCreated)seams.Dispose();
            if(starts.IsCreated)starts.Dispose();if(ends.IsCreated)ends.Dispose();if(thinStarts.IsCreated)thinStarts.Dispose();if(thinEnds.IsCreated)thinEnds.Dispose();if(weights.IsCreated)weights.Dispose();
            if(hem.IsCreated)hem.Dispose();if(progress.IsCreated)progress.Dispose();if(phase.IsCreated)phase.Dispose();if(freedom.IsCreated)freedom.Dispose();if(closed.IsCreated)closed.Dispose();if(radii.IsCreated)radii.Dispose();
            if(canonical.IsCreated)canonical.Dispose();if(counts.IsCreated)counts.Dispose();if(triangles.IsCreated)triangles.Dispose();if(collisionBones.IsCreated)collisionBones.Dispose();if(transforms.IsCreated)transforms.Dispose();
            if(contactTransforms.IsCreated)contactTransforms.Dispose();
        }
    }
}
