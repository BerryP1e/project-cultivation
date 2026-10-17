using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>为同一 Tripo 人体骨序的长袍生成附加衣骨和渐变蒙皮；不修改源 FBX 或动作。</summary>
public static class OutfitClothBaker
{
    const string Folder = "Assets/Art/Characters/OutfitCloth";
    const int Sectors = 8, Segments = 3;

    public static void Bake(AppearanceDefinition appearance, SkinnedMeshRenderer reference)
    {
        var source = appearance.取模型().GetComponentInChildren<SkinnedMeshRenderer>(true);
        var original = source.sharedMesh;
        if (source.bones.Length != 78 || reference.bones.Length != 78)
            throw new InvalidOperationException("当前衣服预处理只接受已确认的 78 骨人体骨架");
        for (int i = 0; i < 78; i++)
            if (source.bones[i].name != reference.bones[i].name) throw new InvalidOperationException("人体骨序不同");
        var points = original.vertices;
        var originalBinds = original.bindposes;
        Vector3 Rest(int index) => originalBinds[index].inverse.GetColumn(3);
        var centre = Rest(1);
        float waist = Mathf.Min(.49f, Rest(2).z - .025f);
        float floor = appearance.id == "app_goldencore" ? .055f : .095f;
        var specs = new List<AppearanceClothProfile.Bone>();
        var rests = new List<Matrix4x4>();
        var rawRests = new List<Matrix4x4>();
        var binds = reference.sharedMesh.bindposes.ToList();
        void Chain(string name, int parent, Vector3 anchor, float bottom, bool sleeve, bool hair = false)
        {
            float length = Mathf.Max(.06f, anchor.z - bottom) / Segments;
            for (int j = 0; j < Segments; j++)
            {
                var rawRest = Matrix4x4.TRS(anchor + Vector3.back * (length * j), Quaternion.identity, Vector3.one);
                var rest = reference.sharedMesh.bindposes[parent].inverse * originalBinds[parent] * rawRest;
                int previous = j == 0 ? -1 : specs.Count - 1;
                var local = (previous < 0 ? reference.sharedMesh.bindposes[parent] : rests[previous].inverse) * rest;
                specs.Add(new AppearanceClothProfile.Bone {
                    名称 = name + "_" + j, 人体骨索引 = parent, 上级衣骨 = previous,
                    局部位置 = local.GetColumn(3), 局部旋转 = local.rotation,
                    末端偏移 = Vector3.back * length, 衣袖 = sleeve, 头发 = hair
                });
                rests.Add(rest); rawRests.Add(rawRest); binds.Add(rest.inverse);
            }
        }
        for (int sector = 0; sector < Sectors; sector++)
        {
            float angle = sector * Mathf.PI * 2 / Sectors;
            Chain("Hem_" + sector, 1, new Vector3(centre.x + Mathf.Cos(angle) * .075f,
                centre.y + Mathf.Sin(angle) * .075f, waist), floor, false);
        }
        // 每侧宽袖独立一条三节链，固定点在前臂，不把手指/手掌交给衣骨。
        bool wideSleeves = appearance.id != "app_player";
        for (int side = 0; wideSleeves && side < 2; side++)
        {
            var elbow = Rest(side == 0 ? 32 : 56); var hand = Rest(side == 0 ? 36 : 60);
            var anchor = Vector3.Lerp(elbow, hand, .64f);
            float bottom = points.Where(v => Mathf.Sign(v.y - centre.y) == (side == 0 ? 1 : -1)
                && Mathf.Abs(v.y - centre.y) > .16f && v.z < anchor.z)
                .Select(v => v.z).DefaultIfEmpty(anchor.z - .12f).Min();
            Chain(side == 0 ? "Sleeve_L" : "Sleeve_R", side == 0 ? 32 : 56, anchor,
                Mathf.Max(.035f, bottom), true);
        }

        float Distance(Vector3 p, Vector3 a, Vector3 b)
        { var d = b-a; return Vector3.Distance(p, a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.000001f,d.sqrMagnitude))); }
        float Fade(float a, float b, float v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v));
        var weights = original.boneWeights;
        var originalWeights = (BoneWeight[])weights.Clone();
        // 同一张网格仍有独立的手、靴、内裤拓扑岛，不能只按高度把它们划进衣摆。
        var parents=Enumerable.Range(0,points.Length).ToArray();
        int Find(int x){while(parents[x]!=x){parents[x]=parents[parents[x]];x=parents[x];}return x;}
        void Join(int a,int b){parents[Find(b)]=Find(a);}
        var coincident=new Dictionary<Vector3Int,int>();
        for(int i=0;i<points.Length;i++){
            var p=points[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
            if(coincident.TryGetValue(key,out int other))Join(i,other);else coincident.Add(key,i);
        }
        var triangles=original.triangles;
        for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
        var islands=new Dictionary<int,Bounds>();
        for(int i=0;i<points.Length;i++){int group=Find(i);if(islands.TryGetValue(group,out var bounds)){bounds.Encapsulate(points[i]);islands[group]=bounds;}else islands[group]=new Bounds(points[i],Vector3.zero);}
        var protectedIslands=new HashSet<int>();
        foreach(var island in islands){var b=island.Value;
            bool hand=b.min.z>.43f&&b.size.z<.2f&&b.size.x<.13f&&Mathf.Abs(b.center.y-centre.y)>.2f;
            bool boot=b.min.z<.04f&&b.max.z<.2f&&b.size.y<.17f;
            bool trousers=appearance.id=="app_goldencore"&&b.min.z>.14f&&b.max.z<.49f&&b.size.y<.2f&&b.size.x<.15f;
            if(hand||boot||trousers)protectedIslands.Add(island.Key);
        }
        int hems = 0, sleeves = 0;
        for (int n = 0; n < points.Length; n++)
        {
            var p = points[n]; var old = weights[n];
            if(protectedIslands.Contains(Find(n)))continue;
            var candidates = new Dictionary<int, float>();
            void Add(int index, float w) { if (w <= .00001f) return; candidates[index] = candidates.TryGetValue(index, out float v) ? v + w : w; }
            float cloth = 0; int chainA = 0, chainB = 0; float angular = 0;
            float legDistance = Mathf.Min(Distance(p, Rest(3), Rest(5)), Distance(p, Rest(12), Rest(15)));
            // 保留腿/靴和裆部，只有腿外的袍摆获得独立衣骨；腰部平滑过渡。
            float LowerWeight(int index,float w)=>index<=20?w:0;
            float lower=LowerWeight(old.boneIndex0,old.weight0)+LowerWeight(old.boneIndex1,old.weight1)+LowerWeight(old.boneIndex2,old.weight2)+LowerWeight(old.boneIndex3,old.weight3);
            if (p.z < waist && lower>.65f)
            {
                // 金丹内裤/靴已有独立拓扑岛保护，余下低处是长袍，内侧也要连贯跟随。
                float outside = appearance.id=="app_goldencore" ? 1 : Fade(.085f, .13f, legDistance);
                float feet = Mathf.Min(Vector3.Distance(p,Rest(5)),Vector3.Distance(p,Rest(15)));
                float boot = Mathf.Lerp(Fade(.11f,.17f,feet),1,Fade(.13f,.24f,p.z));
                cloth = .92f * Fade(0, .15f, waist-p.z) * outside * boot;
                float angle = Mathf.Repeat(Mathf.Atan2(p.y-centre.y, p.x-centre.x), Mathf.PI*2) * Sectors/(Mathf.PI*2);
                chainA = Mathf.FloorToInt(angle) * Segments;
                chainB = ((Mathf.FloorToInt(angle)+1)%Sectors) * Segments;
                angular = angle-Mathf.Floor(angle);
            }
            int sideIndex = p.y > centre.y ? 0 : 1;
            int upper = sideIndex == 0 ? 31 : 55, forearm = sideIndex == 0 ? 32 : 56, handIndex = sideIndex == 0 ? 36 : 60;
            var armStart = Rest(upper); var armEnd = Rest(handIndex);
            float lateral = Mathf.Abs(p.y-centre.y);
            float handDistance = Vector3.Distance(p, armEnd);
            float ArmWeight(int index,float w)=>index>=30?w:0;
            float arm=ArmWeight(old.boneIndex0,old.weight0)+ArmWeight(old.boneIndex1,old.weight1)+ArmWeight(old.boneIndex2,old.weight2)+ArmWeight(old.boneIndex3,old.weight3);
            var armLine = Vector3.Lerp(armStart, armEnd, Mathf.Clamp01((p.y-armStart.y)/(armEnd.y-armStart.y)));
            float sleeve = wideSleeves && arm>.55f && lateral > .08f && p.z < armLine.z-.03f && handDistance > .065f
                ? .9f*Fade(.08f,.14f,lateral)*Fade(.03f,.075f,armLine.z-p.z)
                    *Fade(.04f,.085f,Distance(p,armStart,armEnd))*Fade(.065f,.11f,handDistance) : 0;
            if (sleeve > cloth)
            { cloth=sleeve; chainA=chainB=Sectors*Segments+sideIndex*Segments; angular=0; sleeves++; }
            else if (cloth > .01f) hems++;
            if (cloth <= .00001f) continue;
            Add(old.boneIndex0, old.weight0*(1-cloth)); Add(old.boneIndex1, old.weight1*(1-cloth));
            Add(old.boneIndex2, old.weight2*(1-cloth)); Add(old.boneIndex3, old.weight3*(1-cloth));
            void BlendChain(int chain, float w)
            {
                float length = specs[chain].末端偏移.magnitude;
                float progress = Mathf.Clamp((rawRests[chain].m23-p.z)/length, 0, Segments-1);
                int first = Mathf.FloorToInt(progress), second = Mathf.Min(first+1, Segments-1);
                float t = progress-first;
                Add(78+chain+first,w*(1-t)); Add(78+chain+second,w*t);
            }
            BlendChain(chainA, cloth*(1-angular)); BlendChain(chainB, cloth*angular);
            var top = candidates.OrderByDescending(x=>x.Value).Take(4).ToArray();
            float sum=top.Sum(x=>x.Value);
            var bw = new BoneWeight();
            if(top.Length>0){bw.boneIndex0=top[0].Key;bw.weight0=top[0].Value/sum;}
            if(top.Length>1){bw.boneIndex1=top[1].Key;bw.weight1=top[1].Value/sum;}
            if(top.Length>2){bw.boneIndex2=top[2].Key;bw.weight2=top[2].Value/sum;}
            if(top.Length>3){bw.boneIndex3=top[3].Key;bw.weight3=top[3].Value/sum;}
            weights[n]=bw;
        }
        if(hems<100||wideSleeves&&sleeves<30)throw new InvalidOperationException(appearance.DisplayName+" 衣服区域未覆盖，停止生成");
        var hairVertices=new List<int>();
        if(wideSleeves){
            // 长发颜色与头/颈权重共同筛选；帽冠、脸、衣领和贴头发根保持原动画。
            var texture=source.sharedMaterials[0].mainTexture;
            var previous=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try{Graphics.Blit(texture,rt);RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
            var uv=original.uv;var head=Rest(26);float rootHeight=head.z+.10f;
            try{for(int i=0;i<points.Length;i++){
                var p=points[i];if(p.z>=rootHeight||p.z<head.z-.38f)continue;
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
            int hairStart=specs.Count;float bottom=hairVertices.Min(i=>points[i].z);
            var anchors=new[]{new Vector3(head.x+.075f,head.y+.04f,rootHeight),new Vector3(head.x+.075f,head.y-.04f,rootHeight),
                new Vector3(head.x-.035f,head.y+.085f,rootHeight),new Vector3(head.x-.035f,head.y-.085f,rootHeight)};
            var labels=new[]{"Hair_Back_L","Hair_Back_R","Hair_Side_L","Hair_Side_R"};
            for(int i=0;i<anchors.Length;i++)Chain(labels[i],26,anchors[i],bottom,false,true);
            foreach(int index in hairVertices){
                var p=points[index];float strength=.9f*Fade(0,.17f,rootHeight-p.z);
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
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Characters", "OutfitCloth");
        var mesh = AppearanceMeshBinding.创建兼容网格(source,reference.sharedMesh,reference.bones,true);
        mesh.name = appearance.id+"-skin"; mesh.bindposes=binds.ToArray();mesh.boneWeights=weights;
        string meshPath=Folder+"/"+appearance.id+"-skin.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing){
            // CopySerialized 单独使用会让团结保留旧 GPU 顶点缓冲，CPU Bake 正常、实际手部却拉丝。
            existing.Clear();existing.indexFormat=mesh.indexFormat;
            existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.tangents=mesh.tangents;
            existing.colors=mesh.colors;
            for(int channel=0;channel<8;channel++){var uv=new List<Vector4>();mesh.GetUVs(channel,uv);if(uv.Count>0)existing.SetUVs(channel,uv);}
            existing.bindposes=mesh.bindposes;existing.boneWeights=mesh.boneWeights;
            existing.subMeshCount=mesh.subMeshCount;
            for(int sub=0;sub<mesh.subMeshCount;sub++)existing.SetIndices(mesh.GetIndices(sub),mesh.GetTopology(sub),sub,false);
            existing.bounds=mesh.bounds;existing.name=mesh.name;existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;
        }
        else AssetDatabase.CreateAsset(mesh,meshPath);
        string path=Folder+"/"+appearance.id+".asset";
        var profile=AssetDatabase.LoadAssetAtPath<AppearanceClothProfile>(path);
        if(!profile){profile=ScriptableObject.CreateInstance<AppearanceClothProfile>();AssetDatabase.CreateAsset(profile,path);}
        profile.蒙皮=mesh;profile.人体骨名=source.bones.Select(x=>x.name).ToArray();profile.衣骨=specs.ToArray();
        profile.长发顶点=hairVertices.ToArray();
        profile.单节限角=10;
        appearance.衣服随动=profile;EditorUtility.SetDirty(profile);EditorUtility.SetDirty(appearance);EditorUtility.SetDirty(mesh);
        Debug.Log("[着装随动] "+appearance.DisplayName+"：衣摆="+hems+" 宽袖="+sleeves+" 长发="+hairVertices.Count+" 附加骨="+specs.Count);
    }
}
