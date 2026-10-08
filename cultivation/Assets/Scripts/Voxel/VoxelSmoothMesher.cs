using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Surface nets over occupancy samples. Shared global samples keep chunk seams continuous.</summary>
public static class VoxelSmoothMesher
{
    static readonly Vector3Int[] Corners = {
        new Vector3Int(0,0,0),new Vector3Int(1,0,0),new Vector3Int(0,1,0),new Vector3Int(1,1,0),
        new Vector3Int(0,0,1),new Vector3Int(1,0,1),new Vector3Int(0,1,1),new Vector3Int(1,1,1) };
    static readonly int[] Edges={0,1,2,3,4,5,6,7,0,2,1,3,4,6,5,7,0,4,1,5,2,6,3,7};
    struct Vertex { public Vector3 p,n; public Color32 color; }
    sealed class Field
    {
        readonly VoxelVolumeAsset data;readonly byte[] cells;readonly float[] distances;
        readonly Dictionary<Vector3Int,float> values=new Dictionary<Vector3Int,float>();
        public Field(VoxelVolumeAsset data,byte[] cells,float[] distances) {this.data=data;this.cells=cells;this.distances=distances;}
        public float Sample(Vector3Int p)
        {
            if(distances!=null)
                return data.范围内(p.x,p.y,p.z)?distances[data.索引(p.x,p.y,p.z)]:-data.格子边长*4;
            if(values.TryGetValue(p,out float value))return value;
            float sum=0;
            for(int z=-1;z<=1;z++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                sum+=取(data,cells,p+new Vector3Int(x,y,z))*(x==0?2:1)*(y==0?2:1)*(z==0?2:1);
            value=sum/64f-.5f;values.Add(p,value);return value;
        }
        public float Sample(Vector3 p)
        {
            var a=Vector3Int.FloorToInt(p);var t=p-(Vector3)a;float sum=0;
            for(int i=0;i<8;i++){var c=Corners[i];sum+=Sample(a+c)*(c.x==0?1-t.x:t.x)*(c.y==0?1-t.y:t.y)*(c.z==0?1-t.z:t.z);}return sum;
        }
    }

    public static Mesh 生成(VoxelVolumeAsset data,byte[] cells,Vector3Int chunk,float[] distances=null)
    {
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color32>();var indices=new List<int>();
        var cache=new Dictionary<Vector3Int,Vertex>();
        if(distances==null && ReferenceEquals(cells,data.实体) && data.距离!=null && data.距离.Length==cells.Length) distances=data.距离;
        var field=new Field(data,cells,distances);
        var start=chunk*data.分块边长;
        var end=Vector3Int.Min(start+Vector3Int.one*data.分块边长,data.尺寸);
        for(int z=start.z;z<end.z;z++)for(int y=start.y;y<end.y;y++)for(int x=start.x;x<end.x;x++)
        {
            var p=new Vector3Int(x,y,z);float value=field.Sample(p);
            for(int axis=0;axis<3;axis++)
            {
                var direction=axis==0?Vector3Int.right:axis==1?Vector3Int.up:new Vector3Int(0,0,1);
                if((value>0)==(field.Sample(p+direction)>0)) continue;
                Vector3Int a,b,c,d;
                if(axis==0) {a=p+new Vector3Int(0,-1,-1);b=p+new Vector3Int(0,0,-1);c=p;d=p+new Vector3Int(0,-1,0);}
                else if(axis==1) {a=p+new Vector3Int(-1,0,-1);b=p+new Vector3Int(-1,0,0);c=p;d=p+new Vector3Int(0,0,-1);}
                else {a=p+new Vector3Int(-1,-1,0);b=p+new Vector3Int(0,-1,0);c=p;d=p+new Vector3Int(-1,0,0);}
                var va=顶点(data,cells,a,cache,field);var vb=顶点(data,cells,b,cache,field);var vc=顶点(data,cells,c,cache,field);var vd=顶点(data,cells,d,cache,field);
                int index=vertices.Count;
                加(va,vertices,normals,colors);加(vb,vertices,normals,colors);加(vc,vertices,normals,colors);加(vd,vertices,normals,colors);
                if(value>0) {indices.Add(index);indices.Add(index+1);indices.Add(index+2);indices.Add(index);indices.Add(index+2);indices.Add(index+3);}
                else {indices.Add(index);indices.Add(index+2);indices.Add(index+1);indices.Add(index);indices.Add(index+3);indices.Add(index+2);}
            }
        }
        var mesh=new Mesh {name="VoxelSmooth_"+chunk,indexFormat=IndexFormat.UInt32};
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
    }
    static void 加(Vertex v,List<Vector3> p,List<Vector3> n,List<Color32> c) {p.Add(v.p);n.Add(v.n);c.Add(v.color);}
    static float 取(VoxelVolumeAsset data,byte[] cells,Vector3Int p) => data.范围内(p.x,p.y,p.z)&&cells[data.索引(p.x,p.y,p.z)]!=0?1:0;
    static Vertex 顶点(VoxelVolumeAsset data,byte[] cells,Vector3Int cell,Dictionary<Vector3Int,Vertex> cache,Field field)
    {
        if(cache.TryGetValue(cell,out var result))return result;
        Vector3 p=Vector3.zero;int crossings=0;Color color=Color.black;int count=0;
        for(int i=0;i<8;i++) if(取(data,cells,cell+Corners[i])>0) {var c=cell+Corners[i];color+=(Color)data.颜色[data.索引(c.x,c.y,c.z)];count++;}
        for(int i=0;i<Edges.Length;i+=2)
        {
            var a=cell+Corners[Edges[i]];var b=cell+Corners[Edges[i+1]];
            float da=field.Sample(a),db=field.Sample(b);
            if((da>0)==(db>0))continue;
            p+=Vector3.Lerp((Vector3)a,(Vector3)b,da/(da-db));crossings++;
        }
        p=crossings>0?p/crossings:(Vector3)cell+Vector3.one*.5f;
        const float epsilon=.4f;
        var gradient=new Vector3(
            field.Sample(p-Vector3.right*epsilon)-field.Sample(p+Vector3.right*epsilon),
            field.Sample(p-Vector3.up*epsilon)-field.Sample(p+Vector3.up*epsilon),
            field.Sample(p-Vector3.forward*epsilon)-field.Sample(p+Vector3.forward*epsilon));
        result=new Vertex {p=data.原点+(p+Vector3.one*.5f)*data.格子边长,n=gradient.sqrMagnitude>.000001f?gradient.normalized:Vector3.up,color=count>0?color/count:Color.gray};
        cache.Add(cell,result);return result;
    }
}
