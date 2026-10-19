using System;
using UnityEngine;

/// <summary>人体由原 Animator 控制；外袍、宽袖使用分段随动与连续表面避让，长发使用受限骨链。</summary>
[DefaultExecutionOrder(20000)]
public sealed class AppearanceClothRig : MonoBehaviour
{
    [SerializeField] AppearanceClothProfile 配置;
    [SerializeField] Transform[] 人体骨;
    [SerializeField] Transform[] 衣骨;
    [SerializeField] GameObject 布料节点;
    [SerializeField] GameObject 裤装节点;
    SkinnedMeshRenderer 布料蒙皮;
    Transform[] 布料骨;
    Mesh 布料网格;
    Vector3 布料惯性, 上帧布料位置;
    Vector3[] 碰撞起点, 碰撞终点;
    Vector3[] 布料原顶点, 布料原法线;
    BoneWeight[] 布料权重;
    Matrix4x4[] 布料绑定, 布料变换;
    Vector3[] 布料顶点缓存, 布料法线缓存;
    Vector3[] 布料静止外向;
    int[] 接缝计数;
    float[] 衣摆进度, 衣摆权重, 衣摆相位;
    Vector3[] 衣摆静止外向;
    int[] 表面三角;
    OutfitGarmentShell 袍面;
    OutfitLowerBodyContact 裤靴接触;
    OutfitSurfaceJob 布料表面;
    OutfitLowerBodyPose 下肢姿态;
    Vector3[] 细腿起点,细腿终点;
    Vector3[] 原鞋筒起点,原鞋筒终点;
    float[] 原鞋接触半径;
    Vector3[] 发面原点;
    BoneWeight[] 发面权重;
    Matrix4x4[] 发面绑定,发面矩阵;
    Transform[] 发面骨;
    Quaternion[] 发面候选;
    float[] 发面基准穿入;
    Vector3[] 发面修正, 发面梯度;
    int[] 发面接触数, 发面接触顶点;
    float[] 发骨柔度, 发骨限角, 发面穿入;
    static readonly int[,] 碰撞骨段 = { {1,23}, {3,4}, {4,5}, {12,13}, {13,15}, {31,32}, {32,36}, {55,56}, {56,60} };
    static readonly float[] 碰撞半径 = { .060f, .045f, .035f, .045f, .035f, .022f, .020f, .022f, .020f };
    Vector3[] 末端, 速度;
    Vector3[] 姿态偏移, 姿态速度;
    Quaternion[] 上帧父骨旋转;
    Vector3 姿态上帧位置, 姿态移动速度;
    Vector3 上帧锚点;
    float 上帧时间 = -1;
    YufengFlight 御风来源;
    PlayerAnimationController 动画来源;
    float 地面柔化混合;
    bool 已初始化;
    public float 最大随动偏角 { get; private set; }
    public AppearanceClothProfile 当前配置 => 配置;
    public int 细腿接触次数 => 袍面?.细腿接触次数??0;
    public int 鞋靴接触次数 => 袍面?.鞋靴接触次数??0;
    public float 最大鞋靴推出距离 => 袍面?.最大鞋靴推出距离??0;
    public int 衣骨数 => 衣骨 == null ? 0 : 衣骨.Length;
    public float 原腿距 => 下肢姿态?.原腿距??0;
    public float 当前腿距 => 下肢姿态?.当前腿距??0;
    public float 下肢骨长误差 => 下肢姿态?.骨长误差??0;
    public int 头发骨数
    {
        get { int count=0;if(配置)foreach(var bone in 配置.衣骨)if(bone.头发)count++;return count; }
    }

    public static void 应用(SkinnedMeshRenderer renderer, AppearanceClothProfile profile, Transform[] bodyBones)
    {
        var rig = renderer.GetComponent<AppearanceClothRig>();
        if (rig) rig.清除();
        if (!profile) { renderer.bones = bodyBones; return; }
        if (profile.人体骨名.Length != bodyBones.Length)
            throw new InvalidOperationException("衣服骨架数量不匹配");
        for (int i = 0; i < bodyBones.Length; i++)
            if (!bodyBones[i] || bodyBones[i].name != profile.人体骨名[i])
                throw new InvalidOperationException("衣服骨序不匹配：" + i);
        if (!rig) rig = renderer.gameObject.AddComponent<AppearanceClothRig>();
        rig.配置 = profile;
        rig.御风来源 = renderer.GetComponentInParent<YufengFlight>();
        rig.动画来源 = renderer.GetComponentInParent<PlayerAnimationController>();
        rig.地面柔化混合 = 0;
        rig.人体骨 = (Transform[])bodyBones.Clone();
        rig.下肢姿态=profile.仅保留原鞋?new OutfitLowerBodyPose(profile,rig.人体骨):null;
        rig.衣骨 = new Transform[profile.衣骨.Length];
        var combined = new Transform[bodyBones.Length + profile.衣骨.Length];
        Array.Copy(bodyBones, combined, bodyBones.Length);
        for (int i = 0; i < rig.衣骨.Length; i++)
        {
            var spec = profile.衣骨[i];
            var bone = new GameObject("OutfitCloth_" + spec.名称).transform;
            bone.gameObject.layer = renderer.gameObject.layer;
            bone.SetParent(spec.上级衣骨 < 0 ? bodyBones[spec.人体骨索引] : rig.衣骨[spec.上级衣骨], false);
            bone.localPosition = spec.局部位置;
            bone.localRotation = spec.局部旋转;
            rig.衣骨[i] = combined[bodyBones.Length + i] = bone;
        }
        renderer.sharedMesh = profile.蒙皮;
        renderer.bones = combined;
        if(profile.裤装蒙皮){
            rig.裤装节点=new GameObject("OutfitTrousers");rig.裤装节点.layer=renderer.gameObject.layer;rig.裤装节点.transform.SetParent(renderer.transform,false);
            var trousers=rig.裤装节点.AddComponent<SkinnedMeshRenderer>();trousers.sharedMesh=profile.裤装蒙皮;trousers.sharedMaterial=profile.裤装材质;
            trousers.bones=bodyBones;trousers.rootBone=renderer.rootBone;trousers.localBounds=renderer.localBounds;trousers.updateWhenOffscreen=renderer.updateWhenOffscreen;
            trousers.shadowCastingMode=renderer.shadowCastingMode;trousers.receiveShadows=renderer.receiveShadows;
        }
        rig.创建布料(renderer);
        rig.发面原点=profile.蒙皮.vertices;rig.发面权重=profile.蒙皮.boneWeights;rig.发面绑定=profile.蒙皮.bindposes;rig.发面骨=combined;
        rig.发面矩阵=new Matrix4x4[combined.Length];rig.发面候选=new Quaternion[profile.衣骨.Length];rig.发面基准穿入=new float[profile.长发顶点.Length];
        rig.发面修正=new Vector3[profile.衣骨.Length];rig.发面梯度=new Vector3[profile.衣骨.Length];rig.发面接触数=new int[profile.衣骨.Length];
        rig.发面接触顶点=new int[profile.衣骨.Length];rig.发面穿入=new float[profile.衣骨.Length];
        rig.发骨柔度=new float[profile.衣骨.Length];rig.发骨限角=new float[profile.衣骨.Length];
        for(int i=0;i<profile.衣骨.Length;i++)if(profile.衣骨[i].头发){
            int depth=0;for(int parent=profile.衣骨[i].上级衣骨;parent>=0;parent=profile.衣骨[parent].上级衣骨)depth++;
            float softness=Mathf.Clamp01(depth/2f);
            rig.发骨柔度[i]=Mathf.Lerp(.015f,1,softness*softness);
            rig.发骨限角[i]=Mathf.Lerp(Mathf.Lerp(5,7,profile.长发释放),Mathf.Lerp(75,90,profile.长发释放),Mathf.Pow(softness,1.15f))*Mathf.Deg2Rad;
        }
        rig.enabled = true;
        rig.重置随动();
    }

