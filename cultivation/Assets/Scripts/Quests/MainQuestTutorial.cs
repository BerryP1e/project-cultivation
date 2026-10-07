using System.Collections;
using UnityEngine;

/// <summary>主线教学的交易凭据与讲述者，沿用对话标记存档。</summary>
public static class MainQuestTutorial
{
    public static void 记录兑换(bool teachingHall)
    {
        var stage=任务管理器.实例?.取当前阶段("q_main_005");
        if(stage!=null && stage.条件==任务条件.宗门兑换 && stage.物品id==(teachingHall?"传法阁":"功德堂"))
            对话标记.添加("教学兑换_"+stage.id);
    }
    public static void 记录悬赏(宗门委托 quest)
    {
        var stage=任务管理器.实例?.取当前阶段("q_main_005");
        if(stage!=null && stage.条件==任务条件.已接宗门悬赏 && quest.是悬赏 && quest.等级==Mathf.Max(1,stage.数量))
            对话标记.添加("教学悬赏_"+stage.id);
    }
    public static bool 已接指定悬赏(QuestDefinition stage)
    {
        var current=宗门任务.当前悬赏;
        return 对话标记.具备("教学悬赏_"+stage.id) || current!=null && current.等级==Mathf.Max(1,stage.数量);
    }
    public static bool 操作面板已开()
    {
        foreach(var panel in Object.FindObjectsOfType<功德堂兑换>())if(panel.面板已开)return true;
        foreach(var panel in Object.FindObjectsOfType<执事阁界面>())if(panel.面板已开)return true;
        foreach(var panel in Object.FindObjectsOfType<炼丹界面>())if(panel.已打开)return true;
        foreach(var panel in Object.FindObjectsOfType<UIInkAlchemyPage>())if(panel.已打开)return true;
        return false;
    }
    public static GameObject 取师兄()=>取讲述者("大师兄");
    public static GameObject 取讲述者(string speaker)
    {
        string id=speaker=="大师兄"?"npc_dashixiong":speaker=="师父"?"npc_shifu":null;
        if(id==null)return null;
        foreach(var npc in Object.FindObjectsOfType<NpcInstance>())if(npc.定义!=null && npc.定义.id==id && !npc.IsDead)return npc.gameObject;
        return null;
    }
    public static IEnumerator 讲述者到身旁(string speaker, bool preserveExisting=false, bool flyIn=false)
    {
        if(speaker!="大师兄" && speaker!="师父")yield break;
        var player=物品使用器.取玩家物体();if(player==null)yield break;
        var actor=取讲述者(speaker);
        var point=取空闲站位(player,actor);
        bool existed=actor!=null;
        bool spawnedAIWasEnabled=false;
        if(actor==null)
        {
            var path=speaker=="大师兄"?"NPC/Human/大师兄/大师兄":"NPC/Human/太虚宗掌门/太虚宗掌门";
            var prefab=NpcPrefabs.加载(path);
            if(prefab==null){Debug.LogWarning("[主线教学] 缺少讲述者 "+path);yield break;}
            var start=flyIn ? point+new Vector3(10,6,-14) : point;
            actor=Object.Instantiate(prefab,start,Quaternion.identity);actor.name=speaker;
            var newAI=actor.GetComponent<NpcAiBase>();spawnedAIWasEnabled=newAI==null || newAI.enabled;if(newAI!=null)newAI.enabled=false;
            yield return null;
        }
        actor.GetComponent<StoryCompanionGuide>()?.StopGuide();
        var anchor=actor.GetComponent<StoryCompanionAnchor>();
        if(anchor!=null && !preserveExisting)anchor.enabled=false;
        var ai=actor.GetComponent<NpcAiBase>();bool enabled=existed?ai!=null && ai.enabled:spawnedAIWasEnabled;if(ai!=null)ai.enabled=false;
        try
        {
            if(!(existed && preserveExisting) && Vector3.Distance(actor.transform.position,point)>6)
            {
                StoryCompanionGuide.Play(actor,"Yufeng_Forward");
                while(actor!=null && Vector3.Distance(actor.transform.position,point)>.2f)
                {
                    任务管理器.保持演出中();
                    actor.transform.position=Vector3.MoveTowards(actor.transform.position,point,14f*Time.deltaTime);
                    var direction=point-actor.transform.position;direction.y=0;
                    if(direction.sqrMagnitude>.001f)actor.transform.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,ai!=null?ai.模型朝向补偿:0,0);
                    yield return null;
                }
            }
            if(actor!=null)
            {
                StoryCompanionGuide.Play(actor,"Idle");
                var direction=player.transform.position-actor.transform.position;direction.y=0;
                if(direction.sqrMagnitude>.001f)actor.transform.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,ai!=null?ai.模型朝向补偿:0,0);
            }
        }
        finally {if(ai!=null)ai.enabled=enabled;}
        if(preserveExisting)
        {
            if(anchor==null)actor.AddComponent<StoryCompanionAnchor>();
            else if(!anchor.enabled)anchor.enabled=true;
        }
    }

    static Vector3 取空闲站位(GameObject player, GameObject actor)
    {
        var origin=player.transform.position;
        for(int i=0;i<16;i++)
        {
            float angle=(i%8)*45f*Mathf.Deg2Rad;
            var point=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(i<8?3.2f:5f);
            bool blocked=false;
            foreach(var npc in Object.FindObjectsOfType<NpcInstance>())
                if(npc.gameObject!=actor && Vector3.Distance(npc.transform.position,point)<2.2f){blocked=true;break;}
            if(blocked)continue;
            foreach(var c in Physics.OverlapCapsule(point+Vector3.up*.6f,point+Vector3.up*1.7f,.35f,~0,QueryTriggerInteraction.Ignore))
            {
                if(c is TerrainCollider || c.transform.IsChildOf(player.transform) || actor!=null && c.transform.IsChildOf(actor.transform))continue;
                blocked=true;break;
            }
            if(!blocked)return point;
        }
        return origin+Vector3.left*4f;
    }
}
