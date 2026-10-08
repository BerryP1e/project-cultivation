using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Cross-frame checks through real player casts, animated projectiles and physics.</summary>
public static class VoxelGameplayChecks
{
    public static string Result {get;private set;}="Not run";
    [MenuItem("修仙/体素/验证战斗与灵木回收（Play）")]
    public static void 开始()
    {
        if(Result=="Running")throw new InvalidOperationException("Already running");
        var pilot=Object.FindObjectOfType<VoxelMapPilot>();if(!Application.isPlaying || !pilot)throw new InvalidOperationException("Enter wilderness Play first");
        Result="Running";pilot.StartCoroutine(Guard(Check(pilot)));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while(true)
        {
            bool more=false;Exception error=null;try{more=test.MoveNext();}catch(Exception e){error=e;}
            if(error!=null){(test as IDisposable)?.Dispose();Result="FAIL "+error;Debug.LogError(Result);yield break;}
            if(!more){Result="PASS";Debug.Log("VOXEL_GAMEPLAY_CHECK_PASS");yield break;}
            yield return test.Current;
        }
    }
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static T Asset<T>(string name) where T:UnityEngine.Object
    {return AssetDatabase.LoadAssetAtPath<T>("Assets/Data/Generated/"+typeof(T).Name+"/"+name+".asset");}
    static IEnumerator Check(VoxelMapPilot pilot)
    {
        var vitals=Object.FindObjectOfType<PlayerVitals>();var player=vitals.gameObject;var movement=player.GetComponent<PlayerController>();
        var data=Object.FindObjectOfType<UIPanelData>();var loader=player.GetComponent<PlayerAbilityLoader>();var aim=player.GetComponent<PlayerManualAim>();
        var cc=player.GetComponent<CharacterController>();var caster=player.GetComponent<ActiveSkillCaster>();var animation=player.GetComponent<PlayerAnimationController>();
        Require(aim && aim.可接收输入 && !aim.有锁定,"Player not ready for unlocked aiming");
        var position=player.transform.position;var rotation=player.transform.rotation;bool move=movement.enabled,collision=cc.enabled,regen=vitals.自动回复;
        float mana=vitals.当前灵气,health=vitals.当前气血;var slots=data.主动技能;var gongfa=data.当前功法;var treasure=data.当前法宝;
        int kills=data.青山剑有效击杀;var owned=new List<string>(data.已拥有法宝);var passives=new List<PassiveDivineAbility>(data.已获得被动神通);var disabled=new List<PassiveDivineAbility>(data.已停用被动);
        var wood=QuestDatabase.取().找物品("item_lingmu");int woodBefore=data.物品数量(wood);var clones=new List<Object>();
        try
        {
            movement.enabled=false;cc.enabled=false;vitals.自动回复=false;pilot.还原岩石();
            var terrain=Terrain.activeTerrain;var home=new Vector3(4,0,75);home.y=terrain.SampleHeight(home)+terrain.transform.position.y;player.transform.position=home;
            // A disconnected crown must actually rotate under physics, then grant one item per crown.
            pilot.破坏球(new Vector3(34.33f,1.2f,7.96f),1.34f);Drain(pilot);
            var debris=Object.FindObjectsOfType<VoxelTreeDebris>();Require(debris.Length>0 && debris.All(d=>!d.GetComponent<Rigidbody>().isKinematic),"Severed tree did not become a physical body");
            var start=debris[0].transform.rotation;float until=Time.time+.65f;
            while(Time.time<until)yield return null;
            Require(debris[0] && Quaternion.Angle(start,debris[0].transform.rotation)>8,"Tree crown did not topple");
            until=Time.time+4;while(Object.FindObjectsOfType<VoxelTreeDebris>().Length>0 && Time.time<until)yield return null;
            Require(data.物品数量(wood)-woodBefore==debris.Length,"Wood award was missing or duplicated");
            Debug.Log("TREE_PHYSICS_AND_INVENTORY_PASS crowns="+debris.Length);
            pilot.还原岩石();
            // Equip the actual remote method and enabled evil eye, rather than calling the voxel tool directly.
            data.当前功法=Asset<GongFaDefinition>("gongfa_taixu_lianqi");
            var eye=Asset<PassiveDivineAbility>("ability_xieyan");data.已获得被动神通.Add(eye);data.已停用被动.Remove(eye);loader.Refresh();
            vitals.当前灵气=vitals.灵气上限;until=Time.time+1.1f;while(Time.time<until)yield return null;
            var target=home+Vector3.forward*1.5f;Require(aim.确认施放(-1,target),"Manual remote basic rejected");
            Require(player.GetComponent<DevilEyeAbility>() && Object.FindObjectsOfType<AbilityPreciseShotVfx>().Length>0,"Evil eye did not follow basic aim");
            until=Time.time+5;while(pilot.地面.活动地块数==0 && Time.time<until)yield return null;
            Require(pilot.地面.活动地块数>0,"Animated basic projectile did not damage ground");Drain(pilot);
            until=Time.time+2;while(animation && animation.动作播放中 && Time.time<until)yield return null;
            Debug.Log("MANUAL_BASIC_AND_EVIL_EYE_PASS");
            // Both melee loadouts retain their real sequence and weapon rig while unlocked.
            foreach(string method in new[]{"gongfa_jiuba_xuangong","gongfa_taixu_jianjue"})
            {
                var g=Asset<GongFaDefinition>(method);if(!g && method.Contains("jiuba"))g=AssetDatabase.FindAssets("t:GongFaDefinition").Select(id=>AssetDatabase.LoadAssetAtPath<GongFaDefinition>(AssetDatabase.GUIDToAssetPath(id))).First(x=>x.普攻方法id=="basic_jiuba_01");
                data.当前功法=g;loader.Refresh();yield return null;
                var melee=player.GetComponents<BasicJiuba01>().FirstOrDefault(x=>x.enabled);Require(melee,"Melee method was not equipped");
                int sequence=melee.出手序号;Require(aim.确认施放(-1,target),"Manual melee rejected: "+method);
                until=Time.time+3;while(melee.出手动作中 && Time.time<until)yield return null;
                Require(melee.出手序号==sequence+1,"Melee sequence did not advance once");Drain(pilot);
            }
            Debug.Log("MANUAL_MELEE_SEQUENCES_PASS");data.当前功法=null;loader.Refresh();
            // Small ranges keep this regression bounded; timing, animation, mana and charge rules stay real.
            string[] ids={"ability_fentian_yanshu","ability_bingbao_shu","ability_hanxu","ability_xiao_jianzhen","ability_leidong_qianshan"};
            for(int i=0;i<ids.Length;i++)
            {
                pilot.还原岩石();player.transform.position=home;animation?.停止动作();
                var skill=Object.Instantiate(Asset<ActiveDivineAbility>(ids[i]));clones.Add(skill);
                if(skill.结算方式!=ActiveSkillKind.追踪弹)skill.范围=skill.结算方式==ActiveSkillKind.向前冰柱?3:1.2f;
                data.主动技能=new List<Object>{skill,null,null,null,null,null};vitals.当前灵气=vitals.灵气上限;
                float before=vitals.当前灵气;Require(aim.确认施放(0,target),"Manual skill rejected: "+ids[i]);
                Require(Mathf.Abs(vitals.当前灵气-(before-skill.消耗灵力))<.001f,"Mana cost did not use normal cast path");
                if(skill.可积攒次数<=1){float after=vitals.当前灵气;Require(!aim.确认施放(0,target) && vitals.当前灵气==after,"Cooldown allowed a duplicate or consumed extra mana");}
                until=Time.time+7;while(pilot.地面.活动地块数==0 && Time.time<until)yield return null;
                Require(pilot.地面.活动地块数>0,"Skill did not reach voxel damage timing: "+ids[i]);
                Drain(pilot);Debug.Log("MANUAL_SKILL_PASS "+ids[i]);
                until=Time.time+7;while(Object.FindObjectsOfType<HomingBoltSkillRunner>().Length+Object.FindObjectsOfType<IcePillarSkillRunner>().Length+Object.FindObjectsOfType<DirectedAbilityRunner>().Length+Object.FindObjectsOfType<AreaSkillRunner>().Length+Object.FindObjectsOfType<BlinkSkillRunner>().Length>0 && Time.time<until)yield return null;
            }
            // The giant must complete gathering, impact and piercing against a world point without a dummy NPC.
            pilot.还原岩石();player.transform.position=home;animation?.停止动作();
            var sword=player.GetComponent<QingshanSwordTreasure>();data.当前法宝=Asset<TreasureDefinition>(QingshanSwordTreasure.法宝id);data.已拥有法宝.Add(QingshanSwordTreasure.法宝id);data.青山剑有效击杀=100;
            yield return null;Require(aim.确认施放(-2,target),"Manual giant rejected");
            until=Time.time+10;while(sword.UltimateState!=QingshanSwordTreasure.UltimatePhase.Idle && Time.time<until)yield return null;
            Require(sword.UltimateState==QingshanSwordTreasure.UltimatePhase.Idle && pilot.地面.活动地块数>0,"Giant failed to impact and finish");Drain(pilot);
            Debug.Log("MANUAL_GIANT_PIERCE_PASS patches="+pilot.地面.活动地块数);
            UiEscRegistry.SetSceneInputBlocked(pilot,true);float blockedMana=vitals.当前灵气;
            Require(!aim.确认施放(-1,target) && vitals.当前灵气==blockedMana,"UI allowed a world cast");UiEscRegistry.SetSceneInputBlocked(pilot,false);
        }
        finally
        {
            UiEscRegistry.SetSceneInputBlocked(pilot,false);pilot.还原岩石();
            int added=data.物品数量(wood)-woodBefore;if(added>0)data.移除物品(wood,added);
            data.当前功法=gongfa;data.主动技能=slots;data.当前法宝=treasure;data.青山剑有效击杀=kills;data.已拥有法宝=owned;data.已获得被动神通=passives;data.已停用被动=disabled;
            animation?.停止动作();player.transform.SetPositionAndRotation(position,rotation);cc.enabled=collision;movement.enabled=move;vitals.当前灵气=mana;vitals.当前气血=health;vitals.自动回复=regen;loader.Refresh();
            foreach(var clone in clones)if(clone)Object.Destroy(clone);Physics.SyncTransforms();
        }
    }
    static void Drain(VoxelMapPilot pilot)
    {
        int guard=0;while(pilot.等待更新 && guard++<100000)pilot.更新局部();Require(!pilot.等待更新,"Voxel queue did not finish");Physics.SyncTransforms();
    }
}
