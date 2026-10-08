using System.Collections.Generic;
using UnityEngine;

/// <summary>Clip overlapping sample halos to exact terrain-hole boundaries and preserve top-surface material.</summary>
public static class VoxelTerrainMesh
{
    struct Vertex
    {
        public Vector3 p,n;public Color color;
        public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex {p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,color=Color.Lerp(a.color,b.color,t)};
    }
    public static void 处理(VoxelVolumeAsset data,Mesh mesh)
    {
        if(!data.限制水平范围)return;
        var input=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors32;var indices=mesh.triangles;
        var vertices=new List<Vector3>();var outputNormals=new List<Vector3>();var outputColors=new List<Color32>();var triangles=new List<int>();
        var polygon=new List<Vertex>(8);var scratch=new List<Vertex>(8);
        for(int i=0;i<indices.Length;i+=3)
        {
            polygon.Clear();for(int j=0;j<3;j++){int v=indices[i+j];polygon.Add(new Vertex {p=input[v],n=normals[v],color=colors[v]});}
            Clip(polygon,scratch,0,data.水平最小.x,true);Clip(polygon,scratch,0,data.水平最大.x,false);
            Clip(polygon,scratch,2,data.水平最小.y,true);Clip(polygon,scratch,2,data.水平最大.y,false);
            if(polygon.Count<3)continue;
            int start=vertices.Count;
            foreach(var v in polygon)
            {
                var color=v.color;
                if(data.地形源)
                {
                    float height=data.地形源.GetInterpolatedHeight(v.p.x/data.地形源.size.x,v.p.z/data.地形源.size.z);
                    color.a=Mathf.Clamp01(2-(height-v.p.y)/data.格子边长);
                }
                vertices.Add(v.p);outputNormals.Add(v.n);outputColors.Add(color);
            }
            for(int j=1;j+1<polygon.Count;j++){triangles.Add(start);triangles.Add(start+j);triangles.Add(start+j+1);}
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(outputNormals);mesh.SetColors(outputColors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
    }
    static void Clip(List<Vertex> polygon,List<Vertex> scratch,int axis,float value,bool minimum)
    {
        scratch.Clear();if(polygon.Count==0)return;
        var previous=polygon[polygon.Count-1];bool prevInside=minimum?previous.p[axis]>=value:previous.p[axis]<=value;
        foreach(var current in polygon)
        {
            bool inside=minimum?current.p[axis]>=value:current.p[axis]<=value;
            if(inside!=prevInside)scratch.Add(Vertex.Lerp(previous,current,(value-previous.p[axis])/(current.p[axis]-previous.p[axis])));
            if(inside)scratch.Add(current);previous=current;prevInside=inside;
        }
        polygon.Clear();polygon.AddRange(scratch);
    }
}
