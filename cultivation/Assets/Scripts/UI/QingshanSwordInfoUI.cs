using UnityEngine;
using UnityEngine.UI;

/// <summary>青山剑右键信息窗：平放的模型、剑数与下一阶段击杀进度。</summary>
public class QingshanSwordInfoUI : MonoBehaviour
{
    UIPanelData data;GameObject window;UIMountPage preview;Image fill;Text progress,status;
    float oldScale;bool paused;public bool IsOpen{get;private set;}
    readonly System.Collections.Generic.Dictionary<CanvasGroup,Vector3> hud=new System.Collections.Generic.Dictionary<CanvasGroup,Vector3>();
    public static bool Open(UIPanelData source){if(source==null)return false;var ui=source.GetComponent<QingshanSwordInfoUI>();if(ui==null)ui=source.gameObject.AddComponent<QingshanSwordInfoUI>();ui.data=source;return ui.TryOpen();}
    bool Equipped=>data!=null && data.当前法宝!=null && data.当前法宝.法宝id==QingshanSwordTreasure.法宝id && data.已拥有(data.当前法宝);
    Text Label(string name,Transform parent,string text,int size,float y,float h){var t=UIBuildUtils.CreateText(name,parent,Resources.Load<Font>("Fonts/SimHei")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),text,size,TextAnchor.MiddleCenter,new Color(.94f,.93f,.83f));UIBuildUtils.Place(t.rectTransform,new Vector2(.1f,y),new Vector2(.9f,y+h),Vector2.zero,Vector2.zero);t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
    void Build(){
        window=new GameObject("青山剑信息窗口",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));window.SetActive(false);
        var canvas=window.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2460;
        var scaler=window.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var dim=UIBuildUtils.CreateImage("Dim",window.transform,new Color(0,0,0,.35f));UIBuildUtils.Stretch(dim.rectTransform);dim.raycastTarget=true;
        var body=UIBuildUtils.CreateImage("SwordInformation",window.transform,new Color(.025f,.055f,.045f,.9f));body.sprite=StationInteractor.取圆角();body.type=Image.Type.Sliced;body.raycastTarget=true;
        body.rectTransform.anchorMin=body.rectTransform.anchorMax=new Vector2(.5f,.5f);body.rectTransform.sizeDelta=new Vector2(900,520);
        Label("Title",body.transform,"青山剑",30,.86f,.1f);
        var image=UIBuildUtils.CreateRect("HorizontalSword",body.transform).gameObject.AddComponent<RawImage>();UIBuildUtils.Place(image.rectTransform,new Vector2(.16f,.4f),new Vector2(.84f,.83f),Vector2.zero,Vector2.zero);
        preview=image.gameObject.AddComponent<UIMountPage>();preview.预览图=image;preview.data=data;preview.正交展示=true;preview.背景色=Color.clear;preview.取景留白=1.14f;preview.贴图边长=1024;preview.可以拖动旋转=false;preview.自定义展示旋转=true;preview.展示模型旋转=new Vector3(225,90,0);preview.正交取景倍率=.4f;
        var track=UIBuildUtils.CreateImage("GrowthTrack",body.transform,new Color(.45f,.55f,.5f,.3f));UIBuildUtils.Place(track.rectTransform,new Vector2(.15f,.30f),new Vector2(.85f,.318f),Vector2.zero,Vector2.zero);
        fill=UIBuildUtils.CreateImage("GrowthProgress",track.transform,new Color(.67f,.85f,.71f));UIBuildUtils.Stretch(fill.rectTransform);
        progress=Label("NextEvolution",body.transform,"",22,.19f,.1f);status=Label("ActiveSkill",body.transform,"",19,.09f,.08f);
        var close=UIBuildUtils.CreateImage("Close",body.transform,new Color(.12f,.2f,.16f,.7f));UIBuildUtils.Place(close.rectTransform,new Vector2(.92f,.88f),new Vector2(.98f,.98f),Vector2.zero,Vector2.zero);close.raycastTarget=true;close.sprite=StationInteractor.取圆角();close.type=Image.Type.Sliced;close.gameObject.AddComponent<Button>().onClick.AddListener(Close);Label("Label",close.transform,"×",28,0,1);
    }
    public bool TryOpen(){
        if(!Equipped || IsOpen || UiEscRegistry.SceneInputBlocked)return false;if(window==null)Build();
        oldScale=Time.timeScale;paused=true;IsOpen=true;UiEscRegistry.SetSceneInputBlocked(this,true);window.SetActive(true);
        preview.Refresh(data.当前法宝);preview.设置观察角度(0,8);Refresh();
        foreach(var canvas in FindObjectsOfType<Canvas>(true)){if(canvas.name!="HudCanvas" && canvas.name!="QuestGuideCanvas" && canvas.name!="ChronicleCanvas")continue;var group=canvas.GetComponent<CanvasGroup>();if(group==null)group=canvas.gameObject.AddComponent<CanvasGroup>();hud[group]=new Vector3(group.alpha,group.interactable?1:0,group.blocksRaycasts?1:0);group.alpha=0;group.interactable=false;group.blocksRaycasts=false;}
        Time.timeScale=0;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;return true;
    }
    void Refresh(){int count=data.青山剑数量;float p=count==9?1:(Mathf.Clamp(data.青山剑有效击杀,0,400)%50)/50f;fill.rectTransform.anchorMax=new Vector2(p,1);fill.rectTransform.offsetMax=Vector2.zero;
        progress.text=count==9?"九剑圆满 · 成长已完成":"当前 "+count+" 剑 · 距离下一剑还需击杀 "+(50-data.青山剑有效击杀%50)+" 个同级或更高级妖魔";
        status.text=count>=3?"E · 万剑归宗已解锁":"三剑解锁万剑归宗 · 当前仍可自动御剑";
    }
    void Update(){if(IsOpen){if(!Equipped || Input.GetKeyDown(KeyCode.Escape))Close();else Refresh();}}
    public void Close(){if(!IsOpen)return;IsOpen=false;if(window!=null)window.SetActive(false);UiEscRegistry.SetSceneInputBlocked(this,false);if(paused){Time.timeScale=oldScale;paused=false;}foreach(var p in hud)if(p.Key!=null){p.Key.alpha=p.Value.x;p.Key.interactable=p.Value.y>0;p.Key.blocksRaycasts=p.Value.z>0;}hud.Clear();UiEscRegistry.NotifyClosed();}
    void OnDisable()=>Close();void OnDestroy(){Close();if(window!=null)Destroy(window);}
}
