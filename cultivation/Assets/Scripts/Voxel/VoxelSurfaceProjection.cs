using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;

/// <summary>Preserve single-material source UV detail near the original surface; expose cut colour deeper inside.</summary>
public static class VoxelSurfaceProjection
{
    struct Projection {public Vector2 uv;public Vector3 normal;public byte blend;}
    sealed class Node {public Bounds bounds;public Node left,right;public int start,count;}
    sealed class SurfaceTree
    {
        public readonly Vector3[] points;
        public readonly Vector2[] uv;
        readonly int[] triangles;
        readonly Bounds[] bounds;
        readonly Vector3[] centers;
        readonly Node root;
        public SurfaceTree(Vector3[] points,Vector2[] uv)
        {
            this.points=points;this.uv=uv;int count=points.Length/3;
            triangles=new int[count];bounds=new Bounds[count];centers=new Vector3[count];
            for(int i=0;i<count;i++)
            {triangles[i]=i;var b=new Bounds(points[i*3],Vector3.zero);b.Encapsulate(points[i*3+1]);b.Encapsulate(points[i*3+2]);bounds[i]=b;centers[i]=b.center;}
            root=Build(0,count);
        }
        Node Build(int start,int count)
        {
            var b=bounds[triangles[start]];for(int i=start+1;i<start+count;i++)b.Encapsulate(bounds[triangles[i]]);
            var node=new Node {bounds=b,start=start,count=count};if(count<=8)return node;
            var size=b.size;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
            Array.Sort(triangles,start,count,Comparer<int>.Create((a,c)=>centers[a][axis].CompareTo(centers[c][axis])));
            int half=count/2;node.left=Build(start,half);node.right=Build(start+half,count-half);return node;
        }
        public void Nearest(Vector3 p,out float squared,out Vector3 point,out int triangle)
        {squared=float.PositiveInfinity;point=Vector3.zero;triangle=int.MaxValue;Search(root,p,ref squared,ref point,ref triangle);}
        void Search(Node node,Vector3 p,ref float best,ref Vector3 nearest,ref int triangle)
        {
            if(node.bounds.SqrDistance(p)>best)return;
            if(node.left!=null)
            {
                var a=node.left;var b=node.right;if(a.bounds.SqrDistance(p)>b.bounds.SqrDistance(p)){a=node.right;b=node.left;}
                Search(a,p,ref best,ref nearest,ref triangle);Search(b,p,ref best,ref nearest,ref triangle);return;
            }
            for(int i=node.start;i<node.start+node.count;i++)
            {
                int t=triangles[i]*3;var q=最近点(p,points[t],points[t+1],points[t+2]);float distance=(q-p).sqrMagnitude;
                if(distance<best || (distance==best && t<triangle)){best=distance;nearest=q;triangle=t;}
            }
        }
    }
    static readonly ConditionalWeakTable<VoxelVolumeAsset,SurfaceTree> trees=new ConditionalWeakTable<VoxelVolumeAsset,SurfaceTree>();
    public static void 预热(VoxelVolumeAsset data)
    {if(data.表面三角点!=null && data.表面三角点.Length>=3 && data.表面三角UV!=null && data.表面三角UV.Length==data.表面三角点.Length)GetTree(data);}
    static SurfaceTree GetTree(VoxelVolumeAsset data)
    {
        if(trees.TryGetValue(data,out var tree) && ReferenceEquals(tree.points,data.表面三角点) && ReferenceEquals(tree.uv,data.表面三角UV))return tree;
        trees.Remove(data);tree=new SurfaceTree(data.表面三角点,data.表面三角UV);trees.Add(data,tree);return tree;
    }
    public static void 应用(VoxelVolumeAsset data,Mesh mesh)
    {
        var points=data.表面三角点;var sourceUV=data.表面三角UV;
        if(points==null || sourceUV==null || points.Length<3 || points.Length!=sourceUV.Length)return;
        var tree=GetTree(data);
        var vertices=mesh.vertices;var colors=mesh.colors32;var uv=new Vector2[vertices.Length];var normals=mesh.normals;
        var sourceNormals=data.表面三角法线;bool preserveNormals=sourceNormals!=null && sourceNormals.Length==points.Length;
        var cache=new Dictionary<Vector3,Projection>();
        for(int i=0;i<vertices.Length;i++)
        {
            var p=vertices[i];
            if(!cache.TryGetValue(p,out var projection))
            {
                tree.Nearest(p,out float best,out var nearest,out int triangle);
                var a=points[triangle];var ab=points[triangle+1]-a;var ac=points[triangle+2]-a;var aq=nearest-a;
                float d00=Vector3.Dot(ab,ab),d01=Vector3.Dot(ab,ac),d11=Vector3.Dot(ac,ac),d20=Vector3.Dot(aq,ab),d21=Vector3.Dot(aq,ac);
                float denominator=d00*d11-d01*d01;
                float b=0,c=0;
                if(Mathf.Abs(denominator)>Mathf.Max(d00*d11*1e-8f,1e-35f)){b=(d11*d20-d01*d21)/denominator;c=(d00*d21-d01*d20)/denominator;}
                projection=new Projection {uv=sourceUV[triangle]*(1-b-c)+sourceUV[triangle+1]*b+sourceUV[triangle+2]*c,
                    blend=(byte)Mathf.RoundToInt(Mathf.Clamp01(2-Mathf.Sqrt(best)/data.格子边长)*255)};
                if(preserveNormals)projection.normal=(sourceNormals[triangle]*(1-b-c)+sourceNormals[triangle+1]*b+sourceNormals[triangle+2]*c).normalized;
                cache.Add(p,projection);
            }
            uv[i]=projection.uv;colors[i].a=projection.blend;
            if(preserveNormals)normals[i]=Vector3.Slerp(normals[i],projection.normal,projection.blend/255f).normalized;
        }
        mesh.uv=uv;mesh.colors32=colors;if(preserveNormals)mesh.normals=normals;
    }
    static Vector3 最近点(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
    {
        var ab=b-a;var ac=c-a;var ap=p-a;
        float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0 && d2<=0)return a;
        var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0 && d4<=d3)return b;
        float vc=d1*d4-d3*d2;if(vc<=0 && d1>=0 && d3<=0)return a+ab*(d1/(d1-d3));
        var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0 && d5<=d6)return c;
        float vb=d5*d2-d1*d6;if(vb<=0 && d2>=0 && d6<=0)return a+ac*(d2/(d2-d6));
        float va=d3*d6-d5*d4;if(va<=0 && d4-d3>=0 && d5-d6>=0)return b+(c-b)*((d4-d3)/(d4-d3+d5-d6));
        float denominator=va+vb+vc;if(Mathf.Abs(denominator)<1e-35f)return a;
        return a+ab*(vb/denominator)+ac*(vc/denominator);
    }
}
