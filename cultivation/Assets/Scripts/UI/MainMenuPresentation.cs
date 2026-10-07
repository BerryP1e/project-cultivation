using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Independent painted layers; clocks remain live while gameplay time is paused.</summary>
public sealed class MainMenuPresentation : MonoBehaviour
{
    readonly List<Material> materials = new List<Material>();
    RectTransform nearSeal, farSeal;
    CanvasGroup entrance;
    float age;

    public static void Install(MainMenuUI menu)
    {
        if (menu.GetComponent<MainMenuPresentation>() != null) return;
        menu.gameObject.AddComponent<MainMenuPresentation>().Build(menu);
    }

    public static void Layout(Transform root)
    {
        var title = root.Find("Title")?.GetComponent<Text>();
        if (title != null) {
            title.text = "争渡"; title.fontSize = 104; title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(.91f,.95f,.86f); title.raycastTarget = false;
            Place(title.rectTransform,new Vector2(215,780),new Vector2(310,150));
            var outline=title.GetComponent<Outline>();
            if(outline!=null) { outline.effectColor=new Color(.02f,.12f,.13f,.9f); outline.effectDistance=new Vector2(2,-2); }
        }
        var buttons = root.Find("Buttons") as RectTransform;
        if(buttons!=null) Place(buttons,new Vector2(215,410),new Vector2(260,340));
    }

    void Build(MainMenuUI menu)
    {
        Layout(transform);
        var titleText=transform.Find("Title")?.GetComponent<Text>();
        if(titleText!=null) {
            titleText.enabled=false;
            var logo=Layer(transform,"ZhengDuLogo","title",new Vector2(215,788),new Vector2(345,185),Color.white);
            logo.transform.SetSiblingIndex(titleText.transform.GetSiblingIndex()+1);
        }
        var bg=transform.Find("Background")?.GetComponent<Image>();
        if(bg!=null) { bg.sprite=null; bg.color=Color.white; bg.material=Material(4); bg.raycastTarget=false; }
        var stage=UIBuildUtils.CreateRect("AnimatedLandscape",transform);
        UIBuildUtils.Stretch(stage); stage.SetSiblingIndex(1);
        var landscapeFrame=UIBuildUtils.CreateImage("LandscapeFrame",stage,Color.white);
        UIBuildUtils.Stretch(landscapeFrame.rectTransform);
        landscapeFrame.sprite=Resources.Load<Sprite>("UI/MainMenu/Animated/frame");
        landscapeFrame.raycastTarget=false;
        var far=Layer(stage,"FarSeal","seal-ink",new Vector2(990,550),new Vector2(1720,1720),new Color(.75f,.80f,.73f,.36f));
        far.material=Material(7); farSeal=far.rectTransform;
        var near=Layer(stage,"NearSeal","seal-ink",new Vector2(990,550),new Vector2(1080,1080),new Color(.87f,.89f,.81f,.87f));
        near.material=Material(7); nearSeal=near.rectTransform;
        var energy=UIBuildUtils.CreateImage("FlowingSpiritRibbons",stage,new Color(.29f,.37f,.31f,.24f));
        Place(energy.rectTransform,new Vector2(990,550),new Vector2(1320,1320));
        energy.material=Material(3); energy.raycastTarget=false;
        var vortex=UIBuildUtils.CreateImage("SpiritVortex",stage,Color.white);
        Place(vortex.rectTransform,new Vector2(990,550),new Vector2(950,950));
        vortex.material=Material(2); vortex.raycastTarget=false;
        // Cover the staircase inside the portal while keeping both seals above the vortex.
        vortex.transform.SetSiblingIndex(landscapeFrame.transform.GetSiblingIndex()+1);
        var motes=UIBuildUtils.CreateRect("VortexParticles",stage);
        UIBuildUtils.Stretch(motes); motes.gameObject.AddComponent<MainMenuSpiritMotes>();
        var figure=Layer(stage,"WindSwordsman","figure",new Vector2(1555,293),new Vector2(390,610),Color.white);
        figure.material=Material(0);
        var buttons=new[]{menu.新游戏按钮,menu.读取存档按钮,menu.设置按钮,menu.退出按钮};
        foreach(var button in buttons) if(button!=null) {
            button.transition=Selectable.Transition.None;
            button.image.sprite=null; button.image.color=Color.clear; button.image.raycastTarget=true;
            var curtain=UIBuildUtils.CreateImage("LivingInkCurtain",button.transform,new Color(.17f,.24f,.23f,.94f));
            curtain.transform.SetAsFirstSibling();
            curtain.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4"); curtain.material=Material(5);
            curtain.rectTransform.anchorMin=curtain.rectTransform.anchorMax=new Vector2(.5f,.5f);
            curtain.rectTransform.sizeDelta=new Vector2(330,88);
            button.gameObject.AddComponent<MainMenuButtonMotion>().Initialize(curtain);
            var label=button.GetComponentInChildren<Text>(); if(label!=null) label.fontSize=29;
            if(label!=null) label.color=new Color(.97f,.96f,.87f);
        }
        var caption=UIBuildUtils.CreateText("MenuCaption",transform,menu.字体,"一念入道 · 与天争渡",18,TextAnchor.MiddleCenter,new Color(.67f,.79f,.74f));
        Place(caption.rectTransform,new Vector2(215,680),new Vector2(300,40));
        entrance=stage.gameObject.AddComponent<CanvasGroup>(); entrance.blocksRaycasts=false;
        // Particle sprites are batched UI geometry, with separate depths and velocities.
    }

    Material Material(int mode)
    {
        var shader=Resources.Load<Shader>("UI/MainMenu/MenuAtmosphere");
        if(shader==null) return null;
        var mat=new Material(shader); mat.SetFloat("_Mode",mode); materials.Add(mat); return mat;
    }
    static Image Layer(Transform parent,string name,string asset,Vector2 position,Vector2 size,Color color)
    {
        var image=UIBuildUtils.CreateImage(name,parent,color);
        image.sprite=Resources.Load<Sprite>("UI/MainMenu/Animated/"+asset);
        image.preserveAspect=true; image.raycastTarget=false; Place(image.rectTransform,position,size); return image;
    }
    public static void Place(RectTransform rt,Vector2 position,Vector2 size)
    {
        rt.anchorMin=rt.anchorMax=Vector2.zero; rt.pivot=new Vector2(.5f,.5f);
        rt.anchoredPosition=position; rt.sizeDelta=size;
    }
    void Update()
    {
        age+=Time.unscaledDeltaTime;
        float movement=UIInkMotion.减少动效 ? .12f : 1;
        foreach(var mat in materials) if(mat!=null) mat.SetFloat("_Clock",age*movement);
        if(farSeal!=null) farSeal.localRotation=Quaternion.Euler(0,0,age*1.1f*movement);
        if(nearSeal!=null) nearSeal.localRotation=Quaternion.Euler(0,0,-age*2.1f*movement);
        if(entrance!=null) entrance.alpha=Mathf.SmoothStep(0,1,age/1.2f);
    }
    void OnDestroy() { foreach(var mat in materials) if(mat!=null) Destroy(mat); }
}