    /// <summary>面板克隆用自己的骨架，不能直接引用游戏角色的衣骨。</summary>
    public static void 同步预览(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
    {
        if(!source||!target||source.name=="OutfitFabric"||source.name=="OutfitTrousers")return;
        bool wasEnabled=target.enabled;target.enabled=false;
        var rig = source.GetComponent<AppearanceClothRig>();
        var names = rig && rig.配置 ? rig.配置.人体骨名 : null;
        int count = names == null ? source.bones.Length : names.Length;
        var old = target.bones;
        var body = new Transform[count];
        if (old.Length < count) throw new InvalidOperationException("预览人体骨缺失");
        Array.Copy(old, body, count);
        // 无行为的克隆可能还保留旧衣骨；先清理，只清理约定前缀的附加骨根。
        if (!target.GetComponent<AppearanceClothRig>())
        {
            var oldFabric=target.transform.Find("OutfitFabric");
            if(oldFabric)DestroyNode(oldFabric.gameObject);
            var oldTrousers=target.transform.Find("OutfitTrousers");if(oldTrousers)DestroyNode(oldTrousers.gameObject);
            for (int i = count; i < old.Length; i++)
                if (old[i] && old[i].name.StartsWith("OutfitCloth_", StringComparison.Ordinal)
                    && !old[i].parent.name.StartsWith("OutfitCloth_", StringComparison.Ordinal))
                    DestroyNode(old[i].gameObject);
        }
        target.sharedMaterials=source.sharedMaterials;
        应用(target, rig ? rig.配置 : null, body);
        target.sharedMesh = source.sharedMesh;
        target.enabled=wasEnabled;
        var previewRig = target.GetComponent<AppearanceClothRig>();
        if (previewRig) previewRig.enabled = false; // 面板在手动动画之后、Render 之前推进。
    }

    public void 清除()
    {
        下肢姿态?.重置();下肢姿态=null;
        if(布料网格){if(Application.isPlaying)Destroy(布料网格);else DestroyImmediate(布料网格);}
        if(布料节点)DestroyNode(布料节点);
        if(裤装节点)DestroyNode(裤装节点);裤装节点=null;
        布料节点=null;布料蒙皮=null;布料网格=null;
        if (衣骨 != null && 配置)
            for (int i = 0; i < 衣骨.Length; i++)
                if (衣骨[i] && 配置.衣骨[i].上级衣骨 < 0) DestroyNode(衣骨[i].gameObject);
        衣骨 = null; 配置 = null; 人体骨 = null;
        末端 = 速度 = null;
        姿态偏移 = 姿态速度 = null;
        上帧父骨旋转 = null;
        已初始化 = false;
        袍面?.Dispose();袍面 = null;布料表面?.Dispose();布料表面=null;
        裤靴接触?.Dispose();裤靴接触=null;
    }

    static void DestroyNode(GameObject node)
    {
        node.SetActive(false);
        if (Application.isPlaying) {node.name="Retired_"+node.name;Destroy(node);} else DestroyImmediate(node);
    }

    void OnEnable() { 已初始化 = false; }
    void OnDestroy(){袍面?.Dispose();裤靴接触?.Dispose();布料表面?.Dispose();if(布料网格){if(Application.isPlaying)Destroy(布料网格);else DestroyImmediate(布料网格);}}
    void LateUpdate()
    {
        if (!配置 || 衣骨 == null) return;
        // 预览自己推进手动动画，真实角色用 scaled time，暂停时衣服也暂停。
        推进(Time.deltaTime, Time.time);
    }

    public void 重置随动()
    {
        if (!配置 || 衣骨 == null) return;
        下肢姿态?.重置();
        袍面?.Reset();
        布料表面?.重置();
        裤靴接触?.Reset();
        if (末端 == null || 末端.Length != 衣骨.Length)
        { 末端 = new Vector3[衣骨.Length]; 速度 = new Vector3[衣骨.Length]; }
        for (int i = 0; i < 衣骨.Length; i++)
        {
            衣骨[i].localRotation = 配置.衣骨[i].局部旋转;
            末端[i] = 衣骨[i].TransformPoint(配置.衣骨[i].末端偏移);
            速度[i] = Vector3.zero;
        }
        上帧锚点 = 人体骨[1].position;
        姿态偏移 = new Vector3[衣骨.Length];
        姿态速度 = new Vector3[衣骨.Length];
        上帧父骨旋转 = new Quaternion[衣骨.Length];
        for(int i=0;i<衣骨.Length;i++)上帧父骨旋转[i]=衣骨[i].parent.rotation;
        姿态上帧位置 = transform.position;
        姿态移动速度 = Vector3.zero;
        最大随动偏角 = 0;
        上帧时间 = -1;
        已初始化 = true;
        布料惯性=Vector3.zero;上帧布料位置=transform.position;
        更新布料表面(0,0);
    }

    void 创建布料(SkinnedMeshRenderer renderer)
    {
        if(!配置.布料蒙皮)return;
        布料节点=new GameObject("OutfitFabric");布料节点.layer=renderer.gameObject.layer;
        布料节点.transform.SetParent(renderer.transform,false);
        布料蒙皮=布料节点.AddComponent<SkinnedMeshRenderer>();布料蒙皮.sharedMesh=配置.布料蒙皮;
        布料骨=renderer.bones;布料蒙皮.bones=布料骨;布料蒙皮.rootBone=renderer.rootBone;布料蒙皮.enabled=false;
        var output=new GameObject("OutfitFabricSurface");output.layer=renderer.gameObject.layer;output.transform.SetParent(布料节点.transform,false);
        布料网格=Instantiate(配置.布料蒙皮);布料网格.name="OutfitFabricRuntime";布料网格.MarkDynamic();
        布料原顶点=配置.布料蒙皮.vertices;布料原法线=配置.布料蒙皮.normals;
        布料权重=配置.布料蒙皮.boneWeights;布料绑定=配置.布料蒙皮.bindposes;布料变换=new Matrix4x4[布料绑定.Length];
        布料顶点缓存=new Vector3[布料原顶点.Length];布料法线缓存=new Vector3[布料原顶点.Length];
        布料静止外向=new Vector3[布料原顶点.Length*9];
        var restPositions=new Vector3[布料绑定.Length];
        for(int b=0;b<restPositions.Length;b++)restPositions[b]=布料绑定[b].inverse.GetColumn(3);
        Vector3 Rest(int bone)=>restPositions[bone];
        for(int i=0;i<布料原顶点.Length;i++)for(int c=0;c<9;c++){
            var a=Rest(碰撞骨段[c,0]);
            var b=Rest(碰撞骨段[c,1]);var axis=b-a;
            var normal=布料原顶点[i]-a-axis*Mathf.Clamp01(Vector3.Dot(布料原顶点[i]-a,axis)/Mathf.Max(.000001f,axis.sqrMagnitude));
            布料静止外向[i*9+c]=normal.sqrMagnitude>.000001f?normal.normalized:布料原法线[i].normalized;
        }
        接缝计数=new int[布料原顶点.Length];
        for(int i=0;i<布料原顶点.Length;i++)接缝计数[配置.布料同点[i]]++;
        衣摆进度=new float[布料原顶点.Length];衣摆权重=new float[布料原顶点.Length];衣摆相位=new float[布料原顶点.Length];
        衣摆静止外向=new Vector3[布料原顶点.Length];表面三角=配置.布料蒙皮.triangles;
        var waist=Rest(21);
        for(int i=0;i<布料原顶点.Length;i++){
            void Add(int bone,float value){
                int index=bone-人体骨.Length;
                if(index<0||index>=配置.衣骨.Length||配置.衣骨[index].衣袖||配置.衣骨[index].头发)return;
                int depth=0;for(int parent=配置.衣骨[index].上级衣骨;parent>=0;parent=配置.衣骨[parent].上级衣骨)depth++;
                衣摆权重[i]+=value;衣摆进度[i]+=value*Mathf.Clamp01(depth/2f);
            }
            var w=布料权重[i];Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
            if(衣摆权重[i]>.00001f)衣摆进度[i]/=衣摆权重[i];
            if(配置.袍面覆盖!=null&&配置.袍面覆盖.Length==布料原顶点.Length&&配置.袍面进度.Length==布料原顶点.Length){衣摆权重[i]=配置.袍面覆盖[i];衣摆进度[i]=配置.袍面进度[i];}
            var radial=布料原顶点[i]-waist;radial.z=0;
            衣摆静止外向[i]=radial.sqrMagnitude>.000001f?radial.normalized:Vector3.right;
            衣摆相位[i]=Mathf.Atan2(radial.y,radial.x);
        }
        bool thinLegs=配置.鞋口锚点!=null&&配置.鞋口锚点.Length==2;
        细腿起点=thinLegs?new Vector3[6]:null;细腿终点=thinLegs?new Vector3[6]:null;
        bool boots=配置.仅保留原鞋&&配置.原鞋筒锚点.Length==2;
        int bootCount=boots?(配置.原鞋掌锚点.Length==2?4:2):0;
        原鞋筒起点=boots?new Vector3[bootCount]:null;原鞋筒终点=boots?new Vector3[bootCount]:null;原鞋接触半径=boots?new float[bootCount]:null;
        for(int i=0;i<bootCount;i++)原鞋接触半径[i]=i<2?配置.原鞋筒半径[i]:配置.原鞋掌半径[i-2];
        袍面=配置.姿态空间随动?new OutfitGarmentShell(布料原顶点,表面三角,配置.布料同点,衣摆权重,衣摆进度,配置.布料顶点约束,衣摆静止外向,配置.袍面覆盖!=null&&配置.袍面覆盖.Length==布料原顶点.Length,配置.闭襟覆盖.Length==布料原顶点.Length?配置.闭襟覆盖:null):null;
        裤靴接触=配置.裤装蒙皮?new OutfitLowerBodyContact(配置):null;
        布料表面=new OutfitSurfaceJob(配置,布料原顶点,布料原法线,布料权重,布料静止外向,衣摆静止外向,衣摆权重,衣摆进度,衣摆相位,接缝计数,表面三角,布料绑定.Length);
        output.AddComponent<MeshFilter>().sharedMesh=布料网格;
        var surface=output.AddComponent<MeshRenderer>();surface.sharedMaterials=renderer.sharedMaterials;
        surface.shadowCastingMode=renderer.shadowCastingMode;surface.receiveShadows=renderer.receiveShadows;
        碰撞起点=new Vector3[碰撞半径.Length];碰撞终点=new Vector3[碰撞半径.Length];
        上帧布料位置=transform.position;
    }

    void 更新布料表面(float dt,float clock)
    {
        if(!布料蒙皮||!布料网格)return;
        var matrix=布料节点.transform.localToWorldMatrix;var inverse=matrix.inverse;
        var bones=布料骨;
        for(int i=0;i<布料变换.Length;i++)布料变换[i]=inverse*bones[i].localToWorldMatrix*布料绑定[i];
        var points=布料顶点缓存;var normals=布料法线缓存;
        float scale=Mathf.Abs(transform.lossyScale.x);
        var delta=transform.position-上帧布料位置;上帧布料位置=transform.position;
        var lag=dt>0?Vector3.ClampMagnitude(-delta/Mathf.Max(dt,.008f)*.004f,.025f*scale):Vector3.zero;
        布料惯性=Vector3.Lerp(布料惯性,lag,1-Mathf.Exp(-12*Mathf.Max(0,dt)));
        for(int i=0;i<碰撞半径.Length;i++){碰撞起点[i]=人体骨[碰撞骨段[i,0]].position;碰撞终点[i]=人体骨[碰撞骨段[i,1]].position;}
        if(细腿起点!=null)for(int side=0;side<2;side++){
            int thigh=side==0?3:12,calf=side==0?4:13,foot=配置.鞋口骨索引.Length==2?配置.鞋口骨索引[side]:side==0?5:15;
            细腿起点[side*2]=人体骨[thigh].position;细腿终点[side*2]=人体骨[calf].position;
            细腿起点[side*2+1]=人体骨[calf].position;细腿终点[side*2+1]=人体骨[foot].TransformPoint(配置.鞋口锚点[side]);
            细腿起点[4+side]=细腿终点[side*2+1];细腿终点[4+side]=配置.鞋趾锚点.Length==2?人体骨[side==0?5:15].TransformPoint(配置.鞋趾锚点[side]):人体骨[side==0?6:16].position;
        }
        布料表面.准备(points,布料变换,matrix,碰撞起点,碰撞终点,碰撞半径,细腿起点,细腿终点,scale,布料惯性,姿态移动速度,dt,clock);
        if(原鞋筒起点!=null)for(int side=0;side<2;side++){
            原鞋筒起点[side]=人体骨[side==0?4:13].TransformPoint(配置.原鞋筒锚点[side]);原鞋筒终点[side]=人体骨[side==0?5:15].position;
            if(原鞋筒起点.Length==4){
                原鞋筒起点[side+2]=原鞋筒终点[side+2]=人体骨[side==0?5:15].TransformPoint(配置.原鞋掌锚点[side]);
                // 鞋底只补地面裙口接触；御风脚已收在裙下，不让保守包围球撑穿外层袍片。
                bool flying=(御风来源&&御风来源.御风流程中)||(动画来源&&动画来源.外部驱动御风);
                原鞋接触半径[side+2]=flying?0:配置.原鞋掌半径[side];
            }
        }
        袍面?.Solve(points,matrix,人体骨[21].localToWorldMatrix,matrix*布料变换[21],碰撞起点,碰撞终点,碰撞半径,scale,姿态移动速度,dt,细腿起点,细腿终点,配置.细腿半径,配置.鞋靴半径,false,配置.仅保留原鞋,原鞋筒起点,原鞋筒终点,原鞋接触半径,配置.裤装蒙皮);
        布料表面.接缝(points);
        裤靴接触?.Solve(points,matrix,人体骨,scale);
        布料表面.完成(points,normals);
        布料网格.vertices=points;布料网格.normals=normals;布料网格.RecalculateBounds();
    }

    public void 推进(float dt, float clock)
    {
        if (!配置 || 衣骨 == null) return;
        if (!已初始化 || (人体骨[1].position - 上帧锚点).sqrMagnitude > 4 || dt > .12f
            || clock < 上帧时间) { 重置随动(); 上帧时间 = clock; return; }
        if (dt <= 0) return;
        下肢姿态?.推进(dt,(御风来源&&御风来源.御风流程中)||(动画来源&&动画来源.外部驱动御风),动画来源&&(动画来源.动作播放中||动画来源.闪避动作中));
        if(配置.姿态空间随动){
            推进姿态随动(dt,clock);
            更新布料表面(dt,clock);
            上帧锚点=人体骨[1].position;上帧时间=clock;return;
        }
        var carried=(人体骨[1].position-上帧锚点)*.85f;
        for(int i=0;i<末端.Length;i++)末端[i]+=carried;
        // 小步弹簧在低帧率下保持稳定；每节局部姿态从静止值起算，避免累计旋转。
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 90)), 1, 12);
        float h = dt / steps;
        float scale = Mathf.Abs(transform.lossyScale.x);
        for (int step = 0; step < steps; step++)
            for (int i = 0; i < 衣骨.Length; i++)
            {
                var bone = 衣骨[i]; var spec = 配置.衣骨[i];
                float frequency=spec.头发?6:5;
                float spring=Mathf.Pow(frequency*Mathf.PI*2,2);
                float retention=Mathf.Exp(-(spec.头发?1:配置.阻尼)*frequency*Mathf.PI*4*h);
                bone.localRotation = spec.局部旋转;
                var head = bone.position;
                var animatedRest = bone.TransformVector(spec.末端偏移);
                var rest=spec.头发?animatedRest:Vector3.Slerp(animatedRest,Vector3.down*animatedRest.magnitude,spec.衣袖?.1f:.8f);
                var previous = 末端[i];
                // 风只作小幅随动，不移动衣服根部，也不改变人体骨骼。
                var wind = new Vector3(Mathf.Sin(clock * 1.3f + i * .7f), .12f, Mathf.Cos(clock * .9f + i * .4f))
                    * 配置.微风强度 * scale;
                var predicted = previous + 速度[i] * (h * retention)
                    + (head + rest + wind * .08f - previous) * spring * (h * h);
                var end=predicted;
                // 长发更收敛，根部仍固定在头骨；重复投影兼顾段长、限角和身体避让。
                for(int iteration=0;iteration<3;iteration++){
                    end = 身体约束(end, spec.衣袖, spec.头发, scale);
                    var direction = Vector3.RotateTowards(animatedRest, end - head, (spec.头发?6:spec.衣袖?12:45) * Mathf.Deg2Rad, 0);
                    end=head+direction.normalized*animatedRest.magnitude;
                }
                bone.rotation = Quaternion.FromToRotation(animatedRest, end - head) * bone.rotation;
                速度[i] = Vector3.ClampMagnitude((end - previous) / h,scale*(spec.头发?.8f:2));
                末端[i] = end;
            }
        更新布料表面(dt,clock);
        上帧锚点 = 人体骨[1].position;
        上帧时间 = clock;
    }

    // 弹簧状态保存在每节父骨的姿态空间，Animator 的侧身/前倾会完整携带衣型。
    // 只模拟相对该姿态的微小偏转，不再把整个罩袍拉向世界重力方向。
    void 推进姿态随动(float dt,float clock)
    {
        最大随动偏角=0;
        var movement=(transform.position-姿态上帧位置)/dt;
        姿态上帧位置=transform.position;
        姿态移动速度=Vector3.Lerp(姿态移动速度,Vector3.ClampMagnitude(movement,20),1-Mathf.Exp(-10*dt));
        bool flying=(御风来源&&御风来源.御风流程中)||(动画来源&&动画来源.外部驱动御风);
        // 进入飞行立即恢复已验收的衣服参数；地面逐渐释放，避免起停跳变。
        地面柔化混合=flying?0:Mathf.Lerp(地面柔化混合,配置.地面柔化*Mathf.Clamp01(姿态移动速度.magnitude/1.5f),1-Mathf.Exp(-6*dt));
        int steps=Mathf.Clamp(Mathf.CeilToInt(dt/(1f/120)),1,16);
        float h=dt/steps;
        float scale=Mathf.Abs(transform.lossyScale.x);
        for(int i=0;i<衣骨.Length;i++){
            var bone=衣骨[i];var spec=配置.衣骨[i];
            if(spec.头发){推进长发(i,dt,clock,scale);continue;}
            var baseRotation=spec.局部旋转;
            bone.localRotation=baseRotation;
            if(spec.衣袖&&spec.上级衣骨<0&&布料绑定!=null){
                // 袖根位置随前臂，宽袖的垂向随躯干：T 姿势手臂落下时不能把袖摆卷向背后。
                var meshDirection=布料绑定[人体骨.Length+i].inverse.MultiplyVector(spec.末端偏移);
                var chestDirection=布料绑定[23].MultiplyVector(meshDirection);
                var sleeveTarget=人体骨[23].TransformVector(chestDirection);
                var armDirection=bone.TransformVector(spec.末端偏移);
                sleeveTarget=Vector3.Slerp(sleeveTarget,armDirection,.15f);
                baseRotation=Quaternion.Inverse(bone.parent.rotation)*Quaternion.FromToRotation(armDirection,sleeveTarget)*bone.rotation;
                bone.localRotation=baseRotation;
            }
            var rest=baseRotation*spec.末端偏移;
            float length=rest.magnitude;if(length<.000001f)continue;
            int depth=0;for(int parent=spec.上级衣骨;parent>=0;parent=配置.衣骨[parent].上级衣骨)depth++;
            bool hem=!spec.头发&&!spec.衣袖;
            float softness=Mathf.Clamp01(depth/2f);
            // 腰口稳固，中段和下摆逐渐释放，不能把三节都锁成同一块板。
            float angle=hem?Mathf.Lerp(3,配置.衣摆末端限角,Mathf.Pow(softness,1.4f)):
                Mathf.Min(配置.单节限角,spec.头发?2+depth:3+depth*2);
            angle*=1+地面柔化混合*softness*.12f;
            float radius=length*Mathf.Sin(angle*Mathf.Deg2Rad);
            var dragWorld=hem?Vector3.zero:-姿态移动速度*(spec.头发?.012f:.014f);
            var drag=bone.parent.InverseTransformVector(dragWorld);
            drag-=rest*Vector3.Dot(drag,rest)/(length*length);
            var breeze=new Vector3(Mathf.Sin(clock*2.1f+i*.7f-depth*.6f),.12f,Mathf.Cos(clock*1.6f+i*.4f-depth*.8f));
            var target=drag+breeze*(配置.微风强度*(hem?Mathf.Lerp(.015f,.22f,softness):.015f));
            if(hem){
                // 目标是向下并略向后弯落；切向投影后退速度会产生向上分量，不能当作衣摆风向。
                var horizontal=Vector3.ProjectOnPlane(姿态移动速度,Vector3.up);
                var hanging=(Vector3.down-horizontal*.065f).normalized;
                var posed=bone.parent.TransformDirection(rest).normalized;
                // 袍身前后围合：前襟由腰线携带，后片才有明显拖曳，侧片连续过渡。
                // 不能再让全部扇区朝同一个后下方向倒伏。
                float panel=Mathf.Cos((i/3)*Mathf.PI*.25f);
                float drape=Mathf.Lerp(.10f,1,Mathf.SmoothStep(0,1,(panel+1)*.5f));
                var desired=Vector3.Slerp(posed,hanging,配置.衣摆下垂*softness*drape);
                target+=bone.parent.InverseTransformDirection(desired)*length-rest;
                var carried=Quaternion.Inverse(bone.parent.rotation)*上帧父骨旋转[i]*rest;
                target+=(carried-rest)*Mathf.Lerp(.1f,.75f,softness);
            }
            target=Vector3.ClampMagnitude(target,radius*.85f);
            var restWorld=bone.parent.TransformPoint(spec.局部位置+rest);
            var targetWorld=bone.parent.TransformPoint(spec.局部位置+rest+target);
            // 避让只修正弹簧目标，不能反复向状态注入推力，导致停步后卡在限角上。
            var baseline=身体约束(restWorld,spec.衣袖,spec.头发,scale)-restWorld;
            var collision=身体约束(targetWorld,spec.衣袖,spec.头发,scale)-targetWorld-baseline;
            target=Vector3.ClampMagnitude(target+Vector3.ClampMagnitude(bone.parent.InverseTransformVector(collision),radius*.2f),radius*.85f);
            float frequency=hem?Mathf.Lerp(7,2.3f,softness):Mathf.Clamp(配置.回弹频率,3,20);
            frequency*=1-地面柔化混合*softness*.12f;
            float omega=2*Mathf.PI*frequency;
            for(int step=0;step<steps;step++){
            姿态速度[i]+=(target-姿态偏移[i])*(omega*omega*h);
            姿态速度[i]*=Mathf.Exp(-2*Mathf.Max(.5f,配置.阻尼)*(1-地面柔化混合*softness*.06f)*omega*h);
            var offset=Vector3.ClampMagnitude(姿态偏移[i]+姿态速度[i]*h,radius);
            // offset 球半径为 length*sin(angle)，归一化后自然位于限角锥内。
            // 不用 RotateTowards 对每个微小步进投影，避免小角舍入把末节状态反复清零。
            var direction=(rest+offset).normalized*length;
            var next=direction-rest;
            姿态速度[i]=Vector3.ClampMagnitude((next-姿态偏移[i])/h,length*3);
            姿态偏移[i]=next;
            bone.localRotation=Quaternion.FromToRotation(rest,direction)*baseRotation;
            最大随动偏角=Mathf.Max(最大随动偏角,Quaternion.Angle(baseRotation,bone.localRotation));
            }
            上帧父骨旋转[i]=bone.parent.rotation;
        }
        约束长发表面(scale);
    }

    void 约束长发表面(float scale)
    {
        if(配置.长发顶点.Length==0||发面矩阵==null)return;
        var torsoStart=人体骨[1].position;var torsoAxis=人体骨[23].position-torsoStart;
        var shoulderStart=人体骨[31].position;var shoulderAxis=人体骨[55].position-shoulderStart;
        float torsoDenominator=Mathf.Max(1e-8f,torsoAxis.sqrMagnitude),shoulderDenominator=Mathf.Max(1e-8f,shoulderAxis.sqrMagnitude);
        void Matrices(){for(int b=0;b<发面骨.Length;b++)发面矩阵[b]=发面骨[b].localToWorldMatrix*发面绑定[b];}
        Vector3 Skin(int i){var w=发面权重[i];var p=发面原点[i];return 发面矩阵[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+发面矩阵[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+发面矩阵[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+发面矩阵[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;}
        float Depth(Vector3 p,out Vector3 normal){
            var torsoDelta=p-(torsoStart+torsoAxis*Mathf.Clamp01(Vector3.Dot(p-torsoStart,torsoAxis)/torsoDenominator));
            var shoulderDelta=p-(shoulderStart+shoulderAxis*Mathf.Clamp01(Vector3.Dot(p-shoulderStart,shoulderAxis)/shoulderDenominator));
            float torso=.082f*scale-torsoDelta.magnitude,shoulder=.060f*scale-shoulderDelta.magnitude;
            normal=(torso>=shoulder?torsoDelta:shoulderDelta).normalized;
            return Mathf.Max(0,Mathf.Max(torso,shoulder));
        }
        for(int i=0;i<衣骨.Length;i++)if(配置.衣骨[i].头发){发面候选[i]=衣骨[i].localRotation;衣骨[i].localRotation=配置.衣骨[i].局部旋转;}
        Matrices();for(int n=0;n<配置.长发顶点.Length;n++)发面基准穿入[n]=Depth(Skin(配置.长发顶点[n]),out _);
        for(int i=0;i<衣骨.Length;i++)if(配置.衣骨[i].头发)衣骨[i].localRotation=发面候选[i];
        // 只沿接触面的法线纠正相关关节，不再因一个顶点接触就把整束九次拉回静止姿态。
        // 蒙皮对关节转角的导数包含父链；柔度使发根稳固，发中/发梢承担主要避让。
        for(int pass=0;pass<6;pass++){
            Matrices();Array.Clear(发面修正,0,发面修正.Length);Array.Clear(发面接触数,0,发面接触数.Length);bool contact=false;
            Array.Clear(发面穿入,0,发面穿入.Length);
            // 同一关节的密集重合顶点只取最深接触，避免几百个同向约束重复求导。
            for(int n=0;n<配置.长发顶点.Length;n++){
                int v=配置.长发顶点[n];float depth=Depth(Skin(v),out _)-发面基准穿入[n]-.005f;if(depth<=.00025f*scale)continue;
                var w=发面权重[v];int selected=-1;float strongest=0;
                void Select(int b,float weight){if(b>=人体骨.Length&&weight>strongest&&配置.衣骨[b-人体骨.Length].头发){selected=b-人体骨.Length;strongest=weight;}}
                Select(w.boneIndex0,w.weight0);Select(w.boneIndex1,w.weight1);Select(w.boneIndex2,w.weight2);Select(w.boneIndex3,w.weight3);
                if(selected>=0&&depth>发面穿入[selected]){发面穿入[selected]=depth;发面接触顶点[selected]=v;}
            }
            for(int selected=0;selected<衣骨.Length;selected++){
                float depth=发面穿入[selected];if(depth<=0)continue;int v=发面接触顶点[selected];Depth(Skin(v),out var normal);
                Array.Clear(发面梯度,0,发面梯度.Length);
                var w=发面权重[v];
                void Gradient(int b,float weight){
                    if(weight<.001f||b<人体骨.Length)return;int i=b-人体骨.Length;if(!配置.衣骨[i].头发)return;
                    var point=发面矩阵[b].MultiplyPoint3x4(发面原点[v]);
                    for(;i>=0;i=配置.衣骨[i].上级衣骨)发面梯度[i]+=Vector3.Cross(point-衣骨[i].position,normal)*weight;
                }
                Gradient(w.boneIndex0,w.weight0);Gradient(w.boneIndex1,w.weight1);Gradient(w.boneIndex2,w.weight2);Gradient(w.boneIndex3,w.weight3);
                float denominator=0;for(int i=0;i<衣骨.Length;i++)denominator+=发面梯度[i].sqrMagnitude*发骨柔度[i];
                if(denominator<1e-10f)continue;
                for(int i=0;i<衣骨.Length;i++)if(发面梯度[i].sqrMagnitude>1e-12f){发面修正[i]+=发面梯度[i]*(depth*发骨柔度[i]/denominator);发面接触数[i]++;}
                contact=true;
            }
            if(!contact)break;
            for(int i=0;i<衣骨.Length;i++)if(发面接触数[i]>0){
                var bone=衣骨[i];var correction=Vector3.ClampMagnitude(发面修正[i]*(.85f/发面接触数[i]),.14f);
                var rotation=Quaternion.AngleAxis(correction.magnitude*Mathf.Rad2Deg,correction)*bone.rotation;
                var local=Quaternion.Inverse(bone.parent.rotation)*rotation;
                var rest=配置.衣骨[i].局部旋转*配置.衣骨[i].末端偏移;
                var direction=限定长发方向(rest,local*配置.衣骨[i].末端偏移,发骨限角[i]);
                bone.localRotation=Quaternion.FromToRotation(rest,direction)*配置.衣骨[i].局部旋转;
            }
        }
        for(int i=0;i<衣骨.Length;i++)if(配置.衣骨[i].头发){
            var rest=配置.衣骨[i].局部旋转*配置.衣骨[i].末端偏移;var next=衣骨[i].localRotation*配置.衣骨[i].末端偏移-rest;
            var correction=next-姿态偏移[i];
            // 只消除继续钻入身体的速度，保留沿表面和离开接触面的摆动。
            if(correction.sqrMagnitude>1e-10f&&Vector3.Dot(姿态速度[i],correction)<0)姿态速度[i]-=Vector3.Project(姿态速度[i],correction);
            姿态偏移[i]=next;
            上帧父骨旋转[i]=衣骨[i].parent.rotation;
        }
    }

    static Vector3 限定长发方向(Vector3 rest,Vector3 candidate,float angle)
    {
        float length=rest.magnitude;if(length<1e-6f||candidate.sqrMagnitude<1e-12f)return rest;
        var axis=rest/length;var direction=candidate.normalized;float cosine=Mathf.Cos(angle);
        if(Vector3.Dot(axis,direction)<cosine){
            var tangent=Vector3.ProjectOnPlane(direction,axis);
            if(tangent.sqrMagnitude<1e-12f){tangent=Vector3.Cross(axis,Vector3.up);if(tangent.sqrMagnitude<1e-12f)tangent=Vector3.Cross(axis,Vector3.right);}
            direction=axis*cosine+tangent.normalized*Mathf.Sin(angle);
        }
        return direction*length;
    }

    // 独立长发分支：发根跟头，越接近发梢越柔软；不改变任何人体骨、衣摆或宽袖状态。
    void 推进长发(int index,float dt,float clock,float scale)
    {
        var bone=衣骨[index];var spec=配置.衣骨[index];
        bone.localRotation=spec.局部旋转;
        var rest=spec.局部旋转*spec.末端偏移;
        float length=rest.magnitude;if(length<.000001f)return;
        int depth=0;for(int parent=spec.上级衣骨;parent>=0;parent=配置.衣骨[parent].上级衣骨)depth++;
        float softness=Mathf.Clamp01(depth/2f);
        float release=配置.长发释放;
        float angle=发骨限角[index];
        var drag=bone.parent.InverseTransformVector(-姿态移动速度*Mathf.Lerp(.0006f,Mathf.Lerp(.024f,.032f,release),softness));
        drag-=rest*Vector3.Dot(drag,rest)/(length*length);
        var transport=Quaternion.Inverse(bone.parent.rotation)*上帧父骨旋转[index];
        // 将上帧状态携入当前父骨空间，保留转头/转弯的惯性，而不是强迫下节立即跟齐。
        float inertia=Mathf.Lerp(.08f,.92f,softness);
        姿态偏移[index]=限定长发方向(rest,Vector3.Lerp(rest+姿态偏移[index],transport*(rest+姿态偏移[index]),inertia),angle)-rest;
        姿态速度[index]=Vector3.Lerp(姿态速度[index],transport*姿态速度[index],inertia);
        float phase=(index/3)*1.7f;
        var breeze=new Vector3(Mathf.Sin(clock*3.1f+phase-depth*.65f),Mathf.Sin(clock*2.3f+phase*.7f-depth*.8f),Mathf.Cos(clock*2.7f+phase-depth*.6f));
        breeze-=rest*Vector3.Dot(breeze,rest)/(length*length);
        var target=drag+breeze*(length*Mathf.Lerp(.006f,Mathf.Lerp(.35f,.50f,release),softness*softness));
        // 整体气流趋势与局部发梢波动分开：发根仍跟头，下面逐段迎合向后气流。
        float airflow=Mathf.Clamp01(姿态移动速度.magnitude/6)*softness*Mathf.Lerp(.65f,.80f,release);
        if(airflow>0){
            var posed=bone.parent.TransformVector(rest);
            var flow=(-姿态移动速度.normalized+Vector3.down*.20f).normalized;
            var desired=Vector3.Slerp(posed.normalized,flow,airflow)*posed.magnitude;
            target+=bone.parent.InverseTransformVector(desired)-rest;
        }
        target=限定长发方向(rest,rest+target,angle*.9f)-rest;
        float frequency=depth==0?5:depth==1?Mathf.Lerp(1.6f,1.35f,release):Mathf.Lerp(.9f,.72f,release);
        float damping=depth==0?.65f:depth==1?.34f:Mathf.Lerp(.28f,.23f,release);
        float omega=2*Mathf.PI*frequency;
        int steps=Mathf.Clamp(Mathf.CeilToInt(dt/(1f/120)),1,16);float h=dt/steps;
        for(int step=0;step<steps;step++){
            姿态速度[index]+=(target-姿态偏移[index])*(omega*omega*h);
            姿态速度[index]*=Mathf.Exp(-2*damping*omega*h);
            var direction=限定长发方向(rest,rest+姿态偏移[index]+姿态速度[index]*h,angle);
            var world=bone.position+bone.parent.TransformVector(direction);
            var restWorld=bone.position+bone.parent.TransformVector(rest);
            var contact=身体约束(world,false,true,scale)-world-(身体约束(restWorld,false,true,scale)-restWorld);
            // 受限避让肩背/颈部，避免碰撞修正反复注入弹簧能量。
            direction=限定长发方向(rest,direction+bone.parent.InverseTransformVector(contact),angle);
            var next=direction-rest;
            // 保留切向动量；不要把每次限长/接触投影都重新解释为下一步的弹簧速度。
            姿态速度[index]=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(姿态速度[index],direction.normalized),length*6);
            var correction=next-(姿态偏移[index]+姿态速度[index]*h);
            if(correction.sqrMagnitude>1e-10f&&Vector3.Dot(姿态速度[index],correction)<0)姿态速度[index]-=Vector3.Project(姿态速度[index],correction);
            姿态偏移[index]=next;
            bone.localRotation=Quaternion.FromToRotation(rest,direction)*spec.局部旋转;
            最大随动偏角=Mathf.Max(最大随动偏角,Quaternion.Angle(spec.局部旋转,bone.localRotation));
        }
        上帧父骨旋转[index]=bone.parent.rotation;
    }

    Vector3 身体约束(Vector3 point, bool sleeve, bool hair, float scale)
    {
        Vector3 Project(Vector3 p,Vector3 a,Vector3 b,float radius){
            Vector3? outward=null;
            if(配置.姿态空间随动){
                var reference=Vector3.Cross(b-a,transform.right);
                if(reference.sqrMagnitude<.000001f)reference=Vector3.Cross(b-a,transform.forward);
                outward=reference.normalized;
            }
            return 胶囊外(p,a,b,radius,outward);
        }
        point = Project(point, 人体骨[1].position, 人体骨[23].position, (hair?.082f:.06f) * scale);
        if(hair){
            var up=(人体骨[26].position-人体骨[24].position).normalized;
            point=Project(point,人体骨[24].position,人体骨[26].position+up*(.12f*scale),.075f*scale);
            point=Project(point,人体骨[31].position,人体骨[55].position,.06f*scale);
            point=Project(point,人体骨[31].position,人体骨[36].position,.033f*scale);
            point=Project(point,人体骨[55].position,人体骨[60].position,.033f*scale);
        }
        else if (!sleeve)
        {
            point = Project(point, 人体骨[3].position, 人体骨[4].position, .035f * scale);
            point = Project(point, 人体骨[4].position, 人体骨[5].position, .03f * scale);
            point = Project(point, 人体骨[12].position, 人体骨[13].position, .035f * scale);
            point = Project(point, 人体骨[13].position, 人体骨[15].position, .03f * scale);
            point = Project(point, 人体骨[5].position, 人体骨[6].position, .06f * scale);
            point = Project(point, 人体骨[15].position, 人体骨[16].position, .06f * scale);
        }
        else
        {
            point = Project(point, 人体骨[31].position, 人体骨[36].position, .025f * scale);
            point = Project(point, 人体骨[55].position, 人体骨[60].position, .025f * scale);
        }
        return point;
    }

    static Vector3 胶囊外(Vector3 p, Vector3 a, Vector3 b, float radius, Vector3? referenceDirection=null)
    {
        var axis = b - a;
        var closest = a + axis * Mathf.Clamp01(Vector3.Dot(p - a, axis) / Mathf.Max(.000001f, axis.sqrMagnitude));
        var offset = p - closest;
        float distance = offset.magnitude;
        if(distance>=radius)return p;
        var fallback=Vector3.Cross(axis,Vector3.right).normalized;
        if(fallback.sqrMagnitude<.5f)fallback=Vector3.forward;
        // 固定的衣型外向随人体骨旋转，避免轴心附近每帧凭浮点噪声重新选择哪一侧。
        if(referenceDirection.HasValue&&referenceDirection.Value.sqrMagnitude>.5f)fallback=referenceDirection.Value.normalized;
        // 深入轴心时保持连续的外推方向，不能让浮点噪声把法线瞬间翻向另一侧。
        var normal=distance>.00001f?Vector3.Slerp(fallback,offset/distance,Mathf.SmoothStep(0,1,distance/(radius*.8f))).normalized:fallback;
        return closest+normal*radius;
    }

}
