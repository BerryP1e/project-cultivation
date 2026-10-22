using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>获得/学习成功：上方居中黑幕，先压紧上推再展开，同批按最后一条统一淡出。</summary>
public sealed class UIRewardNotice : MonoBehaviour
{
    const float Enter=.20f,Expand=.14f,FadeAt=1.5f,EndAt=1.7f,Gap=16,FadeSide=64;
    sealed class Row
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public float StartY,StartAlpha;
    }
    static UIRewardNotice instance;
    readonly List<Row> rows=new List<Row>();
    RectTransform anchor;
    Font font;
    Sprite backdrop;
    float lastArrival,entryAge;int arrivalFrame;

    public static void 提示(string text)
    {
        if(!Application.isPlaying||string.IsNullOrWhiteSpace(text))return;
        if(!instance)
        {
            var go=new GameObject("RewardNoticeCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(UIRewardNotice));
            DontDestroyOnLoad(go);instance=go.GetComponent<UIRewardNotice>();instance.Build();
        }
        instance.Add(text);
    }

    void Build()
    {
        var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=5001;
        var scaler=GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        font=Resources.Load<Font>("Fonts/SimHei")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        anchor=UIBuildUtils.CreateRect("UpperCenterNotices",transform);anchor.anchorMin=anchor.anchorMax=new Vector2(.5f,.80f);anchor.sizeDelta=Vector2.zero;
        var texture=new Texture2D(256,2,TextureFormat.RGBA32,false){name="RewardNoticeBlackFade",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[512];
        for(int y=0;y<2;y++)for(int x=0;x<256;x++){float alpha=Mathf.SmoothStep(0,1,Mathf.Min(x,255-x)/64f);pixels[y*256+x]=new Color(0,0,0,alpha);}
        texture.SetPixels(pixels);texture.Apply(false,true);
        backdrop=Sprite.Create(texture,new Rect(0,0,256,2),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(64,0,64,0));backdrop.name="RewardNoticeBlackFade";
    }

    void Add(string text)
    {
        foreach(var row in rows){row.StartY=row.Rect.anchoredPosition.y;row.StartAlpha=row.Group.alpha;}
        var rect=UIBuildUtils.CreateRect("RewardNotice",anchor);rect.anchorMin=rect.anchorMax=Vector2.one*.5f;rect.pivot=Vector2.one*.5f;
        var image=rect.gameObject.AddComponent<Image>();image.sprite=backdrop;image.type=Image.Type.Sliced;image.raycastTarget=false;
        var label=UIBuildUtils.CreateText("NoticeText",rect,font,text,26,TextAnchor.MiddleCenter,new Color(.98f,.97f,.90f));label.raycastTarget=false;label.supportRichText=true;
        float canvasWidth=((RectTransform)transform).rect.width;if(canvasWidth<1)canvasWidth=1920;
        float width=Mathf.Min(label.preferredWidth,canvasWidth*.90f-FadeSide*2-32);
        label.rectTransform.sizeDelta=new Vector2(width,44);
        label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;
        float height=Mathf.Max(44,label.preferredHeight+10);
        rect.sizeDelta=new Vector2(width+32+FadeSide*2,height);
        UIBuildUtils.Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(FadeSide+16,5);label.rectTransform.offsetMax=new Vector2(-FadeSide-16,-5);
        var group=rect.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
        float start=-(height+Gap*.5f);
        if(rows.Count>0){var previous=rows[rows.Count-1];start=Mathf.Min(start,previous.Rect.anchoredPosition.y-(previous.Rect.rect.height+height)*.5f-Gap*.5f);}
        rect.anchoredPosition=new Vector2(0,start);
        rows.Add(new Row{Rect=rect,Group=group,StartY=start,StartAlpha=0});
        lastArrival=Time.unscaledTime;entryAge=0;arrivalFrame=Time.frameCount;
    }

    void Update()
    {
        if(rows.Count==0)return;
        if(Time.frameCount==arrivalFrame)return;
        float age=Time.unscaledTime-lastArrival;
        if(age>=EndAt){foreach(var row in rows)Destroy(row.Rect.gameObject);rows.Clear();return;}
        // 物品学习/换装可令触发帧变长；入场单独限步，不能首帧就跳到完全显示。
        entryAge+=Mathf.Min(Time.unscaledDeltaTime,.04f);
        float push=Mathf.SmoothStep(0,1,entryAge/Enter),spread=Mathf.SmoothStep(0,1,(entryAge-Enter)/Expand);
        float fade=1-Mathf.SmoothStep(0,1,(age-FadeAt)/(EndAt-FadeAt));
        float compactY=0,expandedY=0;
        for(int i=rows.Count-1;i>=0;i--)
        {
            var row=rows[i];
            float y=entryAge<Enter?Mathf.Lerp(row.StartY,compactY,push):Mathf.Lerp(compactY,expandedY,spread);
            row.Rect.anchoredPosition=new Vector2(0,y);
            row.Group.alpha=Mathf.Lerp(row.StartAlpha,1,push)*fade;
            if(i>0)
            {
                float halfHeights=(row.Rect.rect.height+rows[i-1].Rect.rect.height)*.5f;
                compactY+=halfHeights+Gap*.5f;expandedY+=halfHeights+Gap;
            }
        }
    }

    void OnDestroy(){if(instance==this)instance=null;if(backdrop){Destroy(backdrop.texture);Destroy(backdrop);}}
}
