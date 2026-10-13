using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>石台轮廓的二维圆角并集；生成一份草地、岩壁、底面，内部不保留重复面。</summary>
public static class HomeLandMesh
{
    public const float 步长 = .75f;
    public const float 收边宽度 = 2f;
    public const float 行走边距 = 2.7f;
    public const float 底高 = -4f;
    public struct Segment { public Vector2 a, b; public Segment(Vector2 x,Vector2 y){a=x;b=y;} }
    static float PolygonDistance(Vector2 p,Vector2[] points)
    {
        float squared=float.MaxValue;bool inside=true;
        for(int i=0;i<points.Length;i++)
        {
            Vector2 a=points[i],e=points[(i+1)%points.Length]-a,v=p-a;
            Vector2 nearest=v-e*Mathf.Clamp01(Vector2.Dot(v,e)/e.sqrMagnitude);
            squared=Mathf.Min(squared,nearest.sqrMagnitude);
            if(e.x*v.y-e.y*v.x<0)inside=false;
        }
        return Mathf.Sqrt(squared)*(inside?-1:1);
    }
    public static float 距离(HomeAssets assets,HashSet<Vector2Int> cells,Vector2 p)
    {
        float result=10000;
        var center=new Vector2Int(Mathf.RoundToInt(p.x/HomeWorld.网格间距),Mathf.RoundToInt(p.y/HomeWorld.网格间距));
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
        {
            var cell=center+new Vector2Int(x,y);if(!cells.Contains(cell))continue;
            float d=PolygonDistance(p-new Vector2(cell.x,cell.y)*HomeWorld.网格间距,assets.地面内轮廓)-assets.地面圆角;
            // 圆角并集只改相交邻域，连接处不会留下各自的坡边。
            const float blend=8f;
            float h=Mathf.Max(blend-Mathf.Abs(result-d),0)/blend;
            result=Mathf.Min(result,d)-h*h*blend*.25f;
        }
        return result;
    }
    static HashSet<Vector2Int> Raster(HashSet<Vector2Int> cells)
    {
        var result=new HashSet<Vector2Int>();
        foreach(var cell in cells)
        {
            Vector2 p=new Vector2(cell.x,cell.y)*HomeWorld.网格间距;
            int minX=Mathf.FloorToInt((p.x-24)/步长),maxX=Mathf.CeilToInt((p.x+24)/步长);
            int minY=Mathf.FloorToInt((p.y-24)/步长),maxY=Mathf.CeilToInt((p.y+24)/步长);
            for(int x=minX;x<maxX;x++)for(int y=minY;y<maxY;y++)result.Add(new Vector2Int(x,y));
        }
        return result;
    }
    static List<Vector2> Clip(Vector2[] triangle,float[] d)
    {
        var polygon=new List<Vector2>(4);
        for(int i=0;i<3;i++)
        {
            int j=(i+1)%3;
            if(d[i]<=0)polygon.Add(triangle[i]);
            if((d[i]<0&&d[j]>0)||(d[i]>0&&d[j]<0))polygon.Add(Vector2.Lerp(triangle[i],triangle[j],d[i]/(d[i]-d[j])));
        }
        return polygon;
    }
    public static Mesh 生成(HomeAssets assets,IEnumerable<Vector2Int> source,out List<Segment> contour,float level=0)
    {
        var cells=new HashSet<Vector2Int>(source);contour=new List<Segment>();
        var values=new Dictionary<Vector2Int,float>();
        float Sample(Vector2Int c){if(!values.TryGetValue(c,out var d))values[c]=d=距离(assets,cells,new Vector2(c.x,c.y)*步长)-level;return d;}
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var triangles=new List<int>();
        var shared=new Dictionary<Vector2Int,int>();
        int Vertex(Vector2 p)
        {
            var key=new Vector2Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000));
            if(shared.TryGetValue(key,out int existing))return existing;
            float d=距离(assets,cells,p),t=Mathf.Clamp01((d+收边宽度)/收边宽度);
            float y=-1.3f*(1-Mathf.Cos(t*Mathf.PI*.5f));
            const float eps=.02f;
            Vector2 gradient=new Vector2(距离(assets,cells,p+Vector2.right*eps)-距离(assets,cells,p-Vector2.right*eps),距离(assets,cells,p+Vector2.up*eps)-距离(assets,cells,p-Vector2.up*eps))/(2*eps);
            float slope=-1.3f*Mathf.PI*.5f*Mathf.Sin(t*Mathf.PI*.5f)/收边宽度;
            shared[key]=vertices.Count;vertices.Add(new Vector3(p.x,y,p.y));normals.Add(new Vector3(-slope*gradient.x,1,-slope*gradient.y).normalized);colors.Add(new Color(t*t,0,0,1));uv.Add(p/7f);return vertices.Count-1;
        }
        foreach(var c in Raster(cells))
        {
            Vector2Int[] corners={c,c+Vector2Int.up,c+Vector2Int.one,c+Vector2Int.right};
            foreach(var indices in new[]{new[]{0,1,2},new[]{0,2,3}})
            {
                var p=new Vector2[3];var d=new float[3];for(int i=0;i<3;i++){p[i]=new Vector2(corners[indices[i]].x,corners[indices[i]].y)*步长;d[i]=Sample(corners[indices[i]]);}
                var polygon=Clip(p,d);if(polygon.Count<3)continue;
                if(level!=0)
                {
                    var cut=new List<Vector2>();
                    for(int i=0;i<3;i++){int j=(i+1)%3;if((d[i]<=0&&d[j]>0)||(d[i]>0&&d[j]<=0))cut.Add(Vector2.Lerp(p[i],p[j],d[i]/(d[i]-d[j])));}
                    if(cut.Count==2&&(cut[0]-cut[1]).sqrMagnitude>.000001f)contour.Add(new Segment(cut[0],cut[1]));
                    continue;
                }
                for(int i=1;i<polygon.Count-1;i++){int a=Vertex(polygon[0]),b=Vertex(polygon[i]),z=Vertex(polygon[i+1]);if(a==b||b==z||a==z)continue;triangles.AddRange(new[]{a,b,z});}
            }
        }
        if(level!=0)return null;
        // 从最终面索引抽取外边，排除所有内部公共边；凹形、环形扩建同样适用。
        var edges=new Dictionary<ulong,Vector2Int>();var counts=new Dictionary<ulong,int>();
        for(int i=0;i<triangles.Count;i+=3)for(int j=0;j<3;j++)
        {
            int a=triangles[i+j],b=triangles[i+(j+1)%3];ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
            if(counts.ContainsKey(key))counts[key]++;else {counts[key]=1;edges[key]=new Vector2Int(a,b);}
        }
        int topCount=vertices.Count,topIndexCount=triangles.Count;
        for(int i=0;i<topCount;i++){var p=vertices[i];vertices.Add(new Vector3(p.x,底高,p.z));normals.Add(Vector3.down);colors.Add(Color.red);uv.Add(uv[i]);}
        for(int i=0;i<topIndexCount;i+=3)triangles.AddRange(new[]{triangles[i+2]+topCount,triangles[i+1]+topCount,triangles[i]+topCount});
        foreach(var edge in edges)
        {
            if(counts[edge.Key]!=1)continue;
            var a=vertices[edge.Value.x];var b=vertices[edge.Value.y];
            contour.Add(new Segment(new Vector2(a.x,a.z),new Vector2(b.x,b.z)));
            var direction=(b-a).normalized;var outward=new Vector3(-direction.z,0,direction.x);
            int offset=vertices.Count;
            for(int ring=0;ring<4;ring++)foreach(var p in new[]{a,b})
            {
                Vector2 point=new Vector2(p.x,p.z);const float epsilon=.02f;
                var radial=new Vector3(距离(assets,cells,point+Vector2.right*epsilon)-距离(assets,cells,point-Vector2.right*epsilon),0,距离(assets,cells,point+Vector2.up*epsilon)-距离(assets,cells,point-Vector2.up*epsilon)).normalized;
                if(radial.sqrMagnitude<.5f)radial=outward;
                float t=ring/3f;var v=p+radial*(Mathf.Sin(t*Mathf.PI)*.20f);v.y=Mathf.Lerp(p.y,底高,t);
                vertices.Add(v);normals.Add((radial+Vector3.up*(ring==0?.98f:ring==3?-.3f:0)).normalized);colors.Add(Color.red);uv.Add(new Vector2(v.x,v.z)/7f);
            }
            for(int r=0;r<3;r++){int k=offset+r*2;triangles.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
        }
        var mesh=new Mesh{name="连续家园地面",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
    public static void 建空气墙(Transform parent,HomeAssets assets,IEnumerable<Vector2Int> cells)
    {
        foreach(var edge in 边界轮廓(assets,cells))
        {
            Vector2 delta=edge.b-edge.a;if(delta.magnitude<.08f)continue;
            var center=(edge.a+edge.b)*.5f;var go=new GameObject("家园边界");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(center.x,4,center.y);
            go.transform.localRotation=Quaternion.LookRotation(new Vector3(-delta.y,0,delta.x));go.AddComponent<BoxCollider>().size=new Vector3(delta.magnitude+.04f,10,.25f);
        }
    }
    public static List<Segment> 边界轮廓(HomeAssets assets,IEnumerable<Vector2Int> cells)
    {
        生成(assets,cells,out var contour,-行走边距);
        return 简化边界(contour);
    }
    static List<Segment> 简化边界(List<Segment> source)
    {
        // 沿闭合轮廓连接采样段，将几乎共线的边合并；不逐个体素放碰撞体。
        Vector2Int Key(Vector2 p)=>new Vector2Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000));
        var links=new Dictionary<Vector2Int,List<int>>();
        for(int i=0;i<source.Count;i++)foreach(var p in new[]{source[i].a,source[i].b}){var k=Key(p);if(!links.ContainsKey(k))links[k]=new List<int>();links[k].Add(i);}
        var used=new HashSet<int>();var output=new List<Segment>();
        void Simplify(List<Vector2> points,int first,int last,List<Vector2> result)
        {
            var a=points[first];var e=points[last]-a;float max=0;int pivot=first;
            for(int i=first+1;i<last;i++){var v=points[i]-a;float distance=(v-e*Mathf.Clamp01(Vector2.Dot(v,e)/Mathf.Max(e.sqrMagnitude,.000001f))).magnitude;if(distance>max){max=distance;pivot=i;}}
            if(max>.06f){Simplify(points,first,pivot,result);Simplify(points,pivot,last,result);}else result.Add(points[last]);
        }
        for(int i=0;i<source.Count;i++)
        {
            if(used.Contains(i))continue;used.Add(i);var points=new List<Vector2>{source[i].a,source[i].b};
            while(links.TryGetValue(Key(points[points.Count-1]),out var candidates))
            {
                int next=-1;foreach(int n in candidates)if(!used.Contains(n)){next=n;break;}if(next<0)break;used.Add(next);
                var edge=source[next];points.Add(Key(edge.a)==Key(points[points.Count-1])?edge.b:edge.a);
            }
            if(points.Count<3)continue;
            int middle=points.Count/2;var simplified=new List<Vector2>{points[0]};Simplify(points,0,middle,simplified);Simplify(points,middle,points.Count-1,simplified);
            for(int n=0;n<simplified.Count-1;n++)output.Add(new Segment(simplified[n],simplified[n+1]));
        }
        return output;
    }
}
