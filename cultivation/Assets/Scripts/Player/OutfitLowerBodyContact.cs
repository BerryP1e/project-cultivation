using System;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>外摆对实际裤靴蒙皮的局部接触，不驱动人体骨或改变袍面弹簧。</summary>
public sealed class OutfitLowerBodyContact : IDisposable
{
    ContactJob job;
    readonly Matrix4x4[] matrices=new Matrix4x4[78];

    public OutfitLowerBodyContact(AppearanceClothProfile profile)
    {
        var lower=profile.裤装蒙皮;var cloth=profile.布料蒙皮;
        if(profile.裤靴面分组.Length!=lower.triangles.Length/3)throw new InvalidOperationException("裤靴闭合分组与源面不匹配");
        job.rest=new NativeArray<Vector3>(lower.vertices,Allocator.Persistent);job.weights=new NativeArray<BoneWeight>(lower.boneWeights,Allocator.Persistent);
        job.binds=new NativeArray<Matrix4x4>(lower.bindposes,Allocator.Persistent);job.matrices=new NativeArray<Matrix4x4>(78,Allocator.Persistent);
        job.lower=new NativeArray<Vector3>(lower.vertexCount,Allocator.Persistent);job.triangles=new NativeArray<int>(lower.triangles,Allocator.Persistent);
        job.groups=new NativeArray<int>(profile.裤靴面分组,Allocator.Persistent);
        job.minimum=new NativeArray<Vector3>(lower.triangles.Length/3,Allocator.Persistent);job.maximum=new NativeArray<Vector3>(lower.triangles.Length/3,Allocator.Persistent);job.normals=new NativeArray<Vector3>(lower.triangles.Length/3,Allocator.Persistent);
        var direction=new float[lower.triangles.Length/3];var points=lower.vertices;var normals=lower.normals;var triangles=lower.triangles;
        for(int t=0;t<triangles.Length;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2];direction[t/3]=Vector3.Dot(Vector3.Cross(points[b]-points[a],points[c]-points[a]),normals[a]+normals[b]+normals[c])>=0?1:-1;}
        job.orientation=new NativeArray<float>(direction,Allocator.Persistent);
        job.points=new NativeArray<Vector3>(cloth.vertexCount,Allocator.Persistent);job.world=new NativeArray<Vector3>(cloth.vertexCount,Allocator.Persistent);
        job.offsets=new NativeArray<Vector3>(cloth.vertexCount,Allocator.Persistent);
        job.canonical=new NativeArray<int>(profile.布料同点,Allocator.Persistent);job.faces=new NativeArray<int>(cloth.triangles,Allocator.Persistent);
        var active=new bool[cloth.vertexCount];for(int i=0;i<active.Length;i++)active[i]=profile.袍面覆盖.Length==active.Length&&profile.袍面覆盖[i]>.95f&&profile.布料顶点约束[i]>1e-5f&&profile.袍面进度[i]>.08f;
        job.active=new NativeArray<bool>(active,Allocator.Persistent);
        // 固定拓扑的层次包围盒只在换装时建立；每帧随裤靴蒙皮重拟合。
        var order=new int[triangles.Length/3];for(int i=0;i<order.Length;i++)order[i]=i;
        var nodes=new List<ContactNode>();
        int Build(int first,int count)
        {
            int index=nodes.Count;nodes.Add(default);var node=new ContactNode{first=first,count=count,left=-1,right=-1};
            if(count>4){
                var min=Vector3.one*float.PositiveInfinity;var max=Vector3.one*float.NegativeInfinity;
                for(int i=first;i<first+count;i++){int t=order[i]*3;var center=(points[triangles[t]]+points[triangles[t+1]]+points[triangles[t+2]])/3;min=Vector3.Min(min,center);max=Vector3.Max(max,center);}
                var size=max-min;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
                Array.Sort(order,first,count,Comparer<int>.Create((a,b)=>{
                    float ca=points[triangles[a*3]][axis]+points[triangles[a*3+1]][axis]+points[triangles[a*3+2]][axis];
                    float cb=points[triangles[b*3]][axis]+points[triangles[b*3+1]][axis]+points[triangles[b*3+2]][axis];return ca.CompareTo(cb);
                }));
                int half=count/2;node.left=Build(first,half);node.right=Build(first+half,count-half);node.count=0;
            }
            nodes[index]=node;return index;
        }
        Build(0,order.Length);job.order=new NativeArray<int>(order,Allocator.Persistent);job.nodes=new NativeArray<ContactNode>(nodes.ToArray(),Allocator.Persistent);
    }

    public void Solve(Vector3[] points,Matrix4x4 mesh,Transform[] bones,float scale)
    {
        for(int i=0;i<78;i++)matrices[i]=bones[i].localToWorldMatrix;
        job.matrices.CopyFrom(matrices);job.points.CopyFrom(points);job.mesh=mesh;job.inverse=mesh.inverse;job.margin=.004f*scale;job.Run();job.hasHistory=true;job.points.CopyTo(points);
    }

    public void Dispose(){job.Dispose();}
    public void Reset(){job.hasHistory=false;}

    struct ContactNode
    {
        public int first,count,left,right;
        public Vector3 min,max;
    }

    [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
    struct ContactJob : IJob,IDisposable
    {
        public NativeArray<Vector3> rest,lower,minimum,maximum,normals,points,world,offsets;
        public NativeArray<BoneWeight> weights;
        public NativeArray<Matrix4x4> binds,matrices;
        public NativeArray<int> triangles,canonical,faces,groups;
        public NativeArray<float> orientation;
        public NativeArray<bool> active;
        public NativeArray<int> order;
        public NativeArray<ContactNode> nodes;
        public Matrix4x4 mesh,inverse;
        public float margin;
        public bool hasHistory;
        Vector3 boundsMin,boundsMax;

        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float den=va+vb+vc;if(Mathf.Abs(den)<1e-16f)return a;return a+ab*(vb/den)+ac*(vc/den);
        }

        Vector3 Push(Vector3 p)
        {
            if(p.x<boundsMin.x-margin||p.x>boundsMax.x+margin||p.y<boundsMin.y-margin||p.y>boundsMax.y+margin||p.z<boundsMin.z-margin||p.z>boundsMax.z+margin)return Vector3.zero;
            // 分别判定裤装/双靴的闭合体并取并集。最近面的法线符号在凹角与裤靴重叠处
            // 不能代表内外，否则会把本来已在外面的袍角拉到另一面，产生尖刺。
            int inside=0;float y=p.y+margin*.00123f,z=p.z+margin*.00213f;
            var stack=new FixedList512Bytes<int>();stack.Add(0);
            while(stack.Length>0){
                int index=stack[stack.Length-1];stack.RemoveAt(stack.Length-1);var node=nodes[index];
                if(y<node.min.y||y>node.max.y||z<node.min.z||z>node.max.z||p.x>=node.max.x)continue;
                if(node.count==0){stack.Add(node.left);stack.Add(node.right);continue;}
                for(int entry=node.first;entry<node.first+node.count;entry++){int t=order[entry]*3;
                var min=minimum[t/3];var max=maximum[t/3];if(y<min.y||y>max.y||z<min.z||z>max.z||p.x>=max.x)continue;
                var a=lower[triangles[t]];var b=lower[triangles[t+1]];var c=lower[triangles[t+2]];
                float by=b.y-a.y,bz=b.z-a.z,cy=c.y-a.y,cz=c.z-a.z,den=by*cz-bz*cy;if(Mathf.Abs(den)<1e-12f)continue;
                float u=((y-a.y)*cz-(z-a.z)*cy)/den,v=(by*(z-a.z)-bz*(y-a.y))/den;
                if(u<0||v<0||u+v>1)continue;float x=a.x+(b.x-a.x)*u+(c.x-a.x)*v;if(x>p.x)inside^=1<<groups[t/3];
                }
            }
            float best=float.PositiveInfinity;var closest=Vector3.zero;var normal=Vector3.zero;
            stack.Add(0);
            while(stack.Length>0){
                int index=stack[stack.Length-1];stack.RemoveAt(stack.Length-1);var node=nodes[index];
                if(BoundsDistance(p,node.min,node.max)>best)continue;
                if(node.count==0){
                    var left=nodes[node.left];var right=nodes[node.right];
                    if(BoundsDistance(p,left.min,left.max)<BoundsDistance(p,right.min,right.max)){stack.Add(node.right);stack.Add(node.left);}else{stack.Add(node.left);stack.Add(node.right);}continue;
                }
                for(int entry=node.first;entry<node.first+node.count;entry++){int t=order[entry]*3;
                if(inside!=0&&(inside&(1<<groups[t/3]))==0)continue;
                var min=minimum[t/3];var max=maximum[t/3];var delta=new Vector3(Mathf.Max(0,Mathf.Max(min.x-p.x,p.x-max.x)),Mathf.Max(0,Mathf.Max(min.y-p.y,p.y-max.y)),Mathf.Max(0,Mathf.Max(min.z-p.z,p.z-max.z)));
                if(delta.sqrMagnitude>best)continue;
                var q=Closest(p,lower[triangles[t]],lower[triangles[t+1]],lower[triangles[t+2]]);float distance=(p-q).sqrMagnitude;
                if(distance<best){best=distance;closest=q;normal=normals[t/3];}
                }
            }
            if(normal.sqrMagnitude<.5f)return Vector3.zero;
            if(inside==0&&best>=margin*margin)return Vector3.zero;
            var direction=inside!=0?closest-p:p-closest;if(direction.sqrMagnitude<1e-12f)direction=normal;else direction.Normalize();
            return closest+direction*margin-p;
        }

        static float BoundsDistance(Vector3 p,Vector3 min,Vector3 max)
        {
            var d=new Vector3(Mathf.Max(0,Mathf.Max(min.x-p.x,p.x-max.x)),Mathf.Max(0,Mathf.Max(min.y-p.y,p.y-max.y)),Mathf.Max(0,Mathf.Max(min.z-p.z,p.z-max.z)));return d.sqrMagnitude;
        }

        static bool Intersects(Vector3 a,Vector3 b,Vector3 p,Vector3 q,Vector3 r)
        {
            var d=b-a;var e1=q-p;var e2=r-p;var h=Vector3.Cross(d,e2);float den=Vector3.Dot(e1,h);if(Mathf.Abs(den)<1e-12f)return false;
            var s=a-p;float u=Vector3.Dot(s,h)/den;if(u<0||u>1)return false;var cross=Vector3.Cross(s,e1);float v=Vector3.Dot(d,cross)/den;if(v<0||u+v>1)return false;
            float t=Vector3.Dot(e2,cross)/den;return t>1e-5f&&t<.99999f;
        }

        bool FaceContact(int a,int b,int c)
        {
            bool changed=false;
            var min=Vector3.Min(world[a],Vector3.Min(world[b],world[c]));var max=Vector3.Max(world[a],Vector3.Max(world[b],world[c]));
            var stack=new FixedList512Bytes<int>();stack.Add(0);
            while(stack.Length>0){
                int index=stack[stack.Length-1];stack.RemoveAt(stack.Length-1);var node=nodes[index];
                if(min.x>node.max.x||min.y>node.max.y||min.z>node.max.z||max.x<node.min.x||max.y<node.min.y||max.z<node.min.z)continue;
                if(node.count==0){stack.Add(node.left);stack.Add(node.right);continue;}
                for(int entry=node.first;entry<node.first+node.count;entry++){int t=order[entry]*3;
                var lo=minimum[t/3];var hi=maximum[t/3];if(min.x>hi.x||min.y>hi.y||min.z>hi.z||max.x<lo.x||max.y<lo.y||max.z<lo.z)continue;
                var p=lower[triangles[t]];var q=lower[triangles[t+1]];var r=lower[triangles[t+2]];
                if(!Intersects(world[a],world[b],p,q,r)&&!Intersects(world[b],world[c],p,q,r)&&!Intersects(world[c],world[a],p,q,r)&&!Intersects(p,q,world[a],world[b],world[c])&&!Intersects(q,r,world[a],world[b],world[c])&&!Intersects(r,p,world[a],world[b],world[c]))continue;
                // 三个角都在裤靴外仍可能跨过实体；按实际相交面约束整片局部袍料。
                var n=normals[t/3];if(n.sqrMagnitude<.5f)continue;
                changed=true;
                if(active[a]){float depth=margin-Vector3.Dot(world[a]-p,n);if(depth>0)world[a]+=n*Mathf.Min(depth,margin*12);}
                if(active[b]){float depth=margin-Vector3.Dot(world[b]-p,n);if(depth>0)world[b]+=n*Mathf.Min(depth,margin*12);}
                if(active[c]){float depth=margin-Vector3.Dot(world[c]-p,n);if(depth>0)world[c]+=n*Mathf.Min(depth,margin*12);}
                min=Vector3.Min(world[a],Vector3.Min(world[b],world[c]));max=Vector3.Max(world[a],Vector3.Max(world[b],world[c]));
                }
            }
            return changed;
        }

        public void Execute()
        {
            boundsMin=Vector3.one*float.PositiveInfinity;boundsMax=Vector3.one*float.NegativeInfinity;
            for(int b=0;b<78;b++)matrices[b]=matrices[b]*binds[b];
            for(int i=0;i<rest.Length;i++){
                var w=weights[i];var p=rest[i];var x=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                lower[i]=x;boundsMin=Vector3.Min(boundsMin,x);boundsMax=Vector3.Max(boundsMax,x);
            }
            for(int t=0;t<triangles.Length;t+=3){var a=lower[triangles[t]];var b=lower[triangles[t+1]];var c=lower[triangles[t+2]];minimum[t/3]=Vector3.Min(a,Vector3.Min(b,c));maximum[t/3]=Vector3.Max(a,Vector3.Max(b,c));normals[t/3]=Vector3.Cross(b-a,c-a).normalized*orientation[t/3];}
            for(int i=nodes.Length-1;i>=0;i--){
                var node=nodes[i];
                if(node.count==0){node.min=Vector3.Min(nodes[node.left].min,nodes[node.right].min);node.max=Vector3.Max(nodes[node.left].max,nodes[node.right].max);}
                else{node.min=Vector3.one*float.PositiveInfinity;node.max=Vector3.one*float.NegativeInfinity;for(int e=node.first;e<node.first+node.count;e++){node.min=Vector3.Min(node.min,minimum[order[e]]);node.max=Vector3.Max(node.max,maximum[order[e]]);}}
                nodes[i]=node;
            }
            // 局部接触位移跨帧续算，避免每帧从已穿模的目标重新求十几轮；离开接触后渐退。
            for(int i=0;i<points.Length;i++)world[i]=mesh.MultiplyPoint3x4(points[i]+(hasHistory&&active[canonical[i]]?offsets[canonical[i]]*.92f:Vector3.zero));
            for(int pass=0;pass<16;pass++){
                bool changed=false;
                for(int i=0;i<points.Length;i++)if(canonical[i]==i&&active[i]){var push=Push(world[i]);world[i]+=push;changed|=push.sqrMagnitude>1e-12f;}
                for(int f=0;f<faces.Length;f+=3){
                    int a=canonical[faces[f]],b=canonical[faces[f+1]],c=canonical[faces[f+2]];if(!active[a]&&!active[b]&&!active[c])continue;
                    var min=Vector3.Min(world[a],Vector3.Min(world[b],world[c]));var max=Vector3.Max(world[a],Vector3.Max(world[b],world[c]));
                    if(min.x>boundsMax.x+margin||min.y>boundsMax.y+margin||min.z>boundsMax.z+margin||max.x<boundsMin.x-margin||max.y<boundsMin.y-margin||max.z<boundsMin.z-margin)continue;
                    changed|=FaceContact(a,b,c);
                }
                if(!changed)break;
            }
            for(int i=0;i<points.Length;i++)if(canonical[i]==i&&active[i])offsets[i]=Vector3.ClampMagnitude(inverse.MultiplyPoint3x4(world[i])-points[i],.22f);
            for(int i=0;i<points.Length;i++)if(active[canonical[i]])points[i]+=offsets[canonical[i]];
        }

        public void Dispose()
        {
            if(rest.IsCreated)rest.Dispose();if(lower.IsCreated)lower.Dispose();if(minimum.IsCreated)minimum.Dispose();if(maximum.IsCreated)maximum.Dispose();if(normals.IsCreated)normals.Dispose();if(points.IsCreated)points.Dispose();if(world.IsCreated)world.Dispose();if(offsets.IsCreated)offsets.Dispose();
            if(weights.IsCreated)weights.Dispose();if(binds.IsCreated)binds.Dispose();if(matrices.IsCreated)matrices.Dispose();if(triangles.IsCreated)triangles.Dispose();if(canonical.IsCreated)canonical.Dispose();if(faces.IsCreated)faces.Dispose();if(orientation.IsCreated)orientation.Dispose();if(active.IsCreated)active.Dispose();if(groups.IsCreated)groups.Dispose();
            if(order.IsCreated)order.Dispose();if(nodes.IsCreated)nodes.Dispose();
        }
    }
}
