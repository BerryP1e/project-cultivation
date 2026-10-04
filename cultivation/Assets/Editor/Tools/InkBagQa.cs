using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Object;

/// <summary>背包真实按钮与数据链路验收；临时物品不存档、不写资产或场景。</summary>
public static class InkBagQa
{
    static string status="idle";
    public static string Run(string command)
    {
        if(command=="status") return status;
        if(!Application.isPlaying) return "FAIL requires Play";
        var panel=FindObjectOfType<CharacterPanelUI>(true);
        if(command=="inspect") {
            var bag=panel.tabs[0].page.GetComponent<UIInkBagPage>();
            var fade=bag.GetComponentInChildren<UIInkViewportFade>(true);
            var log=new StringBuilder("Channels="+fade.GetComponentInParent<Canvas>().additionalShaderChannels+" Bounds="+fade.Material.GetVector("_FadeBounds")+"\n");
            Canvas.ForceUpdateCanvases();
            foreach(var cell in bag.GetComponentsInChildren<UIInkWaterfallCell>()) {
                var image=cell.transform.Find("InkItemShadow").GetComponent<Image>();
                var mesh=image.canvasRenderer.GetMesh();
                var uv=new List<Vector4>(); mesh.GetUVs(1,uv);
                log.AppendLine(cell.Entry.DisplayName+" at="+cell.Rect.anchoredPosition+" shader="+image.material.shader.name+" uv1="+(uv.Count>0 ? uv[0].ToString() : "none")+" Bounds="+(image.material.shader.name=="Cultivation/UI/InkViewportFade" ? image.materialForRendering.GetVector("_FadeBounds").ToString() : "in flight"));
            }
            return log.ToString();
        }
        if(command=="validate") return Validate(panel);
        if(status=="recording") return "FAIL recording active";
        status="recording"; panel.StartCoroutine(Record(panel)); return "OK bag recording started";
    }
    static string Validate(CharacterPanelUI panel)
    {
        var log=new StringBuilder(); int failures=0;
        Action<string,bool> check=(label,ok)=> { log.AppendLine((ok ? "PASS " : "FAIL ")+label); if(!ok) failures++; };
        bool opened=panel.IsOpen; var oldTab=panel.CurrentTab; bool reduced=UIInkMotion.减少动效;
        panel.SetOpen(true); panel.ShowTab(CharacterTab.背包);
        var bag=panel.tabs[0].page.GetComponent<UIInkBagPage>();
        if(bag==null || bag.Waterfall==null) return "FAIL missing bag page";
        var list=bag.Waterfall.Owner; var data=list.data;
        var saved=new List<ItemDefinition>(data.物品);
        var created=new List<ItemDefinition>();
        var effect=ScriptableObject.CreateInstance<InkBagQaUseEffect>();
        try {
            check("no ambiguous opening button",bag.transform.Find("SkipBagOpening")==null);
            check("original use button has shared ink-label styling",list.infoTarget.actionButton.GetComponent<UIInkActionButton>()!=null);
            var fade=bag.GetComponentInChildren<UIInkViewportFade>(true);
            var fadeShader=Shader.Find("Cultivation/UI/InkViewportFade");
            check("pixel ink-edge shader compiles",fade!=null && fade.Material!=null && fadeShader!=null && !ShaderUtil.ShaderHasError(fadeShader));
            check("detail no longer uses parchment",bag.transform.Find("Info/InkDetailBackground")!=null || list.infoTarget.transform.Find("InkDetailBackground")!=null);
            check("original UIEntryList and UIEntryInfo own selection",list.inkWaterfall==bag.Waterfall && list.infoTarget!=null);
            check("panel pauses simulation",Time.timeScale==0);
            for(int i=0;i<240;i++) {
                var item=ScriptableObject.CreateInstance<ItemDefinition>(); item.物品名="临时验收药材 "+i;
                created.Add(item);
            }
            data.物品=new List<ItemDefinition>(created); data.RaiseChanged(); bag.Skip();
            check("landed cells actually render with the fade shader",Array.TrueForAll(bag.GetComponentsInChildren<UIInkWaterfallCell>(),x=>x.transform.Find("Name").GetComponent<Text>().material.shader==fadeShader && x.GetComponent<UIInkFluid>().渐隐启用));
            check("240 entries use a bounded visible pool",bag.Waterfall.EntryCount==240 && bag.Waterfall.PoolCount<64);
            float largeCatalogScale=bag.Waterfall.ItemScale;
            int pool=bag.Waterfall.PoolCount;
            bag.Waterfall.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-1)});
            check("wheel scrolls upward in paused panel",bag.Waterfall.ScrollOffset>0);
            for(int i=0;i<40;i++) bag.Waterfall.ScrollBy(500);
            check("looping scroll reuses cells",bag.Waterfall.PoolCount==pool);
            var usable=created[0]; usable.可使用=true; usable.使用效果=effect;
            data.物品=new List<ItemDefinition>{usable,usable,usable}; data.RaiseChanged();
            list.Select(usable);
            check("duplicate items aggregate quantity without changing inventory",bag.Waterfall.EntryCount==1 && bag.Waterfall.Quantity(usable)==3 && data.物品.Count==3);
            check("small inventory enlarges items automatically",bag.Waterfall.ItemScale>largeCatalogScale);
            check("opening parcel disappears after playback",!bag.transform.Find("InkBagParcel").gameObject.activeSelf);
            check("single item does not repeat down the whole viewport",Array.FindAll(panel.tabs[0].page.GetComponentsInChildren<UIInkWaterfallCell>(true),x=>x.gameObject.activeInHierarchy).Length==1);
            check("original selection refreshes original detail",list.infoTarget.Current==usable);
            check("use action available",list.infoTarget.actionButton.gameObject.activeInHierarchy && list.infoTarget.actionButton.interactable);
            list.infoTarget.actionButton.onClick.Invoke();
            check("original use button consumes exactly one and synchronizes count",effect.Uses==1 && data.物品数量(usable)==2 && bag.Waterfall.Quantity(usable)==2);
            list.infoTarget.actionButton.onClick.Invoke(); list.infoTarget.actionButton.onClick.Invoke();
            check("last consumed item disappears and detail clears",bag.Waterfall.EntryCount==0 && list.infoTarget.Current==null && data.物品.Count==0);
            data.物品=new List<ItemDefinition>{created[1]}; data.RaiseChanged(); list.Select(created[1]);
            check("non-usable material hides use action",!list.infoTarget.actionButton.gameObject.activeSelf);
            check("missing data icon is not replaced with invented art",created[1].DisplayIcon==null);
            UIInkMotion.减少动效=true; bag.Skip();
            check("reduced motion skips opening and flight",bag.ParcelFrame==7 && !bag.Waterfall.Opening);
            panel.SetOpen(false); check("close restores timeScale",Time.timeScale>0);
        } finally {
            data.物品=saved; data.RaiseChanged(); UIInkMotion.减少动效=reduced;
            panel.SetOpen(opened); if(opened) panel.ShowTab(oldTab);
            foreach(var item in created) Destroy(item); Destroy(effect);
        }
        log.AppendLine("Failures="+failures);
        string result=log.ToString();
        File.WriteAllText(Path.Combine(Application.dataPath,"../screenshots/InkUI-bag-checks-"+Screen.width+"x"+Screen.height+".txt"),result);
        return result;
    }
    static IEnumerator Record(CharacterPanelUI panel)
    {
        string suffix=Screen.width+"x"+Screen.height;
        string directory=Path.Combine(Application.dataPath,"../screenshots"); Directory.CreateDirectory(directory);
        string temp=Path.Combine(Path.GetTempPath(),"inkbag-"+Guid.NewGuid().ToString("N")+".mp4");
        string final=Path.Combine(directory,"InkUI-bag-"+suffix+"-half-speed.mp4");
        bool opened=panel.IsOpen; var oldTab=panel.CurrentTab;
        MediaEncoder encoder=null; Texture2D frame=null;
        try {
            panel.SetOpen(false); yield return new WaitForSecondsRealtime(.4f);
            encoder=new MediaEncoder(temp,new VideoTrackAttributes{frameRate=new MediaRational(5),width=(uint)Screen.width,height=(uint)Screen.height,includeAlpha=false,bitRateMode=VideoBitrateMode.Medium});
            UIInkBagPage bag=null;
            for(int i=0;i<100;i++) {
                if(i==3) { panel.SetOpen(true); panel.ShowTab(CharacterTab.背包); bag=panel.tabs[0].page.GetComponent<UIInkBagPage>(); }
                if(i==24) bag.Waterfall.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-2)});
                if(i==36) { var cells=panel.tabs[0].page.GetComponentsInChildren<UIInkWaterfallCell>(); if(cells.Length>0) cells[cells.Length/2].GetComponent<Button>().onClick.Invoke(); }
                if(i==48) bag.Waterfall.ScrollBy(2500);
                if(i==65) { var material=bag.Waterfall.Owner.data.物品.Find(x=>x!=null && !x.可使用); if(material!=null) bag.Waterfall.Owner.Select(material); }
                if(i==80) panel.SetOpen(false);
                yield return new WaitForSecondsRealtime(.1f); yield return new WaitForEndOfFrame();
                frame=ScreenCapture.CaptureScreenshotAsTexture(); encoder.AddFrame(frame);
                if(i==15 || i==38 || i==67) File.WriteAllBytes(Path.Combine(directory,"UI_背包_after_"+suffix+"-frame-"+i+".png"),frame.EncodeToPNG());
                Destroy(frame); frame=null;
            }
            encoder.Dispose(); encoder=null; File.Copy(temp,final,true); File.Delete(temp);
            status="DONE Video="+final;
        } finally {
            encoder?.Dispose(); if(frame!=null) Destroy(frame);
            panel.SetOpen(opened); if(opened) panel.ShowTab(oldTab);
            if(status=="recording") status="FAILED see Console";
        }
    }
}

public class InkBagQaUseEffect : 物品使用效果
{
    public int Uses;
    public override bool 使用(物品使用请求 request) { Uses++; return true; }
}
