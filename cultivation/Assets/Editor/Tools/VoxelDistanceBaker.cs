using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Offline signed distance to source triangles, accelerated by a median-split BVH.</summary>
public static class VoxelDistanceBaker
{
    struct Triangle { public Vector3 a,b,c,center;public Bounds bounds; }
    sealed class Node {public Bounds bounds;public Node left,right;public int start,count;}
    sealed class Tree
    {
        readonly Triangle[] triangles;
        readonly Node root;
        readonly List<float> hits=new List<float>();
        static readonly Vector3 RayDirection=new Vector3(1,.3713907f,.5291871f).normalized;
        public Tree(Vector3[] source)
        {
            triangles=new Triangle[source.Length/3];
            for(int i=0;i<triangles.Length;i++)
            {
                var a=source[i*3];var b=source[i*3+1];var c=source[i*3+2];var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                triangles[i]=new Triangle {a=a,b=b,c=c,center=(a+b+c)/3,bounds=bounds};
            }
            root=Build(0,triangles.Length);
        }
        Node Build(int start,int count)
        {
            var bounds=triangles[start].bounds;for(int i=start+1;i<start+count;i++)bounds.Encapsulate(triangles[i].bounds);
            var node=new Node {bounds=bounds,start=start,count=count};if(count<=8)return node;
            var size=bounds.size;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
            Array.Sort(triangles,start,count,Comparer<Triangle>.Create((a,b)=>a.center[axis].CompareTo(b.center[axis])));
            int half=count/2;node.left=Build(start,half);node.right=Build(start+half,count-half);return node;
        }
        public float Distance(Vector3 point,bool solid,float shellThickness)
        {
            float squared=float.PositiveInfinity;Nearest(root,point,ref squared);
            float distance=Mathf.Sqrt(squared);
            if(!solid)return shellThickness-distance;
            hits.Clear();Intersections(root,new Ray(point,RayDirection));hits.Sort();
            int crossings=0;float previous=float.NegativeInfinity;
            foreach(float hit in hits)if(hit-previous>1e-5f){crossings++;previous=hit;}
            return (crossings&1)!=0?distance:-distance;
        }
        void Nearest(Node node,Vector3 p,ref float best)
        {
            if(node.bounds.SqrDistance(p)>best)return;
            if(node.left!=null)
            {
                var a=node.left;var b=node.right;
                if(a.bounds.SqrDistance(p)>b.bounds.SqrDistance(p)){a=node.right;b=node.left;}
                Nearest(a,p,ref best);Nearest(b,p,ref best);return;
            }
            for(int i=node.start;i<node.start+node.count;i++)best=Mathf.Min(best,(Closest(p,triangles[i])-p).sqrMagnitude);
        }
        void Intersections(Node node,Ray ray)
        {
            var bounds=node.bounds;bounds.Expand(.00001f);if(!bounds.IntersectRay(ray,out _))return;
            if(node.left!=null){Intersections(node.left,ray);Intersections(node.right,ray);return;}
            for(int i=node.start;i<node.start+node.count;i++)
            {
                var t=triangles[i];var e1=t.b-t.a;var e2=t.c-t.a;var h=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,h);
                if(Mathf.Abs(det)<1e-10f)continue;
                float inv=1/det;var s=ray.origin-t.a;float u=inv*Vector3.Dot(s,h);if(u<0 || u>1)continue;
                var q=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(ray.direction,q);if(v<0 || u+v>1)continue;
                float distance=inv*Vector3.Dot(e2,q);if(distance>1e-6f)hits.Add(distance);
            }
        }
        static Vector3 Closest(Vector3 p,Triangle t)
        {
            var ab=t.b-t.a;var ac=t.c-t.a;var ap=p-t.a;
            float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0 && d2<=0)return t.a;
            var bp=p-t.b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0 && d4<=d3)return t.b;
            float vc=d1*d4-d3*d2;if(vc<=0 && d1>=0 && d3<=0)return t.a+ab*(d1/(d1-d3));
            var cp=p-t.c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0 && d5<=d6)return t.c;
            float vb=d5*d2-d1*d6;if(vb<=0 && d2>=0 && d6<=0)return t.a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0 && d4-d3>=0 && d5-d6>=0)return t.b+(t.c-t.b)*((d4-d3)/(d4-d3+d5-d6));
            float denom=va+vb+vc;if(Mathf.Abs(denom)<1e-20f)return (t.a+t.b+t.c)/3;
            return t.a+ab*(vb/denom)+ac*(vc/denom);
        }
    }

    public static void 烘焙(VoxelVolumeAsset data,Vector3[] triangles,bool solid)
    {
        if(triangles==null || triangles.Length<3)throw new ArgumentException("Missing triangles");
        // FBX meshes range from centimetres to tens of metres in native coordinates.
        // Normalize geometric predicates so ray tolerances do not depend on import scale.
        var bounds=new Bounds(triangles[0],Vector3.zero);foreach(var vertex in triangles)bounds.Encapsulate(vertex);
        float scale=1/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
        var normalized=new Vector3[triangles.Length];for(int i=0;i<triangles.Length;i++)normalized[i]=(triangles[i]-bounds.center)*scale;
        var tree=new Tree(normalized);data.距离=new float[data.实体.Length];
        Color average=Color.black;int count=0;
        for(int i=0;i<data.颜色.Length;i++)if(data.颜色[i].a>0){average+=(Color)data.颜色[i];count++;}
        Color32 fallback=count>0?average/count:Color.gray;
        float band=data.格子边长*4;data.实体数量=0;
        for(int z=0;z<data.尺寸.z;z++)for(int y=0;y<data.尺寸.y;y++)for(int x=0;x<data.尺寸.x;x++)
        {
            int index=data.索引(x,y,z);var p=data.原点+new Vector3(x+.5f,y+.5f,z+.5f)*data.格子边长;
            float distance=tree.Distance((p-bounds.center)*scale,solid,data.格子边长*.65f*scale)/scale;
            // A non-manifold source may leave a few unmatched ray crossings. Material cannot exist outside its bounds.
            if(solid && distance>0 && !bounds.Contains(p))distance=-distance;
            data.距离[index]=Mathf.Clamp(distance,-band,band);
            data.实体[index]=(byte)(distance>0?1:0);
            if(distance>0)data.实体数量++;
            if(data.颜色[index].a==0)data.颜色[index]=fallback;
        }
    }
}
