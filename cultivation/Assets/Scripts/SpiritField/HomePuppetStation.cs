using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

/// <summary>基站负责事务、任务和场景内傀儡；暂停/切场景归还尚未播下的种子。</summary>
public sealed class HomePuppetStation : MonoBehaviour
{
    public HomeStationState 状态;
    public string 错误 { get; private set; } = "";
    public string 当前作业 { get; private set; } = "待命";
    public GameObject 傀儡实例 { get; private set; }
    public const float 移动速度=1.65f,松土时长=10f;
    public const int 松土重复次数=6;
    enum Phase { 待命, 前往, 松土, 播种, 浇水, 收获, 返回 }
    Phase phase;
    Phase job;
    int field=-1,point;
    float timer,checkTimer,blockedTime;
    List<Vector3> path;
    HomeAssets assets;
    CharacterController mover;
    Collider deck;
    Animator animator;
    Transform model;
    Transform leftFoot,rightFoot;
    float footRestHeight;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable[] clips;
    readonly float[] weights=new float[5];
    int currentAnimation;
    int cultivationLoops;
    GameObject pick,kettle;
    ParticleSystem water;
    TextMesh warning;
    static readonly Dictionary<int,HomePuppetStation> claims=new Dictionary<int,HomePuppetStation>();
    public bool 已就绪=>傀儡实例&&animator&&graph.IsValid();
    void Start()
    {
        状态=状态??new HomeStationState();状态.确保();状态.归还种子();
        assets=HomeAssets.取();if(!assets||!assets.傀儡){错误="缺少傀儡资源";return;}
        // 只挡住基站后部与两侧，框架中央留出傀儡停靠、进出的通道。
        AddStationCollider(new Vector3(.5f,1,0),new Vector3(.8f,2,2.1f));
        AddStationCollider(new Vector3(-.65f,1,.9f),new Vector3(1.4f,2,.25f));
        AddStationCollider(new Vector3(-.65f,1,-.9f),new Vector3(1.4f,2,.25f));
        var floor=new GameObject("基站踏板");floor.transform.SetParent(transform,false);
        var floorCollider=floor.AddComponent<BoxCollider>();floorCollider.center=new Vector3(-.9f,.216f,0);floorCollider.size=new Vector3(1f,.432f,1.35f);deck=floorCollider;
        var station=gameObject.AddComponent<StationInteractable>();station.类型=StationInteractable.StationKind.傀儡基站;station.交互距离=4;station.提示最高点=2.8f;
        var w=new GameObject("仓库警示");w.transform.SetParent(transform,false);w.transform.localPosition=Vector3.up*3.1f;
        warning=w.AddComponent<TextMesh>();warning.text="!";warning.fontSize=80;warning.characterSize=.1f;warning.anchor=TextAnchor.MiddleCenter;warning.color=new Color(1,.15f,.1f);w.SetActive(false);
        傀儡实例=new GameObject("种田傀儡");
        傀儡实例.transform.SetPositionAndRotation(待命位置,待命朝向);
        model=Instantiate(assets.傀儡,傀儡实例.transform).transform;model.localScale=Vector3.one*1.25f;
        animator=model.GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
        var modelBounds=傀儡实例.GetComponentInChildren<SkinnedMeshRenderer>();if(modelBounds)modelBounds.updateWhenOffscreen=true;
        mover=傀儡实例.AddComponent<CharacterController>();mover.height=1.3f;mover.radius=.22f;mover.center=new Vector3(0,.65f,0);mover.skinWidth=.02f;mover.stepOffset=.45f;mover.minMoveDistance=0;
        graph=PlayableGraph.Create("FarmPuppet");graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer=AnimationMixerPlayable.Create(graph,5);
        var list=new[]{assets.待机,assets.行走,assets.松土,assets.播种收获,assets.浇水};clips=new AnimationClipPlayable[5];
        for(int i=0;i<5;i++)
        {
            var clip=list[i]?list[i]:assets.行走;clips[i]=AnimationClipPlayable.Create(graph,clip);
            clips[i].SetApplyFootIK(true);clips[i].SetApplyPlayableIK(false);
            clips[i].SetSpeed(i==2?clip.length*松土重复次数/松土时长:1);
            graph.Connect(clips[i],0,mixer,i);mixer.SetInputWeight(i,i==0?1:0);weights[i]=i==0?1:0;
        }
        var output=AnimationPlayableOutput.Create(graph,"Puppet",animator);output.SetSourcePlayable(mixer);graph.Play();
        graph.Evaluate(0);
        footRestHeight=FootHeight();
        var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
        if(hand)
        {
            pick=Instantiate(assets.稿子,hand);pick.transform.localScale=Vector3.one*.65f;pick.transform.localPosition=new Vector3(.05f,.14f,-.13f);pick.transform.localRotation=Quaternion.Euler(-38,157,183);pick.SetActive(false);
            kettle=Instantiate(assets.水壶,hand);kettle.transform.localScale=Vector3.one*.50f;kettle.transform.localPosition=new Vector3(.02f,-.15f,.13f);kettle.transform.localRotation=Quaternion.Euler(0,0,-20);kettle.SetActive(false);
            var flow=new GameObject("水流");flow.transform.SetParent(kettle.transform,false);flow.transform.localPosition=new Vector3(0,0,.3f);
            water=flow.AddComponent<ParticleSystem>();var main=water.main;main.startLifetime=.6f;main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f);main.startColor=new Color(.56f,.85f,1,.85f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.4f;main.maxParticles=100;
            var emission=water.emission;emission.rateOverTime=55;var shape=water.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=5;shape.radius=.025f;
            var material=new Material(Shader.Find("Sprites/Default")){color=new Color(.5f,.8f,1,.8f)};water.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;water.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    void AddStationCollider(Vector3 center,Vector3 size)
    {
        var body=gameObject.AddComponent<BoxCollider>();body.center=center;body.size=size;
    }
    public bool 可行走踏板(Collider collider)=>collider==deck;
    // 从用户在Play场景摆好的根节点读取：前方木板中央，面朝框架外侧。
    Vector3 待命位置=>transform.TransformPoint(new Vector3(-.539434f,.452f,.051194f));
    Quaternion 待命朝向=>transform.rotation*Quaternion.Euler(0,269.3783f,0);
    float FootHeight()=>leftFoot&&rightFoot?Mathf.Min(leftFoot.position.y,rightFoot.position.y)-model.position.y:0;
    void LateUpdate()
    {
        if(!已就绪)return;
        // 模型与导航胶囊分离：原动画的垂直根偏移不再让脚悬空。
        float offset=footRestHeight-FootHeight();
        model.localPosition=new Vector3(0,offset,0);
    }
    public bool 存入背包物品(ItemDefinition item,int count)
    {
        var panel=FindObjectOfType<UIPanelData>();
        if(!item||!panel||count<=0||!状态.能存(item.物品id,count)||!panel.移除物品(item,count))return false;
        return 状态.存入(item.物品id,count);
    }
    public bool 取回背包物品(int slotIndex)
    {
        var panel=FindObjectOfType<UIPanelData>();状态.确保();if(!panel||slotIndex<0||slotIndex>=4)return false;
        var slot=状态.仓库[slotIndex];var item=QuestDatabase.取().找物品(slot.物品id);int count=slot.数量;
        if(!item||!状态.取出(slot.物品id,count))return false;
        panel.给物品(item,count);return true;
    }
    public void 设置任务(bool enabled)
    {
        状态.种田任务=enabled;
        if(!enabled){取消作业();Return();}
    }
    void Play(int index)
    {
        if(currentAnimation==index)return;currentAnimation=index;clips[index].SetTime(0);
    }
    void Update()
    {
        if(!已就绪)return;
        if(!摆放校验.当前场景可摆放)return;
        float sum=0;
        for(int i=0;i<5;i++){weights[i]=Mathf.Lerp(weights[i],i==currentAnimation?1:0,1-Mathf.Exp(-Time.deltaTime*8));sum+=weights[i];}
        for(int i=0;i<5;i++)mixer.SetInputWeight(i,weights[i]/Mathf.Max(sum,.0001f));
        // 不依赖导入器的循环标记；待机与走路循环，松土在10秒内完整重复6次。
        for(int i=0;i<2;i++){float length=(i==0?assets.待机:assets.行走).length;if(clips[i].GetTime()>=length)clips[i].SetTime(clips[i].GetTime()%length);}
        if(phase==Phase.松土&&clips[2].GetTime()>=assets.松土.length){
            double time=clips[2].GetTime();int completed=(int)(time/assets.松土.length);
            cultivationLoops+=completed;
            clips[2].SetTime(cultivationLoops<松土重复次数?time%assets.松土.length:assets.松土.length);
        }
        checkTimer-=Time.deltaTime;
        if(checkTimer<=0)
        {
            checkTimer=.4f;string capacity=容量错误();
            if(!string.IsNullOrEmpty(capacity)){if(phase!=Phase.待命){取消作业();phase=Phase.待命;}错误=capacity;}
            else if(错误.StartsWith("仓库已满"))错误="";
            warning.gameObject.SetActive(!string.IsNullOrEmpty(错误));
        }
        if(warning.gameObject.activeSelf&&Camera.main)warning.transform.rotation=Camera.main.transform.rotation;
        if(!string.IsNullOrEmpty(错误)){Play(0);当前作业=错误;return;}
        // 足部基于实际地面落地，避免控制器skinWidth把傀儡托在半空。
        mover.Move(Vector3.down * 3f * Time.deltaTime);
        if(phase==Phase.待命)
        {
            Play(0);当前作业=状态.种田任务?"巡视灵田":"待命";
            var dockDelta=待命位置-傀儡实例.transform.position;dockDelta.y=0;
            if(dockDelta.magnitude<.1f)傀儡实例.transform.rotation=Quaternion.Slerp(傀儡实例.transform.rotation,待命朝向,1-Mathf.Exp(-Time.deltaTime*8));
            timer-=Time.deltaTime;if(timer<=0&&状态.种田任务){timer=1;FindJob();}return;
        }
        if(phase==Phase.前往||phase==Phase.返回)
        {
            Play(1);
            if(path==null||point>=path.Count){if(phase==Phase.返回){phase=Phase.待命;Play(0);}else BeginAction();return;}
            var delta=path[point]-傀儡实例.transform.position;delta.y=0;
            if(delta.magnitude<.025f){point++;blockedTime=0;return;}
            傀儡实例.transform.rotation=Quaternion.Slerp(傀儡实例.transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*9);
            var before=傀儡实例.transform.position;mover.Move(delta.normalized*Mathf.Min(delta.magnitude,Time.deltaTime*移动速度));
            if((before-傀儡实例.transform.position).sqrMagnitude<.000001f)blockedTime+=Time.deltaTime;else blockedTime=0;
            if(blockedTime>2){取消作业();错误="路径被阻挡";phase=Phase.待命;}
            return;
        }
        var b=灵田.取().状态(field);
        if(b==null){取消作业();Return();return;}
        傀儡实例.transform.rotation=Quaternion.Slerp(傀儡实例.transform.rotation,Quaternion.Euler(0,b.朝向角度,0),1-Mathf.Exp(-Time.deltaTime*8));
        if(water&&phase==Phase.浇水)
        {
            var target=b.位置+Vector3.up*.1f;var velocity=(target-water.transform.position)/.6f+Vector3.up*.7f;
            water.transform.rotation=Quaternion.LookRotation(velocity);var main=water.main;main.startSpeed=velocity.magnitude;
        }
        timer-=Time.deltaTime;if(timer>0)return;
        if(phase==Phase.松土){cultivationLoops=松土重复次数;clips[2].SetTime(assets.松土.length);phase=Phase.播种;pick.SetActive(false);Play(3);timer=assets.播种收获.length;当前作业="播种";return;}
        if(phase==Phase.播种)灵田.取().自动播种(field,状态.携带灵植,状态.消耗携带种子);
        if(phase==Phase.浇水)灵田.取().浇水(field);
        if(phase==Phase.收获)灵田.取().自动收获(field,状态.存入);
        取消作业();Return();
    }
    string 容量错误()
    {
        if(!状态.种田任务)return "";
        foreach(var b in 灵田.取().所有地块)if(b!=null&&b.已成熟&&!状态.能存(b.植.产物id))return "仓库已满：无法存放「"+灵田.取物品名(b.植.产物id)+"」";
        return "";
    }
    void FindJob()
    {
        var farm=灵田.取();
        for(int priority=0;priority<3;priority++)for(int i=0;i<farm.总块数;i++)
        {
            var b=farm.状态(i);if(b==null)continue;
            if(claims.TryGetValue(i,out var owner)&&owner&&owner!=this)continue;
            Phase action=Phase.待命;string plant="";
            if(priority==0&&b.已成熟)action=Phase.收获;
            if(priority==1&&b.需要浇水)action=Phase.浇水;
            if(priority==2&&b.待种植)foreach(var d in 灵植库.全部)if(灵植库.可种(d,b.品阶)&&状态.数量(d.种子id)>0){plant=d.id;action=Phase.松土;break;}
            if(action==Phase.待命)continue;
            Vector3 approach=b.位置;approach.y=0;
            var route=HomeFarmPath.查找(傀儡实例.transform.position,approach,傀儡实例.transform);if(route==null)continue;
            if(action==Phase.松土&&!状态.带种子(plant,i))continue;
            field=i;claims[i]=this;job=action;path=route;point=0;phase=Phase.前往;当前作业="前往第 "+(i+1)+" 块灵田";return;
        }
    }
    void BeginAction()
    {
        var b=灵田.取().状态(field);
        if(b==null||Vector3.Distance(new Vector3(b.位置.x,0,b.位置.z),new Vector3(傀儡实例.transform.position.x,0,傀儡实例.transform.position.z))>.06f)
        {取消作业();Return();return;}
        phase=job;
        if(job==Phase.松土){Play(2);cultivationLoops=0;pick.SetActive(true);timer=松土时长;当前作业="松土";}
        if(job==Phase.浇水){Play(4);kettle.SetActive(true);water.Play();timer=assets.浇水.length;当前作业="浇水";}
        if(job==Phase.收获){Play(3);timer=assets.播种收获.length;当前作业="收获";}
    }
    void Return()
    {
        path=HomeFarmPath.查找(傀儡实例.transform.position,待命位置,傀儡实例.transform);point=0;phase=path==null?Phase.待命:Phase.返回;当前作业="返回基站";blockedTime=0;
    }
    void 取消作业()
    {
        状态.归还种子();if(field>=0&&claims.TryGetValue(field,out var owner)&&owner==this)claims.Remove(field);field=-1;
        if(pick)pick.SetActive(false);if(kettle)kettle.SetActive(false);if(water)water.Stop(true,ParticleSystemStopBehavior.StopEmitting);
    }
    public void 重新尝试(){错误="";timer=0;}
    void OnDestroy()
    {
        if(状态!=null)取消作业();if(graph.IsValid())graph.Destroy();
        if(water&&water.GetComponent<Renderer>().sharedMaterial)Destroy(water.GetComponent<Renderer>().sharedMaterial);
        if(傀儡实例)Destroy(傀儡实例);
    }
}
