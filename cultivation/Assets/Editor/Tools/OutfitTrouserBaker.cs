using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>移植用户分出的金丹裤装与双靴，修正跨腿权重并生成各境界独立配色；源模型不修改。</summary>
public static class OutfitTrouserBaker
{
    [Serializable] sealed class Selection { public int[] trouserDonorFaces,lowerBodyDonorFaces,leftShoeVertices,rightShoeVertices; }

    public static void Bake(AppearanceClothProfile target,SkinnedMeshRenderer reference,string outputFolder)
    {
        var donor=AppearanceDatabase.取().取("app_goldencore");
        var selection=JsonUtility.FromJson<Selection>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Art/Characters/OutfitCloth/Authoring/app_goldencore.json").text);
        if(selection?.trouserDonorFaces==null||selection.trouserDonorFaces.Length<50)throw new InvalidOperationException("缺少用户确认的金丹裤装分区");
        var source=donor.取模型().GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
        var mesh=donor.衣服随动.蒙皮;var sourceFaces=source.triangles;
        var selected=selection.lowerBodyDonorFaces??selection.trouserDonorFaces;
        var ids=selected.SelectMany(f=>sourceFaces.Skip(f*3).Take(3)).Distinct().ToArray();
        var remap=ids.Select((v,i)=>new{v,i}).ToDictionary(x=>x.v,x=>x.i);
        var allPoints=mesh.vertices;var allNormals=mesh.normals;var allUv=mesh.uv;var allWeights=mesh.boneWeights;
        var points=ids.Select(i=>allPoints[i]).ToList();var normals=ids.Select(i=>allNormals[i]).ToList();
        var uv=ids.Select(i=>allUv[i]).ToList();var weights=ids.Select(i=>allWeights[i]).ToList();
        var faces=selected.SelectMany(f=>sourceFaces.Skip(f*3).Take(3)).Select(i=>remap[i]).ToList();
        bool unified=selection.lowerBodyDonorFaces!=null;
        if(unified){
            // 几何岛沿裤裆以下的实际连接判定左右，不让内侧靠近中线的点同时跟两条腿。
            var raw=source.vertices;var parents=Enumerable.Range(0,ids.Length).ToArray();
            int Find(int i){while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
            var same=new Dictionary<Vector3Int,int>();
            for(int i=0;i<ids.Length;i++){var p=raw[ids[i]];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));if(same.TryGetValue(key,out int other))parents[Find(i)]=Find(other);else same.Add(key,i);}
            for(int f=0;f<faces.Count;f+=3)for(int e=0;e<3;e++){
                int a=faces[f+e],b=faces[f+(e+1)%3];if(raw[ids[a]].z<.34f&&raw[ids[b]].z<.34f)parents[Find(b)]=Find(a);
            }
            var groups=Enumerable.Range(0,ids.Length).Where(i=>raw[ids[i]].z<.34f).GroupBy(Find).ToDictionary(g=>g.Key,g=>g.Average(i=>raw[ids[i]].y));
            float mid=source.bindposes[1].inverse.m13;
            for(int i=0;i<ids.Length;i++){
                var p=raw[ids[i]];int side=(p.z<.34f?groups[Find(i)]:p.y)>mid?0:1;
                int thigh=side==0?3:12,calf=side==0?4:13,foot=side==0?5:15,toe=side==0?6:16;
                var old=weights[i];var map=new Dictionary<int,float>();
                void Add(int bone,float value){if(value<=0)return;if(bone==3||bone==12)bone=thigh;else if(bone==4||bone==13)bone=calf;else if(bone==5||bone==15)bone=foot;else if(bone==6||bone==16)bone=toe;if(p.z<.34f&&bone!=thigh&&bone!=calf&&bone!=foot&&bone!=toe)return;map[bone]=map.TryGetValue(bone,out float n)?n+value:value;}
                Add(old.boneIndex0,old.weight0);Add(old.boneIndex1,old.weight1);Add(old.boneIndex2,old.weight2);Add(old.boneIndex3,old.weight3);
                if(map.Count==0)map.Add(calf,1);
                var topWeights=map.OrderByDescending(x=>x.Value).Take(4).ToArray();float sum=topWeights.Sum(x=>x.Value);var w=new BoneWeight();
                for(int j=0;j<topWeights.Length;j++){int b=topWeights[j].Key;float v=topWeights[j].Value/sum;if(j==0){w.boneIndex0=b;w.weight0=v;}else if(j==1){w.boneIndex1=b;w.weight1=v;}else if(j==2){w.boneIndex2=b;w.weight2=v;}else{w.boneIndex3=b;w.weight3=v;}}
                weights[i]=w;
            }
        }
        if(weights.Any(w=>w.boneIndex0>=78||w.boneIndex1>=78||w.boneIndex2>=78||w.boneIndex3>=78))throw new InvalidOperationException("裤装不能携带衣料随动骨");
        // 按几何同点统计真正开口，UV缝不能误当缺面。金丹裤装只有八边腰口。
        string Key(int i){var p=points[i];return Mathf.RoundToInt(p.x*100000)+","+Mathf.RoundToInt(p.y*100000)+","+Mathf.RoundToInt(p.z*100000);}
        var edges=new Dictionary<string,(int a,int b,int count)>();
        for(int t=0;t<faces.Count;t+=3)for(int j=0;j<3;j++){
            int a=faces[t+j],b=faces[t+(j+1)%3];var x=Key(a);var y=Key(b);string key=string.CompareOrdinal(x,y)<0?x+";"+y:y+";"+x;
            if(edges.TryGetValue(key,out var edge))edges[key]=(edge.a,edge.b,edge.count+1);else edges.Add(key,(a,b,1));
        }
        var boundary=edges.Values.Where(e=>e.count==1).ToArray();
        if(boundary.Length!=8)throw new InvalidOperationException("金丹裤装腰口拓扑变化，需重新核对");
        float top=boundary.Max(e=>points[e.a].z);
        if(boundary.Any(e=>points[e.a].z<top-.04f))throw new InvalidOperationException("裤装存在非腰口的缺面");
        var upper=new Dictionary<string,int>();var centre=Vector3.zero;
        var liningUv=uv[Enumerable.Range(0,points.Count).OrderBy(i=>points[i].z).First()];
        foreach(var edge in boundary)if(!upper.ContainsKey(Key(edge.a))){
            int i=edge.a;var p=points[i];p.z=top+.04f;upper.Add(Key(i),points.Count);centre+=p;
            points.Add(p);normals.Add(normals[i]);uv.Add(liningUv+new Vector2(p.x,p.y)*.0001f);weights.Add(new BoneWeight{boneIndex0=21,weight0=1});
        }
        centre/=upper.Count;int cap=points.Count;points.Add(centre);normals.Add(Vector3.forward);uv.Add(liningUv+new Vector2(centre.x,centre.y)*.0001f);weights.Add(new BoneWeight{boneIndex0=21,weight0=1});
        foreach(var e in boundary){int a=upper[Key(e.a)],b=upper[Key(e.b)];faces.AddRange(new[]{e.b,e.a,a,e.b,a,b,b,a,cap});}
        var result=new Mesh{name=target.name+"-trousers"};result.SetVertices(points);result.SetNormals(normals);result.SetUVs(0,uv);result.boneWeights=weights.ToArray();result.bindposes=reference.sharedMesh.bindposes;result.SetTriangles(faces,0);result.RecalculateBounds();
        string path=outputFolder+"/"+target.name+"-trousers.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){
            // 顶点数量从裤装变为裤靴时，显式重建原资产数据，保持GUID并刷新蒙皮缓冲布局。
            existing.Clear(false);existing.indexFormat=result.indexFormat;existing.vertices=result.vertices;existing.normals=result.normals;existing.uv=result.uv;
            existing.bindposes=result.bindposes;existing.boneWeights=result.boneWeights;existing.subMeshCount=1;existing.SetTriangles(result.triangles,0);existing.bounds=result.bounds;existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(result);result=existing;
        }else AssetDatabase.CreateAsset(result,path);
        target.裤装蒙皮=result;target.裤装材质=unified?配色(target.name,donor.取模型().GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial,outputFolder):donor.取模型().GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial;
        var left=new HashSet<int>(selection.leftShoeVertices??new int[0]);var right=new HashSet<int>(selection.rightShoeVertices??new int[0]);
        target.裤靴面分组=Enumerable.Range(0,faces.Count/3).Select(f=>f>=selected.Length?0:sourceFaces.Skip(selected[f]*3).Take(3).All(left.Contains)?1:sourceFaces.Skip(selected[f]*3).Take(3).All(right.Contains)?2:0).ToArray();
        Debug.Log("[金丹裤靴移植] "+target.name+" 用户源面="+selected.Length+" 腰口补面="+(faces.Count/3-selected.Length)+" 顶点="+points.Count+" 统一裤靴="+unified);
    }

    static Material 配色(string id,Material source,string folder)
    {
        Color shadow,light;
        if(id=="app_nascent"){shadow=new Color(.055f,.10f,.077f);light=new Color(.30f,.40f,.29f);}
        else if(id=="app_divinity"){shadow=new Color(.12f,.17f,.15f);light=new Color(.54f,.57f,.46f);}
        else{shadow=new Color(.17f,.22f,.29f);light=new Color(.65f,.73f,.78f);}
        var input=source.mainTexture;var rt=RenderTexture.GetTemporary(input.width,input.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;
        var texture=new Texture2D(input.width,input.height,TextureFormat.RGBA32,false);
        try{Graphics.Blit(input,rt);RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,input.width,input.height),0,0);texture.Apply();}
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        var pixels=texture.GetPixels();for(int i=0;i<pixels.Length;i++){var p=pixels[i];float t=Mathf.Clamp01((p.r*.2126f+p.g*.7152f+p.b*.0722f)*1.35f);var c=Color.Lerp(shadow,light,Mathf.Sqrt(t));c.a=p.a;pixels[i]=c;}
        texture.SetPixels(pixels);texture.Apply();string texturePath=folder+"/"+id+"-lowerbody-color.png";
        File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
        var material=new Material(source){name=id+"-lowerbody"};material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);material.color=Color.white;
        material.DisableKeyword("_NORMALMAP");if(material.HasProperty("_BumpMap"))material.SetTexture("_BumpMap",null);if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.22f);
        string path=folder+"/"+id+"-lowerbody.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing){EditorUtility.CopySerialized(material,existing);UnityEngine.Object.DestroyImmediate(material);material=existing;}else AssetDatabase.CreateAsset(material,path);
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
    }
}
