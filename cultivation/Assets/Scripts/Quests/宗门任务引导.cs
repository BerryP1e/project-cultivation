using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>主线追踪上方的宗门委托 HUD；叹号/边缘箭头在 Overlay 层，不受后处理冲淡。</summary>
public class 宗门任务引导 : MonoBehaviour
{
    Canvas 画布; RectTransform 面板, 叹号, 箭头; Text 名称, 进度, 地点;
    Transform 目标; string 目标名; float 下次更新;
    public bool 有引导目标 => 目标 != null;
    void Start()
    {
        var font=Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f=>f.name.ToLowerInvariant().Contains("simhei"));
        var root=new GameObject("SectQuestGuideCanvas",typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
        画布=root.GetComponent<Canvas>();画布.renderMode=RenderMode.ScreenSpaceOverlay;画布.sortingOrder=1501;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var image=UIBuildUtils.CreateImage("宗门委托追踪",root.transform,new Color(.025f,.035f,.035f,.42f));image.gameObject.AddComponent<UIInkDialogueBackdrop>();
        面板=image.rectTransform;面板.anchorMin=面板.anchorMax=new Vector2(0,.5f);面板.pivot=new Vector2(0,0);面板.anchoredPosition=new Vector2(12,12);面板.sizeDelta=new Vector2(470,0);
        var layout=UIBuildUtils.AddVerticalLayout(面板,7,new RectOffset(40,40,25,25));layout.childControlWidth=true;
        面板.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        名称=文("委托名称",面板,font,22,new Color(.96f,.87f,.66f));进度=文("委托进度",面板,font,20,new Color(.98f,.96f,.90f));地点=文("目标地点",面板,font,18,new Color(.84f,.87f,.79f));
        var mark=UIBuildUtils.CreateText("悬赏感叹号",root.transform,font,"！",54,TextAnchor.MiddleCenter,new Color(1,.82f,.22f));
        叹号=mark.rectTransform;叹号.anchorMin=叹号.anchorMax=new Vector2(.5f,.5f);叹号.sizeDelta=new Vector2(60,64);UIBuildUtils.AddOutline(叹号,new Color(.07f,.05f,.02f,1));
        var arrow=UIBuildUtils.CreateImage("悬赏边缘箭头",root.transform,new Color(1,.82f,.22f));arrow.sprite=任务引导.取三角精灵();箭头=arrow.rectTransform;箭头.anchorMin=箭头.anchorMax=new Vector2(.5f,.5f);箭头.sizeDelta=new Vector2(36,36);
        面板.gameObject.SetActive(false);隐藏标记();
    }
    static Text 文(string name,Transform parent,Font font,int size,Color color)
    {
        var t=UIBuildUtils.CreateText(name,parent,font,"",size,TextAnchor.UpperLeft,color);UIBuildUtils.AddOutline(t.rectTransform,new Color(0,0,0,.95f));return t;
    }
    void Update()
    {
        if(画布==null)return;
        var q=宗门任务.当前追踪; var r=宗门任务.记录(q?.id);
        bool visible=q!=null && r!=null && !UiEscRegistry.SceneInputBlocked && !DialogueUI.正在显示 && !黑幕字幕.有幕在显示;
        面板.gameObject.SetActive(visible);if(!visible){隐藏标记();return;}
        if(Time.unscaledTime>=下次更新)
        {
            下次更新=Time.unscaledTime+.3f;bool done=宗门任务.可交付(q);
            名称.text="宗门 · "+q.名称;
            进度.text=q.是悬赏 ? done ? "清剿完成，回执事阁领取悬赏" : "剿除妖群   "+(q.妖魔列表().Count-r.剩余妖魔.Count)+" / "+q.妖魔列表().Count : q.目标介绍+"   已有 "+宗门任务.持有数(q);
            解析目标(q,done);
        }
        var player=GameObject.Find("Player");
        地点.text=目标==null ? 目标名 : 目标名+"   "+(player!=null?Vector3.Distance(player.transform.position,目标.position).ToString("0.#")+" 米":"");
        刷标记();
    }
    void 解析目标(宗门委托 q,bool done)
    {
        目标=null;
        string desired=done || !q.是悬赏 ? "Sect" : 宗门悬赏刷怪.野外场景;
        if(string.Equals(gameObject.scene.name,desired,StringComparison.OrdinalIgnoreCase))
        {
            if(desired=="Sect")
            {
                目标名="返回执事阁提交";
                目标=FindObjectsOfType<StationInteractable>().FirstOrDefault(s=>s.GetComponent<执事阁界面>()!=null)?.transform;
            }
            else {目标名="宗门野外 · 悬赏妖群";目标=FindObjectOfType<宗门悬赏刷怪>()?.目标;}
        }
        else
        {
            目标名=desired=="Sect" ? "返回宗门 · 执事阁" : "前往宗门野外";
            目标=FindObjectsOfType<Teleporter>().FirstOrDefault(t=>t.选项!=null && t.选项.Any(o=>o!=null && !o.暂未开放 && string.Equals(o.场景,desired,StringComparison.OrdinalIgnoreCase)))?.transform;
        }
        if(!q.是悬赏 && !done){目标=null;目标名="备齐物品后到执事阁提交";}
    }
    void 刷标记()
    {
        var cam=Camera.main;if(cam==null || 目标==null){隐藏标记();return;}
        var pos=cam.WorldToScreenPoint(目标.position+Vector3.up*2.5f);var rect=(RectTransform)画布.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,new Vector2(pos.x,pos.y),null,out var p);
        bool front=pos.z>0; if(!front)p=-p;
        var half=rect.rect.size*.5f;bool inside=front && Mathf.Abs(p.x)<half.x && Mathf.Abs(p.y)<half.y;
        叹号.gameObject.SetActive(inside);箭头.gameObject.SetActive(!inside);
        if(inside)叹号.anchoredPosition=new Vector2(Mathf.Clamp(p.x,-half.x+36,half.x-36),Mathf.Clamp(p.y,-half.y+36,half.y-36));
        else
        {
            if(p.sqrMagnitude<.001f)p=Vector2.right;
            float scale=Mathf.Min((half.x-65)/Mathf.Max(Mathf.Abs(p.x),.001f),(half.y-65)/Mathf.Max(Mathf.Abs(p.y),.001f));
            箭头.anchoredPosition=p*scale;箭头.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(p.y,p.x)*Mathf.Rad2Deg-90);
        }
    }
    void 隐藏标记(){if(叹号!=null)叹号.gameObject.SetActive(false);if(箭头!=null)箭头.gameObject.SetActive(false);}
}
