using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>拆分外袍与人体蒙皮，生成分段衣摆、长袖和长发骨链；不修改源 FBX 或动作。</summary>
public static class OutfitClothBaker
{
    const string Folder = "Assets/Art/Characters/OutfitCloth";
    const int Sectors = 8, Segments = 3;
    [Serializable] sealed class AuthoredMap { public AuthoredVertex[] vertices; public AuthoredFace[] removedFaces; public int hemRootBone; public float hairChainBottom,closedLegWidth,closedLegBlend; public bool poseSpace, addInnerLegs, explicitHair, detachBootHem, thinLegs, shoesOnly,useGoldenTrousers,fullSleeves; public int[] leftShoeVertices,rightShoeVertices,innerSkirtFaces; }
    [Serializable] sealed class PartMap { public string sourceFbxSha256; public int sourceVertexCount; public int[] faceRegions; }
    [Serializable] sealed class AuthoredFace { public Vector3 a, b, c; }
    [Serializable] sealed class AuthoredVertex
    {
        public float x, y, z, weight0, weight1, weight2, weight3;
        public int region, bone0, bone1, bone2, bone3;
        public bool preserveOriginalWeights, preserveAuthoredWeights;
    }

    public static void Bake(AppearanceDefinition appearance, SkinnedMeshRenderer reference, string authoredJson = null, string outputFolder = null)
    {
        outputFolder = outputFolder ?? Folder;
        var source = appearance.取模型().GetComponentInChildren<SkinnedMeshRenderer>(true);
        var original = source.sharedMesh;
        if (source.bones.Length != 78 || reference.bones.Length != 78)
            throw new InvalidOperationException("当前衣服预处理只接受已确认的 78 骨人体骨架");
        for (int i = 0; i < 78; i++)
            if (source.bones[i].name != reference.bones[i].name) throw new InvalidOperationException("人体骨序不同");
        var points = original.vertices;
        var weights = original.boneWeights;
        var unchangedWeights=(BoneWeight[])weights.Clone();var unchangedVertices=new HashSet<int>();
        var authoredLocks=new Dictionary<int,BoneWeight>();
        var authoredRegions = new int[points.Length];
        int hemParent=1;
        bool poseSpace=false;
        bool addInnerLegs=appearance.id=="app_foundation",explicitHair=false,detachBootHem=false;float hairChainBottom=0;
        AuthoredMap manual=null;
        var authoredAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/Authoring/" + appearance.id + ".json");
        var removedFaces = new HashSet<string>();
        string PointKey(Vector3 p) => Mathf.RoundToInt(p.x*100000)+","+Mathf.RoundToInt(p.y*100000)+","+Mathf.RoundToInt(p.z*100000);
        string FaceKey(Vector3 a,Vector3 b,Vector3 c) => string.Join(";",new[]{PointKey(a),PointKey(b),PointKey(c)}.OrderBy(k=>k,StringComparer.Ordinal));
        if (authoredJson != null || authoredAsset)
        {
            var authored = JsonUtility.FromJson<AuthoredMap>(authoredJson ?? authoredAsset.text);
            manual=authored;
            poseSpace=authored.poseSpace;
            addInnerLegs|=authored.addInnerLegs;explicitHair=authored.explicitHair;detachBootHem=authored.detachBootHem;hairChainBottom=authored.hairChainBottom;
            if(authored.hemRootBone!=0){
                if(authored.hemRootBone!=1&&authored.hemRootBone!=21)throw new InvalidOperationException("衣摆根仅支持已核对的髋/腰骨");
                hemParent=authored.hemRootBone;
            }
            if(authored.removedFaces!=null)foreach(var face in authored.removedFaces)
                if(!removedFaces.Add(FaceKey(face.a,face.b,face.c)))throw new InvalidOperationException("人工剔除面重复");
            var matched = new HashSet<int>();
            for (int i = 0; i < points.Length; i++)
                for (int j = 0; j < authored.vertices.Length; j++)
                {
                    var vertex = authored.vertices[j];
                    if ((points[i] - new Vector3(vertex.x, vertex.y, vertex.z)).sqrMagnitude > 1e-10f) continue;
                    if (vertex.bone0 < 0 || vertex.bone0 >= 78 || vertex.bone1 < 0 || vertex.bone1 >= 78
                        || vertex.bone2 < 0 || vertex.bone2 >= 78 || vertex.bone3 < 0 || vertex.bone3 >= 78
                        || vertex.weight0 < 0 || vertex.weight1 < 0 || vertex.weight2 < 0 || vertex.weight3 < 0
                        || Mathf.Abs(vertex.weight0 + vertex.weight1 + vertex.weight2 + vertex.weight3 - 1) > .0001f)
                        throw new InvalidOperationException("人工权重无效：" + appearance.id);
                    weights[i] = new BoneWeight { boneIndex0 = vertex.bone0, weight0 = vertex.weight0,
                        boneIndex1 = vertex.bone1, weight1 = vertex.weight1,
                        boneIndex2 = vertex.bone2, weight2 = vertex.weight2,
                        boneIndex3 = vertex.bone3, weight3 = vertex.weight3 };
                    if(vertex.preserveOriginalWeights){weights[i]=unchangedWeights[i];unchangedVertices.Add(i);}
                    if(vertex.preserveAuthoredWeights)authoredLocks[i]=weights[i];
                    authoredRegions[i] = vertex.region; matched.Add(j); break;
                }
            if (matched.Count != authored.vertices.Length)
                throw new InvalidOperationException("人工选区与源网格不匹配，停止生成：" + appearance.id);
        }
        var originalBinds = original.bindposes;
        if(explicitHair)平滑分区绑定(points,weights,original.triangles,originalBinds);
        foreach(int i in unchangedVertices)weights[i]=unchangedWeights[i];
        foreach(var entry in authoredLocks)weights[entry.Key]=entry.Value;
        var correction=Enumerable.Range(0,78).Select(i=>reference.sharedMesh.bindposes[i].inverse*originalBinds[i]).ToArray();
        if(explicitHair)AppearanceMeshBinding.应用全身解剖坐标(correction,originalBinds,reference.sharedMesh.bindposes);
        Vector3 Rest(int index) => originalBinds[index].inverse.GetColumn(3);
        var centre = Rest(1);
        float waist = Mathf.Min(.49f, Rest(2).z - .025f);
        var specs = new List<AppearanceClothProfile.Bone>();
        var rests = new List<Matrix4x4>();
        var binds = reference.sharedMesh.bindposes.ToList();
        void Chain(string name, int parent, Vector3 anchor, float bottom, bool sleeve, bool hair = false)
        {
            float length = Mathf.Max(.06f, anchor.z - bottom) / Segments;
            for (int j = 0; j < Segments; j++)
            {
                var rawRest = Matrix4x4.TRS(anchor + Vector3.back * (length * j), Quaternion.identity, Vector3.one);
                var rest = correction[parent] * rawRest;
                int previous = j == 0 ? -1 : specs.Count - 1;
                var local = (previous < 0 ? reference.sharedMesh.bindposes[parent] : rests[previous].inverse) * rest;
                specs.Add(new AppearanceClothProfile.Bone {
                    名称 = name + "_" + j, 人体骨索引 = parent, 上级衣骨 = previous,
                    局部位置 = local.GetColumn(3), 局部旋转 = local.rotation,
                    末端偏移 = Vector3.back * length, 衣袖 = sleeve, 头发 = hair
                });
                rests.Add(rest); binds.Add(rest.inverse);
            }
        }
        for(int sector=0;sector<Sectors;sector++){
            float angle=sector*Mathf.PI*2/Sectors;
            Chain("Hem_"+sector,hemParent,new Vector3(centre.x+Mathf.Cos(angle)*.075f,centre.y+Mathf.Sin(angle)*.075f,waist),.065f,false);
        }
        bool wideSleeves = appearance.id != "app_player";
        if(wideSleeves)for(int side=0;side<2;side++){
            int fore=side==0?32:56,hand=side==0?36:60;var anchor=Vector3.Lerp(Rest(fore),Rest(hand),.65f);
            Chain(side==0?"Sleeve_L":"Sleeve_R",fore,anchor,Mathf.Max(.08f,anchor.z-.28f),true);
        }
        if(wideSleeves&&manual!=null&&manual.fullSleeves)for(int side=0;side<2;side++){
            int upper=side==0?31:55,fore=side==0?32:56;var anchor=Vector3.Lerp(Rest(upper),Rest(fore),.55f);
            Chain(side==0?"Sleeve_Upper_L":"Sleeve_Upper_R",upper,anchor,Mathf.Max(.08f,anchor.z-.28f),true);
        }
        float Fade(float a, float b, float v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v));
        var originalWeights = (BoneWeight[])weights.Clone();
        var parents=Enumerable.Range(0,points.Length).ToArray();
        int Find(int x){while(parents[x]!=x){parents[x]=parents[parents[x]];x=parents[x];}return x;}
        void Join(int a,int b){parents[Find(b)]=Find(a);}
        var coincident=new Dictionary<Vector3Int,int>();
        for(int i=0;i<points.Length;i++){
            var p=points[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
            if(coincident.TryGetValue(key,out int other))Join(i,other);else coincident.Add(key,i);
        }
        var triangles=original.triangles;
        int removedCount=0;
        for(int i=0;i<triangles.Length;i+=3)if(removedFaces.Contains(FaceKey(points[triangles[i]],points[triangles[i+1]],points[triangles[i+2]])))removedCount++;
        if(removedCount!=removedFaces.Count)throw new InvalidOperationException("人工剔除面与源拓扑不匹配，停止生成");
        int[] faceRegions=null;var pinnedSeams=new HashSet<string>();
        if(poseSpace){
            var partsAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/Authoring/"+appearance.id+"-parts.json");
            if(!partsAsset)throw new InvalidOperationException("姿态空间着装缺少明确的本体/衣料分面："+appearance.id);
            var parts=JsonUtility.FromJson<PartMap>(partsAsset.text);
            if(parts.sourceVertexCount!=points.Length||parts.faceRegions==null||parts.faceRegions.Length*3!=triangles.Length||parts.faceRegions.Any(r=>r<0||r>7))throw new InvalidOperationException("人工分面与源网格数量不匹配");
            using(var hash=System.Security.Cryptography.SHA256.Create()){
                var actual=BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(original)))).Replace("-","").ToLowerInvariant();
                if(actual!=parts.sourceFbxSha256)throw new InvalidOperationException("人工分面源 FBX 指纹变化，停止生成");
            }
            faceRegions=parts.faceRegions;var skinKeys=new HashSet<string>();var clothKeys=new HashSet<string>();
            if(explicitHair){
                // 分面是最终编辑结果；旧的逐点草稿标签不能覆盖已经修订的衣料选区。
                var votes=new int[points.Length,4];
                for(int f=0;f<faceRegions.Length;f++){
                    int role=faceRegions[f];if(role<1||role>3)continue;
                    for(int j=0;j<3;j++)votes[triangles[f*3+j],role]++;
                }
                int corrected=0;
                foreach(var group in Enumerable.Range(0,points.Length).GroupBy(i=>PointKey(points[i]))){
                    var totals=new int[4];foreach(int i in group)for(int r=1;r<=3;r++)totals[r]+=votes[i,r];
                    int role=totals[2]>totals[1]&&totals[2]>=totals[3]?2:totals[3]>totals[1]?3:totals[1]>0?1:0;
                    if(role==0)continue;
                    foreach(int i in group)if(!unchangedVertices.Contains(i)){if(authoredRegions[i]!=role)corrected++;authoredRegions[i]=role;}
                }
                Debug.Log("[衣料分面绑定] "+appearance.id+" 同步旧顶点标签="+corrected);
            }
            for(int f=0;f<faceRegions.Length;f++){
                int role=faceRegions[f];if(role==7)continue;
                var keys=role==0||role==6?skinKeys:clothKeys;
                for(int j=0;j<3;j++)keys.Add(PointKey(points[triangles[f*3+j]]));
            }
            pinnedSeams.UnionWith(skinKeys);pinnedSeams.IntersectWith(clothKeys);
            // 源模型偶有袍摆与鞋沿焊在一起；这是接触边界，不是衣服缝线，不能随脚骨牵拉。
            if(detachBootHem){
                var lowPoints=new HashSet<string>();for(int i=0;i<points.Length;i++)if(points[i].z<waist-.015f)lowPoints.Add(PointKey(points[i]));
                pinnedSeams.ExceptWith(lowPoints);
            }
        }
        // 未有人工选区的源面暂保留；已在 Blender 核对的长袍饰边走明确衣身分区。
        var shoulderPanels=new HashSet<int>();
        if(appearance.id=="app_goldencore"){
            var fp=Enumerable.Range(0,triangles.Length/3).ToArray();int Face(int f){while(fp[f]!=f){fp[f]=fp[fp[f]];f=fp[f];}return f;}
            var edges=new Dictionary<ulong,int>();
            for(int f=0;f<fp.Length;f++)for(int e=0;e<3;e++){
                int a=Find(triangles[f*3+e]),b=Find(triangles[f*3+(e+1)%3]);ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                if(edges.TryGetValue(key,out int other))fp[Face(f)]=Face(other);else edges.Add(key,f);
            }
            foreach(var group in Enumerable.Range(0,fp.Length).GroupBy(Face)){
                var indices=group.SelectMany(f=>new[]{triangles[f*3],triangles[f*3+1],triangles[f*3+2]}).Distinct().ToArray();var box=new Bounds(points[indices[0]],Vector3.zero);foreach(int i in indices)box.Encapsulate(points[i]);
                if(indices.Length<100&&box.size.y>.22f&&box.size.x<.21f&&box.max.z>.5f&&box.min.z<.15f)foreach(int i in indices)shoulderPanels.Add(i);
            }
        }
        for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
        var islands=new Dictionary<int,Bounds>();
        for(int i=0;i<points.Length;i++){int group=Find(i);if(islands.TryGetValue(group,out var b)){b.Encapsulate(points[i]);islands[group]=b;}else islands[group]=new Bounds(points[i],Vector3.zero);}
        var protectedIslands=new HashSet<int>();
        var protectedVertices=new HashSet<int>();
        protectedVertices.UnionWith(unchangedVertices);
        protectedVertices.UnionWith(authoredLocks.Keys);
        var islandCounts=Enumerable.Range(0,points.Length).GroupBy(Find).ToDictionary(g=>g.Key,g=>g.Count());
        foreach(var island in islands){var b=island.Value;
            bool hand=b.min.z>.43f&&b.size.z<.2f&&b.size.x<.13f&&Mathf.Abs(b.center.y-centre.y)>.2f;
            bool boot=b.min.z<.04f&&b.max.z<.2f&&b.size.y<.17f;
            bool trousers=appearance.id=="app_goldencore"&&b.min.z>.14f&&b.max.z<.49f&&b.size.y<.2f&&b.size.x<.15f;
            if(islandCounts[island.Key]>=100&&(hand||boot||trousers))protectedIslands.Add(island.Key);
        }
        var distances=new float[points.Length];var fabricWeights=(BoneWeight[])weights.Clone();
        var fabricPoints=(Vector3[])points.Clone();int hems=0,sleeves=0;
        BoneWeight FabricWeight(BoneWeight old,Vector3 point,bool sleeve,int side,float strength){
            int chain;float angular=0;int next;
            if(sleeve){
                chain=Sectors*Segments+side*Segments;next=chain;
                if(manual!=null&&manual.fullSleeves){
                    int upper=side==0?31:55,fore=side==0?32:56,hand=side==0?36:60;
                    var upperAnchor=Vector3.Lerp(Rest(upper),Rest(fore),.55f);var foreAnchor=Vector3.Lerp(Rest(fore),Rest(hand),.65f);
                    angular=Mathf.SmoothStep(0,1,Mathf.InverseLerp(upperAnchor.y,foreAnchor.y,point.y));
                    next=chain;chain=Sectors*Segments+2*Segments+side*Segments;
                }
            }
            else{float angle=Mathf.Repeat(Mathf.Atan2(point.y-centre.y,point.x-centre.x),Mathf.PI*2)*Sectors/(Mathf.PI*2);int sector=Mathf.FloorToInt(angle);chain=sector*Segments;next=((sector+1)%Sectors)*Segments;angular=angle-sector;}
            BoneWeight ChainWeight(int start){float root=sleeve?(start>=Sectors*Segments+2*Segments?Vector3.Lerp(Rest(side==0?31:55),Rest(side==0?32:56),.55f).z:Vector3.Lerp(Rest(side==0?32:56),Rest(side==0?36:60),.65f).z):waist;float progress=Mathf.Clamp((root-point.z)/specs[start].末端偏移.magnitude,0,Segments-1);int first=Mathf.FloorToInt(progress);return new BoneWeight{boneIndex0=78+start+first,weight0=1-(progress-first),boneIndex1=78+start+Mathf.Min(first+1,Segments-1),weight1=progress-first};}
            // 先在相邻袍片/节之间插值，再渐变接回原腰线；每个点最多四根有效骨。
            var a=ChainWeight(chain);var b=ChainWeight(next);var map=new Dictionary<int,float>();
            void Add(int i,float value){if(value>0)map[i]=map.TryGetValue(i,out float v)?v+value:value;}
            void Insert(BoneWeight w,float value){Add(w.boneIndex0,w.weight0*value);Add(w.boneIndex1,w.weight1*value);Add(w.boneIndex2,w.weight2*value);Add(w.boneIndex3,w.weight3*value);}
            Insert(old,1-strength);Insert(a,strength*(1-angular));Insert(b,strength*angular);
            var top=map.OrderByDescending(x=>x.Value).Take(4).ToArray();float sum=top.Sum(x=>x.Value);var result=new BoneWeight();
            if(top.Length>0){result.boneIndex0=top[0].Key;result.weight0=top[0].Value/sum;}if(top.Length>1){result.boneIndex1=top[1].Key;result.weight1=top[1].Value/sum;}
            if(top.Length>2){result.boneIndex2=top[2].Key;result.weight2=top[2].Value/sum;}if(top.Length>3){result.boneIndex3=top[3].Key;result.weight3=top[3].Value/sum;}return result;
        }
        var baseMesh=AppearanceMeshBinding.创建兼容网格(source,reference.sharedMesh,reference.bones,true,weights,explicitHair);
        var bodyPoints=baseMesh.vertices;UnityEngine.Object.DestroyImmediate(baseMesh);
        if(manual?.leftShoeVertices?.Length>0&&manual.rightShoeVertices?.Length>0)for(int side=0;side<2;side++){
            int foot=side==0?5:15;var shoe=side==0?manual.leftShoeVertices:manual.rightShoeVertices;
            // 鞋的几何鞋底已水平；自动足骨的 roll 不能拿来再次旋转整只鞋。
            var sourceFoot=(Vector3)originalBinds[foot].inverse.GetColumn(3);
            var targetFoot=(Vector3)reference.sharedMesh.bindposes[foot].inverse.GetColumn(3);
            int calf=side==0?4:13;var sourceCalf=(Vector3)originalBinds[calf].inverse.GetColumn(3);var targetCalf=(Vector3)reference.sharedMesh.bindposes[calf].inverse.GetColumn(3);
            foreach(int i in shoe){var w=weights[i];float t=w.boneIndex0==calf?w.weight0:w.boneIndex1==calf?w.weight1:0;bodyPoints[i]=points[i]+Vector3.Lerp(targetFoot-sourceFoot,targetCalf-sourceCalf,t);}
        }
        BoneWeight Mix(BoneWeight a,BoneWeight b,float t){
            var map=new Dictionary<int,float>();
            void Add(int bone,float value){if(value>0)map[bone]=map.TryGetValue(bone,out float old)?old+value:value;}
            void Insert(BoneWeight w,float value){Add(w.boneIndex0,w.weight0*value);Add(w.boneIndex1,w.weight1*value);Add(w.boneIndex2,w.weight2*value);Add(w.boneIndex3,w.weight3*value);}
            Insert(a,1-t);Insert(b,t);var top=map.OrderByDescending(x=>x.Value).Take(4).ToArray();float sum=top.Sum(x=>x.Value);var result=new BoneWeight();
            if(top.Length>0){result.boneIndex0=top[0].Key;result.weight0=top[0].Value/sum;}if(top.Length>1){result.boneIndex1=top[1].Key;result.weight1=top[1].Value/sum;}
            if(top.Length>2){result.boneIndex2=top[2].Key;result.weight2=top[2].Value/sum;}if(top.Length>3){result.boneIndex3=top[3].Key;result.weight3=top[3].Value/sum;}return result;
        }
        for(int n=0;n<points.Length;n++){
            if(unchangedVertices.Contains(n)||authoredLocks.ContainsKey(n))continue;
            if(authoredRegions[n]==0&&(protectedIslands.Contains(Find(n))||shoulderPanels.Contains(n))){protectedVertices.Add(n);continue;}
            var p=points[n];var w=weights[n];
            float Sum(int a,int b)=>(w.boneIndex0>=a&&w.boneIndex0<=b?w.weight0:0)+(w.boneIndex1>=a&&w.boneIndex1<=b?w.weight1:0)+(w.boneIndex2>=a&&w.boneIndex2<=b?w.weight2:0)+(w.boneIndex3>=a&&w.boneIndex3<=b?w.weight3:0);
            // 练气的靴子与衣身共用连通网格，必须按脚骨权重及小腿空间单独保护。
            if(appearance.id=="app_player"&&p.z<.26f){
                float footWeight=Sum(5,11)+Sum(15,20);
                int calf=p.y>centre.y?4:13,foot=calf==4?5:15;
                var axis=Rest(foot)-Rest(calf);var nearest=Rest(calf)+axis*Mathf.Clamp01(Vector3.Dot(p-Rest(calf),axis)/axis.sqrMagnitude);
                if(footWeight>.15f||(Sum(4,11)+Sum(13,20)>.65f&&(p-nearest).sqrMagnitude<.065f*.065f)){protectedVertices.Add(n);continue;}
            }
            int side=p.y>centre.y?0:1,upper=side==0?31:55,fore=side==0?32:56,hand=side==0?36:60;
            var arm=Vector3.Lerp(Rest(upper),Rest(hand),Mathf.Clamp01((p.y-Rest(upper).y)/(Rest(hand).y-Rest(upper).y)));
            float lateral=Mathf.Abs(p.y-centre.y);
            // 长袖可以垂到腰线以下。先按手臂权重识别，不能先按高度把袖尖截成腰部衣摆。
            float sleeveMix=wideSleeves&&Sum(24,29)<.35f?Fade(.10f,.45f,Sum(30,77))*Fade(.08f,.14f,lateral)*Fade(.24f,.30f,p.z):0;
            bool robe=p.z<waist&&Sum(24,29)<.35f&&lateral<.15f+(waist-p.z)*.80f;
            if(authoredRegions[n]==1){sleeveMix=0;robe=p.z<waist;}
            bool authoredSleeve=authoredRegions[n]==2||authoredRegions[n]==3;
            if(authoredSleeve){side=authoredRegions[n]-2;fore=side==0?32:56;hand=side==0?36:60;upper=side==0?31:55;arm=Vector3.Lerp(Rest(upper),Rest(hand),Mathf.Clamp01((p.y-Rest(upper).y)/(Rest(hand).y-Rest(upper).y)));sleeveMix=1;robe=false;}
            float hemStrength=robe?Fade(0,.15f,waist-p.z):0;
            float sleeveStrength=.35f*Fade(.025f,.15f,arm.z-p.z);
            if(authoredSleeve)sleeveStrength=.98f*Fade(.04f,.16f,arm.z-p.z);
            float sleeveDistance=authoredSleeve?.003f+.012f*Fade(.04f,.22f,arm.z-p.z):.012f*Fade(.025f,.22f,arm.z-p.z);
            float hemDistance=robe?.018f*Fade(0,.32f,waist-p.z):0;
            distances[n]=Mathf.Lerp(hemDistance,sleeveDistance,sleeveMix);
            if(distances[n]<=.00001f)continue;
            // 袖根到衣身的混合区同时渐变绑定与静止位置，不能在同一片表面上硬切腰/手臂。
            fabricWeights[n]=Mix(FabricWeight(w,p,false,side,hemStrength),wideSleeves?FabricWeight(w,p,true,side,sleeveStrength):w,sleeveMix);
            var hipPoint=Vector3.Lerp(bodyPoints[n],correction[hemParent].MultiplyPoint3x4(p),hemStrength);
            var armPoint=bodyPoints[n];
            fabricPoints[n]=Vector3.Lerp(hipPoint,armPoint,sleeveMix);
            if(sleeveMix>.5f)sleeves++;else hems++;
        }
        // 人体与衣料的明确连接边界采用完全一致的蒙皮，不让表面波动拉开袖根/腰口。
        if(faceRegions!=null)for(int i=0;i<points.Length;i++)if(pinnedSeams.Contains(PointKey(points[i]))){distances[i]=0;fabricPoints[i]=bodyPoints[i];fabricWeights[i]=originalWeights[i];protectedVertices.Add(i);}
        // 先统一同点的分区与权重，再拆三角；否则 UV 接缝一侧跟腰、另一侧跟腿，会拉开袍面。
        foreach(var group in Enumerable.Range(0,points.Length).GroupBy(i=>new Vector3Int(Mathf.RoundToInt(points[i].x*100000),Mathf.RoundToInt(points[i].y*100000),Mathf.RoundToInt(points[i].z*100000)))){
            int canonical=group.OrderByDescending(i=>distances[i]).First();
            bool body=group.Any(i=>protectedVertices.Contains(i));
            foreach(int i in group){distances[i]=body?0:distances[canonical];fabricPoints[i]=fabricPoints[canonical];fabricWeights[i]=fabricWeights[canonical];}
        }
        if(hems<100||wideSleeves&&sleeves<30)throw new InvalidOperationException(appearance.DisplayName+" 布料区域未覆盖");
        if(explicitHair&&faceRegions!=null){
            // 衣料的静止形状必须与它实际使用的骨架绑定一致，不能沿用自动蒙皮混合出的折叠形状。
            // 腰以下的闭襟袍面先完整携带原形，再由分段骨与表面网络弯曲。
            foreach(int i in Enumerable.Range(0,points.Length)){
                if(pinnedSeams.Contains(PointKey(points[i])))continue;
                if(authoredRegions[i]==1&&points[i].z<waist){
                    fabricPoints[i]=correction[hemParent].MultiplyPoint3x4(points[i]);
                    float strength=Fade(0,.15f,waist-points[i].z);
                    fabricWeights[i]=FabricWeight(new BoneWeight{boneIndex0=hemParent,weight0=1},points[i],false,0,strength);
                    distances[i]=Mathf.Max(distances[i],.018f*strength);
                }
            }
        }
        var hairVertices=new List<int>();
        if(wideSleeves){
            var reviewedHair=new HashSet<int>();
            var hairBoundaryDistance=new Dictionary<string,float>();
            if(explicitHair){
                if(faceRegions==null)throw new InvalidOperationException("明确长发选区需要全源面清单");
                for(int face=0;face<faceRegions.Length;face++)if(faceRegions[face]==6)for(int j=0;j<3;j++)reviewedHair.Add(triangles[face*3+j]);
                var bodyKeys=new HashSet<string>();var graph=new Dictionary<string,List<KeyValuePair<string,float>>>();
                for(int face=0;face<faceRegions.Length;face++){
                    if(faceRegions[face]==0)for(int j=0;j<3;j++)bodyKeys.Add(PointKey(points[triangles[face*3+j]]));
                    if(faceRegions[face]!=6)continue;
                    for(int j=0;j<3;j++){
                        int a=triangles[face*3+j],b=triangles[face*3+(j+1)%3];string ka=PointKey(points[a]),kb=PointKey(points[b]);float length=Vector3.Distance(points[a],points[b]);
                        if(!graph.ContainsKey(ka))graph[ka]=new List<KeyValuePair<string,float>>();if(!graph.ContainsKey(kb))graph[kb]=new List<KeyValuePair<string,float>>();
                        graph[ka].Add(new KeyValuePair<string,float>(kb,length));graph[kb].Add(new KeyValuePair<string,float>(ka,length));
                    }
                }
                var queue=new SortedSet<(float,string)>();
                foreach(var key in graph.Keys){float d=bodyKeys.Contains(key)?0:float.PositiveInfinity;hairBoundaryDistance[key]=d;if(d==0)queue.Add((0,key));}
                while(queue.Count>0){var node=queue.Min;queue.Remove(node);if(node.Item1>hairBoundaryDistance[node.Item2])continue;
                    foreach(var next in graph[node.Item2]){float d=node.Item1+next.Value;if(d>=hairBoundaryDistance[next.Key])continue;hairBoundaryDistance[next.Key]=d;queue.Add((d,next.Key));}
                }
            }
            // 长发颜色与头/颈权重共同筛选；帽冠、脸、衣领和贴头发根保持原动画。
            var texture=source.sharedMaterials[0].mainTexture;
            var previous=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try{Graphics.Blit(texture,rt);RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
            var uv=original.uv;var head=Rest(26);float rootHeight=head.z+.10f;
            try{for(int i=0;i<points.Length;i++){
                var p=points[i];if(p.z>=rootHeight)continue;
                if(explicitHair){if(reviewedHair.Contains(i))hairVertices.Add(i);continue;}
                if(p.z<head.z-.38f)continue;
                if(p.x<=head.x-.025f&&Mathf.Abs(p.y-head.y)<=.06f)continue;
                var w=originalWeights[i];float HeadWeight(int b,float value)=>b>=24&&b<=26?value:0;
                float headWeight=HeadWeight(w.boneIndex0,w.weight0)+HeadWeight(w.boneIndex1,w.weight1)+HeadWeight(w.boneIndex2,w.weight2)+HeadWeight(w.boneIndex3,w.weight3);
                if(headWeight<.35f)continue;
                var c=pixels.GetPixelBilinear(uv[i].x,uv[i].y);
                bool hairColor=appearance.id=="app_foundation"?c.g>c.r*1.12f&&c.g>c.b*1.02f:
                    c.r-c.g<.045f&&c.r-c.b<.075f&&Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b)<.19f&&Mathf.Max(c.r,c.g,c.b)>.25f;
                if(hairColor)hairVertices.Add(i);
            }}finally{UnityEngine.Object.DestroyImmediate(pixels);}
            if(hairVertices.Count<100)throw new InvalidOperationException(appearance.DisplayName+" 长发未识别，停止生成");
            int hairStart=specs.Count;float bottom=hairChainBottom>0?hairChainBottom:hairVertices.Min(i=>points[i].z);
            var anchors=new[]{new Vector3(head.x+.075f,head.y+.04f,rootHeight),new Vector3(head.x+.075f,head.y-.04f,rootHeight),
                new Vector3(head.x-.035f,head.y+.085f,rootHeight),new Vector3(head.x-.035f,head.y-.085f,rootHeight)};
            var labels=new[]{"Hair_Back_L","Hair_Back_R","Hair_Side_L","Hair_Side_R"};
            for(int i=0;i<anchors.Length;i++)Chain(labels[i],26,anchors[i],bottom,false,true);
            foreach(int index in hairVertices){
                var p=points[index];float strength=.9f*Fade(0,.17f,rootHeight-p.z);
                // 与头皮/本体相接的发根按袍面拓扑距离渐变；不能在相邻顶点硬切发骨与手臂骨。
                if(explicitHair&&hairBoundaryDistance.TryGetValue(PointKey(p),out float boundary))strength*=Fade(0,.04f,boundary);
                if(strength<.00001f)continue;
                var nearest=anchors.Select((a,i)=>new{index=i,d=(new Vector2(a.x,a.y)-new Vector2(p.x,p.y)).sqrMagnitude}).OrderBy(x=>x.d).Take(2).ToArray();
                float first=nearest[1].d/Mathf.Max(.000001f,nearest[0].d+nearest[1].d);
                var candidates=new Dictionary<int,float>{{26,1-strength}};
                void Add(int bone,float weight){if(weight<=.00001f)return;candidates[bone]=candidates.TryGetValue(bone,out float old)?old+weight:weight;}
                for(int n=0;n<2;n++){
                    int chain=hairStart+nearest[n].index*Segments;float weight=strength*(n==0?first:1-first);
                    float progress=Mathf.Clamp((rootHeight-p.z)/specs[chain].末端偏移.magnitude,0,Segments-1);
                    int a=Mathf.FloorToInt(progress),b=Mathf.Min(a+1,Segments-1);float t=progress-a;
                    Add(78+chain+a,weight*(1-t));Add(78+chain+b,weight*t);
                }
                var top=candidates.OrderByDescending(x=>x.Value).Take(4).ToArray();float sum=top.Sum(x=>x.Value);var bw=new BoneWeight();
                if(top.Length>0){bw.boneIndex0=top[0].Key;bw.weight0=top[0].Value/sum;}
                if(top.Length>1){bw.boneIndex1=top[1].Key;bw.weight1=top[1].Value/sum;}
                if(top.Length>2){bw.boneIndex2=top[2].Key;bw.weight2=top[2].Value/sum;}
                if(top.Length>3){bw.boneIndex3=top[3].Key;bw.weight3=top[3].Value/sum;}
                weights[index]=bw;
            }
        }
        if (!AssetDatabase.IsValidFolder(outputFolder)) throw new InvalidOperationException("着装输出目录不存在："+outputFolder);
        var mesh = AppearanceMeshBinding.创建兼容网格(source,reference.sharedMesh,reference.bones,true,originalWeights,explicitHair);
        mesh.vertices=bodyPoints;
        if(manual?.leftShoeVertices?.Length>0&&manual.rightShoeVertices?.Length>0){
            var mn=mesh.normals;var mt=mesh.tangents;var sn=original.normals;var st=original.tangents;
            foreach(int i in manual.leftShoeVertices.Concat(manual.rightShoeVertices)){mn[i]=sn[i];mt[i]=st[i];}
            mesh.normals=mn;mesh.tangents=mt;
        }
        mesh.name = appearance.id+"-skin"; mesh.bindposes=binds.ToArray();mesh.boneWeights=weights;
        var clothTriangles=new List<int>[original.subMeshCount];
        var used=new SortedSet<int>();
        int reviewedIndex=0,protectedBodyFaces=0;
        for(int sub=0;sub<original.subMeshCount;sub++){
            var bodyTriangles=new List<int>();clothTriangles[sub]=new List<int>();var indices=original.GetTriangles(sub);
            for(int k=0;k<indices.Length;k+=3){
                int role=faceRegions==null?-1:faceRegions[reviewedIndex++];
                if(removedFaces.Contains(FaceKey(points[indices[k]],points[indices[k+1]],points[indices[k+2]]))){if(faceRegions!=null&&role!=7)throw new InvalidOperationException("剔除面不是已核对的封口面");continue;}
                if(role==7)throw new InvalidOperationException("人工封口面未列入明确剔除清单");
                bool automatic=distances[indices[k]]>.00001f||distances[indices[k+1]]>.00001f||distances[indices[k+2]]>.00001f;
                bool cloth=faceRegions==null?automatic:role>=1&&role<=5;
                if(faceRegions!=null&&!cloth&&automatic)protectedBodyFaces++;
                for(int j=0;j<3;j++){int index=indices[k+j];if(cloth){clothTriangles[sub].Add(index);used.Add(index);}else bodyTriangles.Add(index);}}
            mesh.SetTriangles(bodyTriangles,sub,false);
        }
        var selected=used.ToArray();var remap=selected.Select((index,i)=>new{index,i}).ToDictionary(x=>x.index,x=>x.i);
        var weldGroups=selected.GroupBy(i=>new Vector3Int(Mathf.RoundToInt(points[i].x*100000),Mathf.RoundToInt(points[i].y*100000),Mathf.RoundToInt(points[i].z*100000))).ToArray();
        var welded=new int[selected.Length];
        foreach(var group in weldGroups){
            int canonical=group.OrderBy(i=>distances[i]).First();
            foreach(int i in group){welded[remap[i]]=remap[canonical];distances[i]=distances[canonical];fabricPoints[i]=fabricPoints[canonical];fabricWeights[i]=fabricWeights[canonical];}
        }
        var fabric=new Mesh{name=appearance.id+"-fabric",indexFormat=mesh.indexFormat};
        var reposed=mesh.vertices;fabric.vertices=selected.Select(i=>distances[i]>.00001f?fabricPoints[i]:reposed[i]).ToArray();
        fabric.uv=selected.Select(i=>original.uv[i]).ToArray();
        fabric.normals=selected.Select(i=>mesh.normals[i]).ToArray();fabric.tangents=selected.Select(i=>mesh.tangents[i]).ToArray();
        fabric.boneWeights=selected.Select(i=>distances[i]>.00001f?fabricWeights[i]:originalWeights[i]).ToArray();
        // 分段衣骨的权重已在上游按袍面区域生成；这里不能再按高度覆盖成刚性腰骨。
        fabric.bindposes=binds.ToArray();fabric.subMeshCount=original.subMeshCount;
        for(int sub=0;sub<fabric.subMeshCount;sub++)fabric.SetTriangles(clothTriangles[sub].Select(i=>remap[i]).ToArray(),sub,false);
        int sourceFabricTriangles=fabric.triangles.Length/3;
        var freedom=selected.Select(i=>distances[i]).ToList();
        var shell=selected.Select(i=>explicitHair&&authoredRegions[i]==1?1f:0).ToList();
        var shellProgress=selected.Select(i=>Mathf.Clamp01((waist-points[i].z)/Mathf.Max(.1f,waist-.04f))).ToList();
        var innerIds=new HashSet<int>();
        if(manual?.innerSkirtFaces!=null)foreach(int f in manual.innerSkirtFaces)for(int j=0;j<3;j++)innerIds.Add(triangles[f*3+j]);
        var inner=selected.Select(i=>innerIds.Contains(i)?1f:0).ToList();
        Refine(fabric,freedom,shell,shellProgress,inner);
        var finalPoints=fabric.vertices;var weldedFinal=new int[finalPoints.Length];var canonicalPoints=new Dictionary<Vector3Int,int>();
        for(int i=0;i<finalPoints.Length;i++){
            var p=finalPoints[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
            if(!canonicalPoints.TryGetValue(key,out int canonical)){canonical=i;canonicalPoints.Add(key,i);}weldedFinal[i]=canonical;
        }
        for(int i=0;i<inner.Count;i++)inner[weldedFinal[i]]=Mathf.Max(inner[weldedFinal[i]],inner[i]);
        for(int i=0;i<inner.Count;i++)inner[i]=inner[weldedFinal[i]];
        if(explicitHair){
            // 细分产生的新边中点也必须共用蒙皮，不能只在显示时把不同动画位置平均焊合。
            var finalWeights=fabric.boneWeights;
            for(int i=0;i<finalWeights.Length;i++)finalWeights[i]=finalWeights[weldedFinal[i]];
            fabric.boneWeights=finalWeights;
        }
        fabric.RecalculateBounds();
        int addedVertices=0,addedTriangles=0;
        Vector3[] shoeAnchors=new Vector3[0];
        int[] shoeAnchorBones=new int[0];
        if(manual!=null&&manual.shoesOnly){} // 用户明确选择只保留原鞋，重新烘焙也不能补回腿或内衬。
        else if(manual!=null&&manual.thinLegs)AddThinLegs(mesh,reference.sharedMesh,manual.leftShoeVertices,manual.rightShoeVertices,out addedVertices,out addedTriangles,out shoeAnchors,out shoeAnchorBones);
        else if(addInnerLegs)AddInnerLegs(mesh,original,reference.sharedMesh,out addedVertices,out addedTriangles);
        mesh=Store(mesh,outputFolder+"/"+appearance.id+"-skin.asset");
        fabric=Store(fabric,outputFolder+"/"+appearance.id+"-fabric.asset");
        string path=outputFolder+"/"+appearance.id+".asset";
        var profile=AssetDatabase.LoadAssetAtPath<AppearanceClothProfile>(path);
        if(!profile){profile=ScriptableObject.CreateInstance<AppearanceClothProfile>();AssetDatabase.CreateAsset(profile,path);}
        profile.蒙皮=mesh;profile.布料蒙皮=fabric;profile.布料顶点约束=freedom.ToArray();profile.人体骨名=source.bones.Select(x=>x.name).ToArray();profile.衣骨=specs.ToArray();
        profile.布料同点=weldedFinal;profile.原布料三角数=sourceFabricTriangles;
        profile.袍面覆盖=explicitHair?shell.ToArray():new float[0];profile.袍面进度=explicitHair?shellProgress.ToArray():new float[0];
        profile.仅保留原鞋=manual!=null&&manual.shoesOnly;
        if(profile.仅保留原鞋&&manual.closedLegWidth>0){profile.闭襟腿距=manual.closedLegWidth;profile.闭襟收束=manual.closedLegBlend;}
        profile.闭襟覆盖=manual!=null&&(manual.thinLegs||manual.shoesOnly)?inner.ToArray():new float[0];profile.鞋口锚点=shoeAnchors;profile.鞋口骨索引=shoeAnchorBones;profile.细腿半径=.009f;
        profile.鞋靴半径=shoeAnchors.Length==2?new[]{Mathf.Clamp((manual.leftShoeVertices.Max(i=>bodyPoints[i].y)-manual.leftShoeVertices.Min(i=>bodyPoints[i].y))*.45f,.02f,.045f),Mathf.Clamp((manual.rightShoeVertices.Max(i=>bodyPoints[i].y)-manual.rightShoeVertices.Min(i=>bodyPoints[i].y))*.45f,.02f,.045f)}:new float[0];
        profile.鞋趾锚点=new Vector3[shoeAnchors.Length];
        for(int side=0;side<shoeAnchors.Length;side++){
            var shoe=side==0?manual.leftShoeVertices:manual.rightShoeVertices;var tip=shoe.OrderBy(i=>bodyPoints[i].x).Take(Mathf.Max(8,shoe.Length/8)).ToArray();
            var centreTip=Vector3.zero;foreach(int i in tip)centreTip+=bodyPoints[i]/tip.Length;
            profile.鞋趾锚点[side]=reference.sharedMesh.bindposes[side==0?5:15].MultiplyPoint3x4(centreTip);
        }
        profile.长发顶点=hairVertices.ToArray();
        profile.补全人体顶点数=addedVertices;profile.补全人体三角数=addedTriangles;
        if(profile.仅保留原鞋)设置原鞋接触(profile,manual.leftShoeVertices,manual.rightShoeVertices);
        profile.移除封底三角数=removedCount;
        profile.姿态空间随动=poseSpace;
        profile.单节限角=25;
        if(manual!=null&&manual.useGoldenTrousers)OutfitTrouserBaker.Bake(profile,reference,outputFolder);
        else{profile.裤装蒙皮=null;profile.裤装材质=null;}
        if(faceRegions!=null)Debug.Log("[明确衣料分面] "+appearance.id+" 衣料面="+sourceFabricTriangles+" 固定边界点="+pinnedSeams.Count+" 阻止自动误选本体面="+protectedBodyFaces);
        appearance.衣服随动=profile;EditorUtility.SetDirty(profile);EditorUtility.SetDirty(appearance);EditorUtility.SetDirty(mesh);
        Debug.Log("[着装随动] "+appearance.DisplayName+"：衣摆="+hems+" 宽袖="+sleeves+" 长发="+hairVertices.Count+" 附加骨="+specs.Count);
    }

    static void 平滑分区绑定(Vector3[] points,BoneWeight[] weights,int[] triangles,Matrix4x4[] binds)
    {
        string Key(int i)=>Mathf.RoundToInt(points[i].x*100000)+","+Mathf.RoundToInt(points[i].y*100000)+","+Mathf.RoundToInt(points[i].z*100000);
        var groups=Enumerable.Range(0,points.Length).GroupBy(Key).Select(g=>g.ToArray()).ToArray();var canonical=new int[points.Length];
        for(int g=0;g<groups.Length;g++)foreach(int i in groups[g])canonical[i]=g;
        var neighbours=groups.Select(_=>new HashSet<int>()).ToArray();
        for(int t=0;t<triangles.Length;t+=3)for(int j=0;j<3;j++){int a=canonical[triangles[t+j]],b=canonical[triangles[t+(j+1)%3]];if(a!=b){neighbours[a].Add(b);neighbours[b].Add(a);}}
        var values=groups.Select(g=>{var result=new float[78];foreach(int i in g){var w=weights[i];result[w.boneIndex0]+=w.weight0/g.Length;result[w.boneIndex1]+=w.weight1/g.Length;result[w.boneIndex2]+=w.weight2/g.Length;result[w.boneIndex3]+=w.weight3/g.Length;}return result;}).ToArray();
        Vector3 Rest(int b)=>binds[b].inverse.GetColumn(3);
        var locked=groups.Select(g=>{var p=points[g[0]];return (p-Rest(36)).sqrMagnitude<.065f*.065f||(p-Rest(60)).sqrMagnitude<.065f*.065f||p.z<.14f&&Mathf.Min((p-Rest(5)).sqrMagnitude,(p-Rest(15)).sqrMagnitude)<.065f*.065f;}).ToArray();
        // 分区边界需经过拓扑渐变，不能把同一片连续袍面硬切成腰骨/臂骨/头骨。
        for(int pass=0;pass<20;pass++){
            var next=values.Select(v=>(float[])v.Clone()).ToArray();
            for(int g=0;g<groups.Length;g++)if(!locked[g]&&neighbours[g].Count>0)for(int b=0;b<78;b++)next[g][b]=values[g][b]*.35f+neighbours[g].Sum(n=>values[n][b])*.65f/neighbours[g].Count;
            values=next;
        }
        for(int g=0;g<groups.Length;g++){
            var top=Enumerable.Range(0,78).OrderByDescending(b=>values[g][b]).Take(4).ToArray();float sum=top.Sum(b=>values[g][b]);
            var w=new BoneWeight{boneIndex0=top[0],weight0=values[g][top[0]]/sum,boneIndex1=top[1],weight1=values[g][top[1]]/sum,boneIndex2=top[2],weight2=values[g][top[2]]/sum,boneIndex3=top[3],weight3=values[g][top[3]]/sum};foreach(int i in groups[g])weights[i]=w;
        }
    }

    // 筑基原模型的袍内没有腿部表面，不能把腰侧飘带收拢假装成裤腿。
    // 补入贴合人体骨架的内裤腿，上接髋部、下伸入原靴筒；原模型的任何部件均不改形。
    static void AddInnerLegs(Mesh mesh,Mesh original,Mesh reference,out int addedVertices,out int addedTriangles)
    {
        var vertices=mesh.vertices.ToList();var normals=mesh.normals.ToList();var uv=mesh.uv.ToList();var tangents=mesh.tangents.ToList();var weights=mesh.boneWeights.ToList();
        int start=vertices.Count;var faces=mesh.GetTriangles(0).ToList();int previousFaces=faces.Count;
        var originalPoints=original.vertices;var originalUV=original.uv;var binds=original.bindposes;
        Vector3 Rest(int b)=>binds[b].inverse.GetColumn(3);
        Vector3 RefRest(int b)=>reference.bindposes[b].inverse.GetColumn(3);
        const int sides=12,rings=5;
        for(int side=0;side<2;side++){
            int thigh=side==0?3:12,calf=side==0?4:13,foot=side==0?5:15;int first=vertices.Count;
            var sourceWeights=original.boneWeights;
            float FootWeight(int i){var w=sourceWeights[i];return (w.boneIndex0==foot?w.weight0:0)+(w.boneIndex1==foot?w.weight1:0)+(w.boneIndex2==foot?w.weight2:0)+(w.boneIndex3==foot?w.weight3:0);}
            int boot=Enumerable.Range(0,originalPoints.Length).Where(i=>originalPoints[i].z<.15f&&(originalPoints[i].y>0)==(side==0)).OrderByDescending(i=>FootWeight(i)).ThenBy(i=>(originalPoints[i]-Rest(foot)).sqrMagnitude).First();
            for(int r=0;r<rings;r++){
                // 新补几何直接围绕实际动画骨架建模，不再混入源自动骨轴的 roll。
                var centre=r<3?Vector3.Lerp(RefRest(thigh),RefRest(calf),r*.5f):Vector3.Lerp(RefRest(calf),RefRest(foot),r==3?.45f:.96f);
                float radius=r<2?.031f:r==2?.027f:.023f;
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(RefRest(calf).z+.07f,RefRest(calf).z-.04f,centre.z));
                var w=new BoneWeight{boneIndex0=thigh,weight0=1-blend,boneIndex1=calf,weight1=blend};
                for(int j=0;j<sides;j++){
                    float angle=j*Mathf.PI*2/sides;var normal=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);var p=centre+normal*radius;
                    vertices.Add(p);
                    normals.Add(normal);
                    uv.Add(originalUV[boot]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.001f);tangents.Add(new Vector4(1,0,0,1));weights.Add(w);
                    if(r>0){int v=first+r*sides+j,n=first+r*sides+(j+1)%sides;faces.Add(v-sides);faces.Add(v);faces.Add(n);faces.Add(v-sides);faces.Add(n);faces.Add(n-sides);}
                }
            }
        }
        // 袍内仍需要胯部遮挡；两条独立的空筒在低角度下会露出上方空洞。
        int hipStart=vertices.Count;
        var hipCentre=(RefRest(3)+RefRest(12))*.5f;
        var innerUV=uv[start];var hipWeight=new BoneWeight{boneIndex0=1,weight0=1};
        for(int ring=0;ring<2;ring++)for(int j=0;j<sides;j++){
            float angle=j*Mathf.PI*2/sides;var normal=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            // 内衬延至腰腹，避免独立衣身与腰带在弯腰时露出背景缝隙。
            var centre=ring==0?RefRest(22):hipCentre-Vector3.forward*.04f;
            vertices.Add(centre+new Vector3(normal.x*(ring==0?.038f:.032f),normal.y*(ring==0?.065f:.058f),0));
            normals.Add(normal);uv.Add(innerUV);tangents.Add(new Vector4(1,0,0,1));weights.Add(ring==0?new BoneWeight{boneIndex0=21,weight0=1}:hipWeight);
            if(ring==1){int v=hipStart+sides+j,n=hipStart+sides+(j+1)%sides;faces.Add(v-sides);faces.Add(v);faces.Add(n);faces.Add(v-sides);faces.Add(n);faces.Add(n-sides);}
        }
        int cap=vertices.Count;vertices.Add(hipCentre-Vector3.forward*.04f);normals.Add(Vector3.back);uv.Add(innerUV);tangents.Add(new Vector4(1,0,0,1));weights.Add(hipWeight);
        for(int j=0;j<sides;j++){faces.Add(cap);faces.Add(hipStart+sides+(j+1)%sides);faces.Add(hipStart+sides+j);}
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.boneWeights=weights.ToArray();mesh.SetTriangles(faces,0);mesh.RecalculateBounds();
        addedVertices=vertices.Count-start;addedTriangles=(faces.Count-previousFaces)/3;
    }

    public static void 设置原鞋接触(AppearanceClothProfile profile,int[] left,int[] right)
    {
        var vertices=profile.蒙皮.vertices;var binds=profile.蒙皮.bindposes;
        profile.原鞋筒锚点=new Vector3[2];profile.原鞋筒半径=new float[2];
        profile.原鞋掌锚点=new Vector3[2];profile.原鞋掌半径=new float[2];
        for(int side=0;side<2;side++){
            var shoe=side==0?left:right;int calf=side==0?4:13;
            if(shoe==null||shoe.Length<8)throw new InvalidOperationException("缺少人工原鞋选区");
            var rim=shoe.OrderByDescending(i=>vertices[i].z).Take(Mathf.Max(8,shoe.Length/8)).ToArray();
            var centre=Vector3.zero;foreach(int i in rim)centre+=vertices[i]/rim.Length;
            profile.原鞋筒锚点[side]=binds[calf].MultiplyPoint3x4(centre);
            var ankle=(Vector3)binds[side==0?5:15].inverse.GetColumn(3);var axis=ankle-centre;
            float radius=0;
            // 用完整靴筒的径向范围，鞋尖不属于靴筒。只量最上沿会漏掉更宽的小腿段。
            foreach(int i in shoe)if(vertices[i].z>ankle.z+.015f){
                var nearest=centre+axis*Mathf.Clamp01(Vector3.Dot(vertices[i]-centre,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));
                radius=Mathf.Max(radius,Vector3.Distance(vertices[i],nearest));
            }
            profile.原鞋筒半径[side]=radius+.003f;
            var sole=shoe.Where(i=>vertices[i].z<=ankle.z+.015f).ToArray();
            if(sole.Length<4)throw new InvalidOperationException("人工原鞋区缺少鞋底");
            var bounds=new Bounds(vertices[sole[0]],Vector3.zero);foreach(int i in sole)bounds.Encapsulate(vertices[i]);
            profile.原鞋掌锚点[side]=binds[side==0?5:15].MultiplyPoint3x4(bounds.center);
            profile.原鞋掌半径[side]=sole.Max(i=>Vector3.Distance(vertices[i],bounds.center))+.002f;
        }
    }

    // 鞋口由用户鞋区确定，细腿仅连接髋、膝、鞋口；不生成腹部或胸口内衬。
    static void AddThinLegs(Mesh mesh,Mesh reference,int[] left,int[] right,out int addedVertices,out int addedTriangles,out Vector3[] anchors,out int[] anchorBones)
    {
        var vertices=mesh.vertices.ToList();var normals=mesh.normals.ToList();var uv=mesh.uv.ToList();var tangents=mesh.tangents.ToList();var weights=mesh.boneWeights.ToList();
        var faces=mesh.GetTriangles(0).ToList();int firstVertex=vertices.Count,firstFace=faces.Count;anchors=new Vector3[2];anchorBones=new int[2];
        Vector3 Rest(int b)=>reference.bindposes[b].inverse.GetColumn(3);
        const int sides=8,rings=7;const float radius=.009f;
        for(int side=0;side<2;side++){
            var shoe=side==0?left:right;
            if(shoe==null||shoe.Length<12||shoe.Any(i=>i<0||i>=firstVertex))throw new InvalidOperationException("缺少明确鞋区，停止补腿");
            int thigh=side==0?3:12,calf=side==0?4:13,foot=side==0?5:15;
            var rim=shoe.OrderByDescending(i=>vertices[i].z).Take(Mathf.Max(8,shoe.Length/8)).ToArray();
            var end=Vector3.zero;foreach(int i in rim)end+=vertices[i]/rim.Length;
            // 插入鞋口少许，避免独立鞋与细腿在抬脚时露出接合缝。
            end.z-=.005f;int anchorBone=end.z>Rest(foot).z+.09f?calf:foot;anchorBones[side]=anchorBone;anchors[side]=reference.bindposes[anchorBone].MultiplyPoint3x4(end);
            var hip=Rest(thigh);var knee=Rest(calf);
            var centres=new[]{hip,Vector3.Lerp(hip,knee,.45f),Vector3.Lerp(hip,knee,.88f),knee,Vector3.Lerp(knee,end,.12f),Vector3.Lerp(knee,end,.75f),end};
            var ws=new[]{new BoneWeight{boneIndex0=thigh,weight0=1},new BoneWeight{boneIndex0=thigh,weight0=1},
                new BoneWeight{boneIndex0=thigh,weight0=.85f,boneIndex1=calf,weight1=.15f},new BoneWeight{boneIndex0=thigh,weight0=.5f,boneIndex1=calf,weight1=.5f},
                new BoneWeight{boneIndex0=calf,weight0=1},new BoneWeight{boneIndex0=calf,weight0=.8f,boneIndex1=anchorBone,weight1=.2f},new BoneWeight{boneIndex0=anchorBone,weight0=1}};
            int start=vertices.Count;var colourUV=uv[shoe[0]];
            for(int r=0;r<rings;r++){
                var axis=(centres[Mathf.Min(r+1,rings-1)]-centres[Mathf.Max(0,r-1)]).normalized;
                var u=Vector3.Cross(axis,Vector3.up).normalized;var v=Vector3.Cross(axis,u).normalized;
                for(int j=0;j<sides;j++){
                    float a=j*Mathf.PI*2/sides;var normal=u*Mathf.Cos(a)+v*Mathf.Sin(a);
                    vertices.Add(centres[r]+normal*radius);normals.Add(normal);uv.Add(colourUV);tangents.Add(new Vector4(u.x,u.y,u.z,1));weights.Add(ws[r]);
                    if(r>0){int b=start+r*sides+j,n=start+r*sides+(j+1)%sides;faces.Add(b-sides);faces.Add(b);faces.Add(n);faces.Add(b-sides);faces.Add(n);faces.Add(n-sides);}
                }
            }
            for(int cap=0;cap<2;cap++){
                int r=cap==0?0:rings-1,index=vertices.Count;vertices.Add(centres[r]);normals.Add(cap==0?Vector3.forward:Vector3.back);uv.Add(colourUV);tangents.Add(Vector4.one);weights.Add(ws[r]);
                for(int j=0;j<sides;j++){faces.Add(index);faces.Add(start+r*sides+(cap==0?j:(j+1)%sides));faces.Add(start+r*sides+(cap==0?(j+1)%sides:j));}
            }
        }
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.boneWeights=weights.ToArray();mesh.SetTriangles(faces,0);mesh.RecalculateBounds();
        addedVertices=vertices.Count-firstVertex;addedTriangles=(faces.Count-firstFace)/3;
    }

    // 补足长边弯折点，供表面避让使用。
    static void Refine(Mesh mesh,List<float> freedom,List<float> shell,List<float> progress,List<float> inner)
    {
        var points=mesh.vertices.ToList();var normals=mesh.normals.ToList();var uv=mesh.uv.ToList();var weights=mesh.boneWeights.ToList();var tangents=mesh.tangents.ToList();
        var submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(mesh.GetTriangles).ToArray();
        BoneWeight Average(BoneWeight a,BoneWeight b){
            var map=new Dictionary<int,float>();
            void Add(int i,float w){if(w>0)map[i]=map.TryGetValue(i,out float old)?old+w:w;}
            void Insert(BoneWeight w){Add(w.boneIndex0,w.weight0*.5f);Add(w.boneIndex1,w.weight1*.5f);Add(w.boneIndex2,w.weight2*.5f);Add(w.boneIndex3,w.weight3*.5f);}
            Insert(a);Insert(b);var top=map.OrderByDescending(x=>x.Value).Take(4).ToArray();float sum=top.Sum(x=>x.Value);var result=new BoneWeight();
            if(top.Length>0){result.boneIndex0=top[0].Key;result.weight0=top[0].Value/sum;}if(top.Length>1){result.boneIndex1=top[1].Key;result.weight1=top[1].Value/sum;}
            if(top.Length>2){result.boneIndex2=top[2].Key;result.weight2=top[2].Value/sum;}if(top.Length>3){result.boneIndex3=top[3].Key;result.weight3=top[3].Value/sum;}return result;
        }
        for(int pass=0;pass<4;pass++){
            var midpoints=new Dictionary<ulong,int>();
            int Mid(int a,int b){
                if((points[a]-points[b]).sqrMagnitude<=.045f*.045f||Mathf.Max(freedom[a],freedom[b])<=.00001f)return -1;
                ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);if(midpoints.TryGetValue(key,out int index))return index;
                index=points.Count;points.Add((points[a]+points[b])*.5f);normals.Add((normals[a]+normals[b]).normalized);uv.Add((uv[a]+uv[b])*.5f);
                tangents.Add((tangents[a]+tangents[b])*.5f);weights.Add(Average(weights[a],weights[b]));freedom.Add((freedom[a]+freedom[b])*.5f);shell.Add((shell[a]+shell[b])*.5f);progress.Add((progress[a]+progress[b])*.5f);inner.Add((inner[a]+inner[b])*.5f);midpoints.Add(key,index);return index;
            }
            for(int sub=0;sub<submeshes.Length;sub++){
                var next=new List<int>();void Tri(int a,int b,int c){next.Add(a);next.Add(b);next.Add(c);}
                var old=submeshes[sub];for(int t=0;t<old.Length;t+=3){int a=old[t],b=old[t+1],c=old[t+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);
                    if(ab>=0&&bc>=0&&ca>=0){Tri(a,ab,ca);Tri(ab,b,bc);Tri(ca,bc,c);Tri(ab,bc,ca);}
                    else if(ab>=0&&bc>=0){Tri(a,ab,c);Tri(ab,bc,c);Tri(ab,b,bc);}
                    else if(bc>=0&&ca>=0){Tri(b,bc,a);Tri(bc,ca,a);Tri(bc,c,ca);}
                    else if(ca>=0&&ab>=0){Tri(c,ca,b);Tri(ca,ab,b);Tri(ca,a,ab);}
                    else if(ab>=0){Tri(a,ab,c);Tri(ab,b,c);}
                    else if(bc>=0){Tri(b,bc,a);Tri(bc,c,a);}
                    else if(ca>=0){Tri(c,ca,b);Tri(ca,a,b);}
                    else Tri(a,b,c);
                }submeshes[sub]=next.ToArray();
            }
            if(midpoints.Count==0)break;
        }
        mesh.SetVertices(points);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.boneWeights=weights.ToArray();
        for(int sub=0;sub<submeshes.Length;sub++)mesh.SetTriangles(submeshes[sub],sub,false);
    }
    static Mesh Store(Mesh mesh,string path)
    {
        EnsureReadable(mesh);
        // 不把越界索引交给渲染器或原生物理烘焙。
        int count=mesh.vertexCount;
        if(mesh.triangles.Any(i=>i<0||i>=count))throw new InvalidOperationException("衣服网格索引越界："+path);
        if(mesh.boneWeights.Length!=count)throw new InvalidOperationException("衣服权重数量不匹配："+path);
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!existing){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EnsureReadable(existing);
        existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;
        existing.normals=mesh.normals;existing.tangents=mesh.tangents;existing.colors=mesh.colors;
        for(int c=0;c<8;c++){var uv=new List<Vector4>();mesh.GetUVs(c,uv);if(uv.Count>0)existing.SetUVs(c,uv);}
        existing.bindposes=mesh.bindposes;existing.boneWeights=mesh.boneWeights;existing.subMeshCount=mesh.subMeshCount;
        for(int sub=0;sub<mesh.subMeshCount;sub++)existing.SetIndices(mesh.GetIndices(sub),mesh.GetTopology(sub),sub,false);
        existing.bounds=mesh.bounds;existing.name=mesh.name;existing.UploadMeshData(false);EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(mesh);return existing;
    }

    static void EnsureReadable(Mesh mesh)
    {
        if(mesh.isReadable)return;
        var data=new SerializedObject(mesh);
        var readable=data.FindProperty("m_IsReadable");
        if(readable==null)throw new InvalidOperationException("CPU mesh readability unavailable: "+mesh.name);
        readable.boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();
        if(!mesh.isReadable)throw new InvalidOperationException("CPU mesh readability failed: "+mesh.name);
    }

}
