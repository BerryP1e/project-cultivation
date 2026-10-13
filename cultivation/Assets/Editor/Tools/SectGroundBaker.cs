using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>宗门石台离线圆角化。同高度平台合并；保留原高度、贴图与台阶。</summary>
public static class SectGroundBaker
{
    const string Folder="Assets/Art/Environment/Sect";
    const float Step=.7f,Radius=1.6f,Bevel=1.2f,Blend=1.5f;
    sealed class Patch
    {
        public MeshFilter top,rock;
        public Vector3[] vertices,rockVertices;
        public Vector2[] uv,rockUv,inner;
        public int[] indices,rockIndices;
        public float height,bottom;
        public Bounds bounds;
        List<(Vector2 a,Vector2 b)> join;
        Vector2 joinCenter;
        public Vector3 WallPoint(Vector3 point,float t)
        {
            if(t<=0)return point;
            float level=height-1.25f,y=Mathf.Lerp(point.y,level,t);
            if(join==null){
                join=new List<(Vector2 a,Vector2 b)>();var cuts=new List<Vector2>();
                for(int i=0;i<rockIndices.Length;i+=3){
                    var segment=new List<Vector2>();
                    for(int j=0;j<3;j++){
                        var a=rockVertices[rockIndices[i+j]];var b=rockVertices[rockIndices[i+(j+1)%3]];
                        if((a.y<=level&&b.y>level)||(a.y>level&&b.y<=level)){
                            var p=Vector3.Lerp(a,b,(level-a.y)/(b.y-a.y));segment.Add(new Vector2(p.x,p.z));
                        }
                    }
                    if(segment.Count==2){join.Add((segment[0],segment[1]));cuts.AddRange(segment);}
                }
                var hull=Hull(cuts);
                if(hull.Length<3)throw new InvalidOperationException("岩壁接合截面不完整："+rock.transform.parent.name);
                joinCenter=hull.Aggregate(Vector2.zero,(a,b)=>a+b)/hull.Length;
            }
            var origin=new Vector2(point.x,point.z);
            var direction=(origin-joinCenter).normalized;float distance=float.MaxValue;
            foreach(var segment in join){
                var a=segment.a-joinCenter;var e=segment.b-segment.a;float denominator=Cross(direction,e);
                if(Mathf.Abs(denominator)<.00001f)continue;
                float r=Cross(a,e)/denominator,u=Cross(a,direction)/denominator;
                if(r>=0&&u>=0&&u<=1)distance=Mathf.Min(distance,r);
            }
            if(distance==float.MaxValue)throw new InvalidOperationException("岩壁接合截面无法投影："+rock.transform.parent.name);
            var target=joinCenter+direction*distance;
            // 固定接合平面，直接使用源三角形的截线；不随高度切换凸包，避免穿出原岩壁。
            float blend=Mathf.SmoothStep(0,1,t);var xz=Vector2.Lerp(origin,target,blend);
            return new Vector3(xz.x,y,xz.y);
        }
        public float Distance(Vector2 p)
        {
            float min=float.MaxValue;bool inside=true;
            for(int i=0;i<inner.Length;i++){
                var a=inner[i];var e=inner[(i+1)%inner.Length]-a;var v=p-a;
                min=Mathf.Min(min,(v-e*Mathf.Clamp01(Vector2.Dot(v,e)/e.sqrMagnitude)).sqrMagnitude);
                if(Cross(e,v)<0)inside=false;
            }
            return Mathf.Sqrt(min)*(inside?-1:1)-Radius;
        }
        public void Sample(Vector2 p,out float y,out Vector2 tex)
        {
            float best=float.MaxValue;y=height;tex=Vector2.zero;
            for(int i=0;i<indices.Length;i+=3){
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                var q=Closest(new Vector3(p.x,height,p.y),vertices[a],vertices[b],vertices[c],out var weights);
                float d=(new Vector2(q.x,q.z)-p).sqrMagnitude;
                if(d>=best)continue;best=d;y=q.y;tex=uv[a]*weights.x+uv[b]*weights.y+uv[c]*weights.z;
            }
        }
        public Vector2 RockUV(Vector3 p)
        {
            float best=float.MaxValue;Vector2 tex=Vector2.zero;
            for(int i=0;i<rockIndices.Length;i+=3){
                int a=rockIndices[i],b=rockIndices[i+1],c=rockIndices[i+2];
                var q=Closest(p,rockVertices[a],rockVertices[b],rockVertices[c],out var w);float d=(q-p).sqrMagnitude;
                if(d>=best)continue;best=d;tex=rockUv[a]*w.x+rockUv[b]*w.y+rockUv[c]*w.z;
            }
            return tex;
        }
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    // 最近三角形点及重心权重；UV 取自原模型，不替换宗门贴图。
    static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c,out Vector3 w)
    {
        var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
        if(d1<=0&&d2<=0){w=Vector3.right;return a;}
        var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
        if(d3>=0&&d4<=d3){w=Vector3.up;return b;}
        float vc=d1*d4-d3*d2;
        if(vc<=0&&d1>=0&&d3<=0){float t=d1/(d1-d3);w=new Vector3(1-t,t,0);return a+t*ab;}
        var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
        if(d6>=0&&d5<=d6){w=Vector3.forward;return c;}
        float vb=d5*d2-d1*d6;
        if(vb<=0&&d2>=0&&d6<=0){float t=d2/(d2-d6);w=new Vector3(1-t,0,t);return a+t*ac;}
        float va=d3*d6-d5*d4;
        if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0){float t=(d4-d3)/((d4-d3)+(d5-d6));w=new Vector3(0,1-t,t);return b+t*(c-b);}
        float inv=1/(va+vb+vc);w=new Vector3(1-(vb+vc)*inv,vb*inv,vc*inv);return a*w.x+b*w.y+c*w.z;
    }
    static Vector2[] Hull(IEnumerable<Vector2> points)
    {
        var sorted=points.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();if(sorted.Length<3)return sorted;
        var hull=new List<Vector2>();
        foreach(var p in sorted){while(hull.Count>=2&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        int lower=hull.Count;
        for(int i=sorted.Length-2;i>=0;i--){var p=sorted[i];while(hull.Count>lower&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}hull.RemoveAt(hull.Count-1);
        return hull.ToArray();
    }
    static Vector2[] Inner(IEnumerable<Vector3> vertices)
    {
        var hull=Hull(vertices.Select(v=>new Vector2(v.x,v.z)));var inner=new List<Vector2>(hull);
        for(int i=0;i<hull.Length;i++){
            var a=hull[i];var e=(hull[(i+1)%hull.Length]-a).normalized;var n=new Vector2(-e.y,e.x);var cut=new List<Vector2>();
            for(int j=0;j<inner.Count;j++){
                var p=inner[j];var q=inner[(j+1)%inner.Count];float dp=Vector2.Dot(p-a,n)-Radius,dq=Vector2.Dot(q-a,n)-Radius;
                if(dp>=0)cut.Add(p);if((dp<0&&dq>0)||(dp>0&&dq<0))cut.Add(Vector2.Lerp(p,q,dp/(dp-dq)));
            }inner=cut;
        }
        return inner.ToArray();
    }
    static Mesh Build(List<Patch> group,List<Patch> all)
    {
        float Distance(Vector2 p){float value=10000;foreach(var patch in group){float d=patch.Distance(p);float h=Mathf.Max(Blend-Mathf.Abs(value-d),0)/Blend;value=Mathf.Min(value,d)-h*h*Blend*.25f;}return value;}
        Patch Owner(Vector2 p)=>group.OrderBy(t=>t.Distance(p)).First();
        var bounds=group[0].bounds;foreach(var p in group)bounds.Encapsulate(p.bounds);bounds.Expand(4);
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var top=new List<int>();var rock=new List<int>();var shared=new Dictionary<Vector2Int,int>();
        int Vertex(Vector2 p){var key=new Vector2Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000));if(shared.TryGetValue(key,out int k))return k;
            var owner=Owner(p);owner.Sample(p,out float y,out var tex);float edge=Distance(p);
            // 相邻的高低石台覆盖处不做向下收边，保留原有台阶落脚面。
            bool covered=all.Any(q=>!group.Contains(q)&&q.Distance(p)<-.15f&&Mathf.Abs(q.height-y)<3.5f);
            float t=covered?0:Mathf.Clamp01((edge+Bevel)/Bevel);y-=.45f*t*t*(3-2*t);
            shared[key]=vertices.Count;vertices.Add(new Vector3(p.x,y,p.y));uv.Add(tex);return vertices.Count-1;}
        for(int x=Mathf.FloorToInt(bounds.min.x/Step);x<Mathf.CeilToInt(bounds.max.x/Step);x++)for(int z=Mathf.FloorToInt(bounds.min.z/Step);z<Mathf.CeilToInt(bounds.max.z/Step);z++){
            Vector2[] corners={new Vector2(x*Step,z*Step),new Vector2(x*Step,(z+1)*Step),new Vector2((x+1)*Step,(z+1)*Step),new Vector2((x+1)*Step,z*Step)};
            foreach(var ids in new[]{new[]{0,1,2},new[]{0,2,3}}){var polygon=new List<Vector2>();
                for(int i=0;i<3;i++){var a=corners[ids[i]];var b=corners[ids[(i+1)%3]];float da=Distance(a),db=Distance(b);if(da<=0)polygon.Add(a);if((da<0&&db>0)||(da>0&&db<0))polygon.Add(Vector2.Lerp(a,b,da/(da-db)));}
                for(int i=1;i<polygon.Count-1;i++){int a=Vertex(polygon[0]),b=Vertex(polygon[i]),c=Vertex(polygon[i+1]);if(a!=b&&b!=c&&c!=a)top.AddRange(new[]{a,b,c});}
            }
        }
        var edges=new Dictionary<ulong,Vector2Int>();var counts=new Dictionary<ulong,int>();
        for(int i=0;i<top.Count;i+=3)for(int j=0;j<3;j++){int a=top[i+j],b=top[i+(j+1)%3];ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);if(counts.ContainsKey(key))counts[key]++;else{counts[key]=1;edges[key]=new Vector2Int(a,b);}}
        foreach(var item in edges){if(counts[item.Key]!=1)continue;var a=vertices[item.Value.x];var b=vertices[item.Value.y];var mid=(a+b)*.5f;var p=new Vector2(mid.x,mid.z);var owner=Owner(p);
            // 同高度平台已经合并，跨高度重叠的内壁也不重复绘制。
            if(all.Any(q=>!group.Contains(q)&&q.Distance(p)<-.15f&&q.height>mid.y-.15f))continue;
            int offset=vertices.Count;
            const int rows=6;for(int r=0;r<=rows;r++)foreach(var v in new[]{a,b}){
                float t=(float)r/rows;var pos=owner.WallPoint(v,t);
                vertices.Add(pos);uv.Add(owner.RockUV(pos));
            }
            for(int r=0;r<rows;r++){int k=offset+r*2;rock.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
        }
        foreach(var patch in group){
            float cap=patch.height-1.25f;
            for(int i=0;i<patch.rockIndices.Length;i+=3){
                var polygon=new List<(Vector3 p,Vector2 uv)>();
                for(int j=0;j<3;j++){
                    int a=patch.rockIndices[i+j],b=patch.rockIndices[i+(j+1)%3];var p=patch.rockVertices[a];var q=patch.rockVertices[b];float dp=p.y-cap,dq=q.y-cap;
                    if(dp<=0)polygon.Add((p,patch.rockUv[a]));
                    if((dp<0&&dq>0)||(dp>0&&dq<0)){float t=dp/(dp-dq);polygon.Add((Vector3.Lerp(p,q,t),Vector2.Lerp(patch.rockUv[a],patch.rockUv[b],t)));}
                }
                int offset=vertices.Count;foreach(var point in polygon){vertices.Add(point.p);uv.Add(point.uv);}
                for(int j=1;j<polygon.Count-1;j++)rock.AddRange(new[]{offset,offset+j,offset+j+1});
            }
        }
        var mesh=new Mesh{name="SectRoundedPlatform",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=2;mesh.SetTriangles(top,0);mesh.SetTriangles(rock,1);mesh.RecalculateNormals();
        var normals=mesh.normals;var smooth=new Dictionary<Vector3Int,Vector3>();
        Vector3Int Key(Vector3 v)=>new Vector3Int(Mathf.RoundToInt(v.x*1000),Mathf.RoundToInt(v.y*1000),Mathf.RoundToInt(v.z*1000));
        for(int i=0;i<vertices.Count;i++){var k=Key(vertices[i]);smooth.TryGetValue(k,out var n);smooth[k]=n+normals[i];}
        for(int i=0;i<vertices.Count;i++)normals[i]=smooth[Key(vertices[i])].normalized;mesh.normals=normals;mesh.RecalculateBounds();return mesh;
    }
    public static int 烘焙(bool 只更新网格=false)
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.name!="Sect")throw new InvalidOperationException("请在非运行的 Sect 场景执行");
        var filters=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh&&f.sharedMesh.name.Contains("dabingshi")).ToArray();
        var patches=new List<Patch>();
        foreach(var cluster in filters.GroupBy(f=>f.transform.parent)){
            var pair=cluster.OrderBy(f=>f.GetComponent<Renderer>().bounds.size.y).ToArray();if(pair.Length!=2)throw new InvalidOperationException("石台需要独立顶面与岩壁："+cluster.Key.name);
            var t=pair[0];var r=pair[1];var v=t.sharedMesh.vertices.Select(t.transform.TransformPoint).ToArray();var rv=r.sharedMesh.vertices.Select(r.transform.TransformPoint).ToArray();
            patches.Add(new Patch{top=t,rock=r,vertices=v,rockVertices=rv,uv=t.sharedMesh.uv,rockUv=r.sharedMesh.uv,indices=t.sharedMesh.triangles,rockIndices=r.sharedMesh.triangles,inner=Inner(v),height=t.GetComponent<Renderer>().bounds.center.y,bottom=rv.Min(p=>p.y),bounds=t.GetComponent<Renderer>().bounds});
        }
        if(patches.Count==0)throw new InvalidOperationException("没有源石台");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art/Environment","Sect");
        var old=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="宗门圆角地面");
        if(只更新网格&&!old)throw new InvalidOperationException("没有既有圆角地面，请先完整烘焙");
        GameObject root=null;
        if(!只更新网格){if(old)Undo.DestroyObjectImmediate(old.gameObject);root=new GameObject("宗门圆角地面");Undo.RegisterCreatedObjectUndo(root,"柔化宗门石台");var level=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Level");if(level)root.transform.SetParent(level.transform,true);}
        var groups=new List<List<Patch>>();foreach(var patch in patches.OrderBy(p=>p.height)){var group=groups.FirstOrDefault(g=>Mathf.Abs(g[0].height-patch.height)<1.25f);if(group==null){group=new List<Patch>();groups.Add(group);}group.Add(patch);}
        if(只更新网格)for(int i=0;i<groups.Count;i++){
            var node=old.Find("石台高度组_"+i);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Platform_"+i+".asset");
            if(!node||!saved||node.GetComponent<MeshFilter>().sharedMesh!=saved||node.GetComponent<MeshCollider>().sharedMesh!=saved)throw new InvalidOperationException("既有圆角地面引用不一致："+i);
        }
        for(int i=0;i<groups.Count;i++){
            var mesh=Build(groups[i],patches);mesh.name="Platform_"+i;string path=Folder+"/Platform_"+i+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){saved.Clear();saved.indexFormat=mesh.indexFormat;saved.name=mesh.name;saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.uv=mesh.uv;saved.subMeshCount=2;saved.SetTriangles(mesh.GetTriangles(0),0);saved.SetTriangles(mesh.GetTriangles(1),1);saved.bounds=mesh.bounds;saved.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
            if(只更新网格){var collider=old.Find("石台高度组_"+i).GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;continue;}
            var go=new GameObject("石台高度组_"+i);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{groups[i][0].top.GetComponent<Renderer>().sharedMaterial,groups[i][0].rock.GetComponent<Renderer>().sharedMaterial};go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        if(只更新网格)return groups.Count;
        foreach(var p in patches)foreach(var f in new[]{p.top,p.rock}){var renderer=f.GetComponent<Renderer>();Undo.RecordObject(renderer,"替换石台显示");renderer.enabled=false;foreach(var c in f.GetComponents<Collider>()){Undo.RecordObject(c,"替换石台碰撞");c.enabled=false;}}
        EditorSceneManager.MarkSceneDirty(scene);return groups.Count;
    }
}
