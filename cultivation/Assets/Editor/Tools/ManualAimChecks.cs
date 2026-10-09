using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>方向普攻、独立邪眼落点和持剑配置切换的跨帧回归；结束还原现场。</summary>
public static class ManualAimChecks
{
    public static string Result{get;private set;}="Not run";
    [MenuItem("修仙/调试/验证技能引导范围（Play）")]
    public static void 验证引导范围()
    {
        var aim=Object.FindObjectOfType<PlayerManualAim>();
        Require(Application.isPlaying&&aim,"请在玩家场景进入 Play");
        var data=Object.FindObjectOfType<UIPanelData>();var sword=aim.GetComponent<QingshanSwordTreasure>();
        Require(sword,"玩家缺少青山剑组件");
        var slots=data.主动技能;int kills=data.青山剑有效击杀,selection=aim.选择槽位;
        float scale=sword.巨剑基础倍率,radius=sword.巨剑体素破坏半径,sense=sword.基础索敌范围;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var selected=typeof(PlayerManualAim).GetField("<选择槽位>k__BackingField",flags);
        var getRadius=typeof(PlayerManualAim).GetMethod("Radius",flags);var getRange=typeof(PlayerManualAim).GetMethod("Range",flags);
        var draw=typeof(PlayerManualAim).GetMethod("Draw",flags);
        void CheckRing(int slot,float expected){
            selected.SetValue(aim,slot);float actual=(float)getRadius.Invoke(aim,null);
            Require(Mathf.Abs(actual-expected)<.0001f,"引导半径错误 slot="+slot+" actual="+actual+" expected="+expected);
            var center=aim.transform.position+Vector3.forward*2;draw.Invoke(aim,new object[]{center,actual,true,Vector3.up});
            var line=Read<LineRenderer>(aim,"ring");Require(line.positionCount==65,"范围圈点数错误");
            for(int i=0;i<line.positionCount;i++){
                var offset=line.GetPosition(i)-center;offset.y=0;
                Require(Mathf.Abs(offset.magnitude-expected)<.001f,"实际绘制范围与结算不符");
            }
        }
        try{
            var fire=AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>("Assets/Data/Generated/ActiveDivineAbility/ability_fentian_yanshu.asset");
            var ice=AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>("Assets/Data/Generated/ActiveDivineAbility/ability_bingbao_shu.asset");
            data.主动技能=new List<Object>{fire,ice,null,null,null,null};
            Require(fire.范围==12&&VoxelCombatDamage.AreaRadius(fire)==1,"焚天结算配置错误");CheckRing(0,1);CheckRing(1,2.5f);
            sword.巨剑体素破坏半径=6;sword.巨剑基础倍率=10.13f;data.青山剑有效击杀=100;CheckRing(-2,2);
            data.青山剑有效击杀=400;CheckRing(-2,2*(11.03f/10.13f));
            sword.巨剑基础倍率=20.26f;CheckRing(-2,2*(21.16f/10.13f));
            sword.基础索敌范围=7;
            Require(Mathf.Abs((float)getRange.Invoke(aim,null)-sword.SenseRange)<.0001f,"巨剑施放距离提示未使用法宝实际神识范围");
            Require(fire.范围==12,"引导检查修改了敌人伤害范围");
            Debug.Log("MANUAL_AIM_RADIUS_PASS fire=1m ice=2.5m giant3=2m giant9+customScale=True actualLineVertices=True castRange=True");
        }finally{
            data.主动技能=slots;data.青山剑有效击杀=kills;sword.巨剑基础倍率=scale;sword.巨剑体素破坏半径=radius;sword.基础索敌范围=sense;
            selected.SetValue(aim,selection);typeof(PlayerManualAim).GetMethod("Hide",flags).Invoke(aim,null);
        }
    }
    [MenuItem("修仙/调试/验证方向普攻与灵虚切换（Play）")]
    public static void 开始()
    {
        var aim=Object.FindObjectOfType<PlayerManualAim>();if(!Application.isPlaying||!aim||!aim.可接收输入)throw new InvalidOperationException("请在可操作的 Player 场景进入 Play");
        if(Result=="Running")throw new InvalidOperationException("验证正在运行");Result="Running";aim.StartCoroutine(Guard(Check(aim)));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while(true){bool more=false;Exception error=null;try{more=test.MoveNext();}catch(Exception e){error=e;}
            if(error!=null){(test as IDisposable)?.Dispose();Result="FAIL "+error;Debug.LogError(Result);yield break;}
            if(!more){Result="PASS";Debug.Log("MANUAL_DIRECTION_AND_LINGXU_PASS");yield break;}yield return test.Current;}
    }
    static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    static T Read<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    static IEnumerator CheckSwordTimeline(PlayerAnimationController driver)
    {
        var animator=driver.animator;var sword=animator.transform.Find("灵虚剑/LingxuSword");
        Require(sword,"灵虚剑没有挂在 Animator 的独立动画路径下");
        foreach(var entry in new[]{("持剑_Idle",1f),("持剑_Run",1f),("A1",1f),("A2",1f),("A3",1f),("A1",2f),("A2",2f),("A3",2f)}) {
            var clip=Resources.Load<AnimationClip>("技能动作/灵虚剑决/"+entry.Item1);
            var bindings=AnimationUtility.GetCurveBindings(clip);
            var curves=new AnimationCurve[10];
            for(int i=0;i<curves.Length;i++){
                string property=i<3?"m_LocalPosition."+"xyz"[i]:i<7?"m_LocalRotation."+"xyzw"[i-3]:"m_LocalScale."+"xyz"[i-7];
                curves[i]=AnimationUtility.GetEditorCurve(clip,bindings.First(b=>b.path=="灵虚剑/LingxuSword"&&b.propertyName==property));
            }
            Require(driver.播动作(clip,entry.Item2),"剑轨迹动作未开始");int samples=0;float maxAngle=0,maxPosition=0,maxScale=0;float deadline=Time.time+clip.length/entry.Item2+3;
            yield return null;
            while(driver.动作播放中&&Time.time<deadline){
                var state=animator.GetCurrentAnimatorStateInfo(0);
                if(state.IsName(driver.动作状态名)&&!animator.IsInTransition(0)&&state.normalizedTime<.85f){
                    float time=Mathf.Repeat(state.normalizedTime,1)*clip.length;
                    var p=new Vector3(curves[0].Evaluate(time),curves[1].Evaluate(time),curves[2].Evaluate(time));
                    var q=new Quaternion(curves[3].Evaluate(time),curves[4].Evaluate(time),curves[5].Evaluate(time),curves[6].Evaluate(time)).normalized;
                    var s=new Vector3(curves[7].Evaluate(time),curves[8].Evaluate(time),curves[9].Evaluate(time));
                    float angle=Quaternion.Angle(sword.localRotation.normalized,q);
                    maxAngle=Mathf.Max(maxAngle,angle);maxPosition=Mathf.Max(maxPosition,Vector3.Distance(sword.localPosition,p));maxScale=Mathf.Max(maxScale,Vector3.Distance(sword.localScale,s));
                    samples++;
                }
                yield return null;
            }
            Require(samples>=3,"未采到稳定的剑动作 "+entry);
            Debug.Log("Sword timeline "+entry+" samples="+samples+" positionError="+maxPosition+" angleError="+maxAngle);
            // Native quaternion interpolation differs slightly from editor float-curve evaluation.
            Require(maxPosition<.002f&&maxScale<.002f&&maxAngle<1f,"身体与剑的播放进度错位 "+entry);
            driver.停止动作();yield return null;
        }
        Debug.Log("LINGXU_SWORD_TIMELINE_PASS: Ready/Run/A1/A2/A3 + 2x attack speed");
    }
    static IEnumerator Check(PlayerManualAim aim)
    {
        var player=aim.gameObject;var data=Object.FindObjectOfType<UIPanelData>();var loader=player.GetComponent<PlayerAbilityLoader>();
        var movement=player.GetComponent<PlayerController>();var cc=player.GetComponent<CharacterController>();var vitals=player.GetComponent<PlayerVitals>();
        var targeting=player.GetComponent<NpcTargeting>();var animation=player.GetComponent<PlayerAnimationController>();var lockNpc=targeting.LockedNpc;
        var old=data.当前功法;var abilities=data.神通;var passives=data.已获得被动神通;var disabled=data.已停用被动;bool move=movement.enabled,collision=cc.enabled,regen=vitals.自动回复;
        var position=player.transform.position;var rotation=player.transform.rotation;float mana=vitals.当前灵气,health=vitals.当前气血;
        var eyeDef=AssetDatabase.LoadAssetAtPath<PassiveDivineAbility>("Assets/Data/Generated/PassiveDivineAbility/ability_xieyan.asset");
        try{
            movement.enabled=false;cc.enabled=false;vitals.自动回复=false;targeting.ClearLock();player.transform.position=new Vector3(10000,100,10000);
            data.已获得被动神通=new List<PassiveDivineAbility>{eyeDef};data.已停用被动=new List<PassiveDivineAbility>();
            data.当前功法=AssetDatabase.LoadAssetAtPath<GongFaDefinition>("Assets/Data/Generated/GongFaDefinition/gongfa_lingxu_jianjue.asset");data.RaiseChanged();loader.Refresh();
            yield return new WaitForSeconds(1.2f);vitals.当前灵气=vitals.灵气上限;
            var attack=player.GetComponent<BasicLingxuSword01>();var eye=player.GetComponent<DevilEyeAbility>();
            Require(attack&&attack.enabled&&eye&&eye.enabled,"新功法或邪眼未装载");
            float sense=PlayerManualAim.SenseRange(player);Require(sense>attack.出手最大距离+.5f,"神识范围不足以执行远处落点回归");
            var point=player.transform.position+Vector3.forward*(sense*.85f);int sequence=attack.出手序号;
            Require(aim.确认施放(-1,point)&&attack.出手序号==sequence+1,"近战因远处鼠标落点拒绝出手");
            Require(Vector3.Distance(Read<GameObject>(eye,"激光").transform.localScale,Vector3.one*.024f)<.00001f,"邪眼激光没有缩至原来的 1/5");
            Require(Quaternion.Angle(player.transform.rotation,Quaternion.Euler(0,movement.visualYawOffset,0))<.1f,"普攻朝向错误");
            Require(Read<Vector3?>(eye,"手动光束点").HasValue&&Vector3.Distance(Read<Vector3?>(eye,"手动光束点").Value,point)<.01f,"邪眼未攻击近战范围以外的引导点");
            // 独立被动间隔：普攻动作未结束时，已就绪的邪眼仍可响应下一次落点确认。
            eye.GetType().GetField("下次攻击",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(eye,0f);
            var second=player.transform.position+Vector3.right*(sense*.8f);
            Require(aim.确认施放(-1,second)&&attack.出手序号==sequence+1&&Vector3.Distance(Read<Vector3?>(eye,"手动光束点").Value,second)<.01f,"邪眼被普攻动作或冷却阻止");
            float deadline=Time.time+5;while(attack.出手动作中&&Time.time<deadline)yield return null;Require(!attack.出手动作中,"近战动作未结束");
            float before=vitals.当前灵气;var outside=player.transform.position+Vector3.back*(sense+10);
            Require(aim.确认施放(-1,outside)&&attack.出手序号==sequence+2,"神识圈外的方向普攻被拒绝");
            Require(Mathf.Abs(vitals.当前灵气-before)<.001f,"邪眼在神识圈外施放或扣灵力");
            animation.停止动作();yield return null;
            var swordCheck=CheckSwordTimeline(animation);while(swordCheck.MoveNext())yield return swordCheck.Current;
            foreach(string id in new[]{"gongfa_taixu_jianjue","gongfa_jiuba_xuangong","gongfa_lingxu_jianjue","gongfa_taixu_lianqi"}){
                var g=AssetDatabase.FindAssets("t:GongFaDefinition").Select(guid=>AssetDatabase.LoadAssetAtPath<GongFaDefinition>(AssetDatabase.GUIDToAssetPath(guid))).First(x=>x.功法id==id);
                data.当前功法=g;data.RaiseChanged();loader.Refresh();yield return null;yield return null;
                int count=player.GetComponents<BasicJiuba01>().Count(x=>x.enabled)+player.GetComponents<BasicRemoteAttack01>().Count(x=>x.enabled);
                Require(count==1,"切换功法后普攻组件重复启用 "+id);
                if(id=="gongfa_lingxu_jianjue"){
                    var mount=player.GetComponent<武器挂载>();var carry=player.GetComponent<WeaponCarryAnim>();Require(mount.实例&&mount.实例.GetComponentInChildren<SkinnedMeshRenderer>().name=="LingxuSword","切回灵虚时武器错误");
                    Require(carry.当前覆盖&&carry.当前覆盖["Armature|Idle_Loop"]==Resources.Load<AnimationClip>("技能动作/灵虚剑决/持剑_Idle"),"Ready 待机覆盖错误");
                    Require(carry.当前覆盖["Armature|Sprint_Loop"]==Resources.Load<AnimationClip>("技能动作/灵虚剑决/持剑_Run"),"Run 移动覆盖错误");
                    swordCheck=CheckSwordTimeline(animation);while(swordCheck.MoveNext())yield return swordCheck.Current;
                }
            }
            var remote=player.GetComponent<BasicRemoteAttack01>();Require(remote&&remote.enabled&&remote.手动出手(outside),"远程方向普攻因鼠标超距拒绝出手");
            Require(Vector3.Distance(Read<Vector3>(remote,"手动点"),player.transform.position)<=remote.神识范围+.01f,"方向飞弹射程未限制");
            aim.GetType().GetMethod("Draw",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(aim,new object[]{point,.6f,true,Vector3.up});
            aim.GetType().GetMethod("DrawSenseRange",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(aim,null);
            var line=Read<LineRenderer>(aim,"senseRing");Require(line&&line.positionCount==129,"神识范围圈缺失");
            Require(Mathf.Abs(Vector3.Distance(line.GetPosition(0),player.transform.position)-sense)<.15f,"神识圈尺寸错误");
        }finally{
            animation.停止动作();data.当前功法=old;data.神通=abilities;data.已获得被动神通=passives;data.已停用被动=disabled;data.RaiseChanged();loader.Refresh();
            player.transform.SetPositionAndRotation(position,rotation);cc.enabled=collision;movement.enabled=move;vitals.自动回复=regen;vitals.当前灵气=mana;vitals.当前气血=health;
            if(lockNpc)targeting.Lock(lockNpc);
        }
    }
}
