using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class InkSkillsQa
{
    static string status="idle";
    public static string Run(string command){
        if(command=="status")return status;
        if(command=="import"){
            foreach(var file in new[]{"fx-star-map.png","fx-star-particle.png"}){
                var importer=AssetImporter.GetAtPath("Assets/resources/UI/InkUI/SkillsDynamic/"+file) as TextureImporter;if(importer==null)return "FAIL missing "+file;
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }return "OK skills alpha assets imported; no scene writes";
        }
        if(!Application.isPlaying)return "FAIL requires Play";
        var panel=Object.FindObjectOfType<CharacterPanelUI>(true);var page=panel.tabs[2].page.GetComponent<UIInkSkillsPage>();if(page==null || page.瀑布流==null)return "FAIL missing skills view";
        if(command=="hud"){
            bool open=panel.IsOpen;var tab=panel.CurrentTab;panel.SetOpen(false);var states=new Dictionary<CanvasGroup,Vector3>();
            foreach(var canvas in Object.FindObjectsOfType<Canvas>(true))if(canvas.name=="HudCanvas" || canvas.name=="ChronicleCanvas" || canvas.name=="QuestGuideCanvas"){var group=canvas.GetComponent<CanvasGroup>();if(group!=null)states[group]=new Vector3(group.alpha,group.interactable?1:0,group.blocksRaycasts?1:0);}
            var late=new GameObject("QuestGuideCanvas",typeof(Canvas),typeof(CanvasGroup));var special=late.GetComponent<CanvasGroup>();special.alpha=.37f;special.blocksRaycasts=false;states[special]=new Vector3(.37f,1,0);
            bool hidden=true,restored=true;
            try{panel.SetOpen(true);foreach(var pair in states)hidden&=pair.Key.alpha==0 && !pair.Key.blocksRaycasts && !pair.Key.interactable;panel.SetOpen(false);foreach(var pair in states)restored&=Mathf.Approximately(pair.Key.alpha,pair.Value.x) && pair.Key.interactable==(pair.Value.y>0) && pair.Key.blocksRaycasts==(pair.Value.z>0);}
            finally{Object.Destroy(late);panel.SetOpen(open);if(open)panel.ShowTab(tab);}
            return (hidden&&restored?"PASS":"FAIL")+" HUD hidden on open and exact previous state restored on close; canvases="+states.Count;
        }
        if(command=="inspect"){var g=page.星图.GetComponentInChildren<UIInkConstellation>();return g==null?"missing graph":"graph enabled="+g.enabled+" active="+g.gameObject.activeInHierarchy+" nodes="+(g.Nodes==null?-1:g.Nodes.Length)+" progress="+g.Progress+" segments="+g.SegmentCount+" rect="+g.rectTransform.rect+" cull="+g.canvasRenderer.cull+" color="+g.color+" material="+g.material.name;}
        if(command=="record"){if(status=="recording")return "FAIL recording active";status="recording";panel.StartCoroutine(Record(panel,page));return "OK skills recording started";}
        var log=new StringBuilder();int passed=0,failed=0;System.Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++;};
        check(page.星图.slots.Count==6,"six original slot controllers");
        var graph=page.星图.GetComponentInChildren<UIInkConstellation>();check(graph!=null && graph.SegmentCount==140,"live constellation renders all five curves");
        for(int i=0;i<6;i++)check(page.星图.slots[i].index==i && Vector2.Distance(((RectTransform)page.星图.slots[i].transform).anchorMin,page.星位锚点(i))<.001f,"fixed star index "+(i+1));
        check(page.已悟神通.source==ListSource.神通 && page.生效被动.source==ListSource.生效被动,"mastered versus enabled passive sources preserved");
        check(page.被动星数==page.星图.data.GetPassiveAbilities().Count,"enabled passives get separate stars without using slots");
        bool free=true;foreach(var star in page.星图.GetComponentsInChildren<UIInkPassiveStar>())free&=page.星位空白有效(((RectTransform)star.transform).anchorMin,star.Entry);
        check(free,"passives occupy distinct free space away from active slots");
        var volume=page.星图.GetComponent<UIInkSkillVolume>();check(volume!=null && volume.三维就绪 && volume.三维星位数==6,"six real 3D previews rendered to transparent target");
        check(page.已悟神通.GetComponent<ScrollRect>().verticalScrollbar!=null,"shared backpack scrollbar on mastered library");
        check(!page.生效被动.gameObject.activeSelf,"enabled passive list replaced by satellite stars");
        foreach(var star in page.星图.GetComponentsInChildren<UIInkPassiveStar>()){
            star.OnPointerEnter(new PointerEventData(EventSystem.current));check(star.信息可见,"passive satellite hover reveals information");
            star.GetComponent<Button>().onClick.Invoke();check(page.详情.Current==star.Entry && page.已悟神通.Selected==star.Entry,"passive satellite click selects original entry");
            star.OnPointerExit(new PointerEventData(EventSystem.current));check(!star.信息可见,"passive satellite exit hides tooltip");break;
        }
        var entries=page.星图.data.GetAbilities();var keep=page.已悟神通.Selected;
        var temporary=new List<IPanelEntry>();
        try{
            for(int i=0;i<240;i++){var active=ScriptableObject.CreateInstance<ActiveDivineAbility>();active.name="InkQA-"+i;active.神通名称="验收神通"+i;active.神通id="inkqa_"+i;temporary.Add(active);}
            page.已悟神通.SetEntries(temporary);page.瀑布流.Skip();
            check(page.瀑布流.EntryCount==240 && page.瀑布流.PoolCount<64,"240 abilities use bounded pool");
            int pool=page.瀑布流.PoolCount;float before=page.瀑布流.ScrollOffset;page.瀑布流.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-2)});
            check(page.瀑布流.ScrollOffset!=before,"wheel scrolls in paused UI");for(int i=0;i<40;i++)page.瀑布流.ScrollBy(1000);check(page.瀑布流.PoolCount==pool,"looping scroll reuses pool");
        }finally{page.已悟神通.SetEntries(entries);page.瀑布流.Skip();if(keep!=null)page.已悟神通.Select(keep);foreach(var entry in temporary)Object.Destroy(entry as Object);}
        UIInkWaterfallCell drag=null;foreach(var cell in page.已悟神通.GetComponentsInChildren<UIInkWaterfallCell>())if(cell.Entry is ActiveDivineAbility){drag=cell;break;}
        if(drag!=null){var saved=page.星图.data.主动技能[0];var second=page.星图.data.主动技能[1];var ability=drag.Entry as Object;
            try{
                drag.OnBeginDrag(new PointerEventData(EventSystem.current));check(UIDragContext.Entry==drag.Entry && UIDragContext.Ghost.GetComponentInParent<RectMask2D>()==null,"original drag context outside masks");
                check(UIDragContext.Spark!=null && UIDragContext.Spark.粒子数==24,"drag has visible cursor light and bounded particle pool");
                UIDragContext.Move(new Vector2(Screen.width*.5f,Screen.height*.5f));
                check(Vector2.Distance(UIDragContext.Spark.transform.position,new Vector2(Screen.width*.5f,Screen.height*.5f))<1,"drag light follows actual pointer position");
                check(UIDragContext.Ghost.GetComponent<UIInkFluid>()==null,"ink drift only moves child art, never drag root");
                page.星图.slots[0].OnDrop(new PointerEventData(EventSystem.current));check(page.星图.data.主动技能[0]==ability && !UIDragContext.Dragging,"drop uses original EquipToSlot");
                check(UIDragContext.Spark==null,"drag light clears on accepted drop");
                var source=page.星图.slots[0].GetComponent<UIInkSkillStar>();source.OnBeginDrag(new PointerEventData(EventSystem.current));check(UIDragContext.OriginSlot==0,"equipped star is a drag source");
                int changes=0;System.Action changed=()=>changes++;page.星图.data.Changed+=changed;
                try{page.星图.slots[1].OnDrop(new PointerEventData(EventSystem.current));}finally{page.星图.data.Changed-=changed;}
                check(page.星图.data.主动技能[1]==ability && page.星图.data.主动技能[0]==second && changes==1,"slot swap is atomic and retains both abilities");
                page.星图.slots[1].GetComponent<UIInkSkillStar>().OnBeginDrag(new PointerEventData(EventSystem.current));page.瀑布流.OnDrop(new PointerEventData(EventSystem.current));
                check(page.星图.data.主动技能[1]==null && page.星图.data.GetAbilities().Contains(ability as IPanelEntry),"drag back to library unequips without losing learned ability");
                page.星图.data.EquipToSlot(0,ability);source.OnBeginDrag(new PointerEventData(EventSystem.current));source.OnEndDrag(new PointerEventData(EventSystem.current));check(page.星图.data.主动技能[0]==ability && !UIDragContext.Dragging,"cancel preserves equipment and returns ghost");
            }finally{UIDragContext.End(true);if(saved==null)page.星图.data.ClearSlot(0);else page.星图.data.EquipToSlot(0,saved);if(second==null)page.星图.data.ClearSlot(1);else page.星图.data.EquipToSlot(1,second);}
        }
        foreach(var cell in page.已悟神通.GetComponentsInChildren<UIInkWaterfallCell>())if(cell.Entry is PassiveDivineAbility){cell.OnBeginDrag(new PointerEventData(EventSystem.current));check(!UIDragContext.Dragging,"passive cannot equip active slot");cell.OnEndDrag(new PointerEventData(EventSystem.current));break;}
        var passive=entries.Find(x=>x is PassiveDivineAbility && !x.DisplayName.Contains("御风")) as PassiveDivineAbility;
        if(passive!=null){bool enabled=page.星图.data.IsPassiveEnabled(passive);try{page.详情.Show(passive);page.详情.actionButton.onClick.Invoke();check(page.星图.data.IsPassiveEnabled(passive)!=enabled,"detail toggles passive through original action");check(page.被动星数==page.星图.data.GetPassiveAbilities().Count,"passive stars synchronize on Changed");}finally{if(page.星图.data.IsPassiveEnabled(passive)!=enabled)page.星图.data.TogglePassive(passive);if(keep!=null)page.已悟神通.Select(keep);}}
        check(page.详情.GetComponent<Image>()==null || !page.详情.GetComponent<Image>().enabled,"old paper detail removed");
        check(page.详情.actionButton.GetComponent<UIInkActionButton>()!=null,"shared ink operation button");
        bool attraction=true;foreach(var particle in page.星图.GetComponentsInChildren<UIInkStarParticle>())attraction&=particle.牵引位移.magnitude<=5.01f;
        check(attraction,"star attraction bounded to five pixels");
        CheckUniqueActive(page.星图.data,check);
        log.AppendLine("TOTAL "+passed+" passed / "+failed+" failed; Screen="+Screen.width+"x"+Screen.height+"; Pool="+page.瀑布流.PoolCount);return log.ToString();
    }
    static void CheckUniqueActive(UIPanelData data,System.Action<bool,string> check){
        var saved=data.主动技能.ToArray();var pending=data.待装备神通;
        var first=ScriptableObject.CreateInstance<ActiveDivineAbility>();first.神通id="inkqa_unique";first.神通名称="唯一性验收";
        var alias=ScriptableObject.CreateInstance<ActiveDivineAbility>();alias.神通id=first.神通id;alias.神通名称=first.神通名称;
        var other=ScriptableObject.CreateInstance<ActiveDivineAbility>();other.神通id="inkqa_other";
        int changes=0;System.Action changed=()=>changes++;
        try{
            for(int i=0;i<saved.Length;i++)data.ClearSlot(i);
            data.EquipToSlot(0,first);data.Changed+=changed;
            data.EquipToSlot(2,first);
            check(data.主动技能[0]==null && data.主动技能[2]==first && changes==1,"same active ability moves between slots with one notification");
            changes=0;data.EquipToSlot(3,alias);
            check(data.主动技能[2]==null && data.主动技能[3]==alias && changes==1,"different asset with same skill id cannot duplicate equipment");
            data.BeginPendingEquip(first);changes=0;
            check(data.HandleSlotClicked(4) && data.主动技能[3]==null && data.主动技能[4]==first && data.待装备神通==null && changes==1,"pending click equipment also moves existing ability");
            data.EquipToSlot(1,other);data.BeginPendingEquip(first);
            check(!data.HandleSlotClicked(1) && data.主动技能[1]==other && data.主动技能[4]==first,"pending equipment preserves occupied-slot rejection");
            data.CancelPendingEquip();changes=0;
            check(data.交换主动槽(4,1,first) && data.主动技能[1]==first && data.主动技能[4]==other && changes==1,"unique equipment retains atomic occupied-slot swap");
            data.主动技能[0]=alias;data.HasEmptySlot();
            check(data.主动技能[0]==alias && data.主动技能[1]==null,"legacy duplicate state keeps first occurrence and clears later slots");
            data.ClearSlot(0);data.EquipToSlot(2,first);data.EquipToSlot(-1,first);
            check(data.主动技能[2]==first,"invalid target never clears existing equipment");
        }finally{
            data.Changed-=changed;data.CancelPendingEquip();for(int i=0;i<saved.Length;i++)data.ClearSlot(i);
            for(int i=0;i<saved.Length;i++)if(saved[i]!=null)data.EquipToSlot(i,saved[i]);
            if(pending!=null)data.BeginPendingEquip(pending);Object.Destroy(first);Object.Destroy(alias);Object.Destroy(other);
        }
    }
    static IEnumerator Record(CharacterPanelUI panel,UIInkSkillsPage page){
        string suffix=Screen.width+"x"+Screen.height;string dir=Path.Combine(Application.dataPath,"../screenshots");Directory.CreateDirectory(dir);
        string temp=Path.Combine(Path.GetTempPath(),"inkskills-"+System.Guid.NewGuid().ToString("N")+".mp4"),final=Path.Combine(dir,"InkUI-skills-"+suffix+"-half-speed.mp4");
        bool opened=panel.IsOpen;var oldTab=panel.CurrentTab;MediaEncoder encoder=null;Texture2D frame=null;
        var saved=page.星图.data.主动技能[0];var second=page.星图.data.主动技能[1];UIInkWaterfallCell drag=null;
        try{
            panel.SetOpen(true);panel.ShowTab(CharacterTab.背包);yield return new WaitForSecondsRealtime(.3f);
            for(int i=0;i<50;i++){
                if(i>=2 && panel.CurrentTab!=CharacterTab.神通)panel.ShowTab(CharacterTab.神通);
                if(i==2)panel.ShowTabByIndex(2);
                if(i==20)page.瀑布流.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-2)});
                if(i==22){foreach(var cell in page.已悟神通.GetComponentsInChildren<UIInkWaterfallCell>())if(cell.Entry is ActiveDivineAbility){drag=cell;break;}if(drag!=null)drag.OnBeginDrag(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,drag.transform.position)});}
                if(i==24 && drag!=null)drag.OnDrag(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,page.星图.slots[0].transform.position)});
                if(i==26 && drag!=null)page.星图.slots[0].OnDrop(new PointerEventData(EventSystem.current));
                if(i==30)foreach(var particle in page.星图.GetComponentsInChildren<UIInkStarParticle>())particle.调试局部鼠标=new Vector2(40,5);
                if(i==30)page.星图.slots[0].GetComponent<UIInkSkillStar>().OnBeginDrag(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,page.星图.slots[0].transform.position)});
                if(i==32)UIDragContext.Move(RectTransformUtility.WorldToScreenPoint(null,page.星图.slots[1].transform.position));
                if(i==34)page.星图.slots[1].OnDrop(new PointerEventData(EventSystem.current));
                if(i==40)page.星图.slots[1].GetComponent<UIInkSkillStar>().OnBeginDrag(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,page.星图.slots[1].transform.position)});
                if(i==42)UIDragContext.Move(RectTransformUtility.WorldToScreenPoint(null,page.已悟神通.transform.position));
                if(i==44)page.瀑布流.OnDrop(new PointerEventData(EventSystem.current));
                if(i==28){var cells=page.已悟神通.GetComponentsInChildren<UIInkWaterfallCell>();if(cells.Length>0)cells[0].GetComponent<Button>().onClick.Invoke();}
                if(i==35 && page.生效被动.data.GetPassiveAbilities().Count>0)page.详情.Show(page.生效被动.data.GetPassiveAbilities()[0]);
                if(i==35){var stars=page.星图.GetComponentsInChildren<UIInkPassiveStar>();if(stars.Length>0)stars[0].OnPointerEnter(new PointerEventData(EventSystem.current));}
                if(i==39)foreach(var star in page.星图.GetComponentsInChildren<UIInkPassiveStar>())star.OnPointerExit(new PointerEventData(EventSystem.current));
                yield return new WaitForSecondsRealtime(.1f);if(i>=2 && panel.CurrentTab!=CharacterTab.神通)panel.ShowTab(CharacterTab.神通);yield return new WaitForEndOfFrame();frame=ScreenCapture.CaptureScreenshotAsTexture();
                if(encoder==null){suffix=frame.width+"x"+frame.height;final=Path.Combine(dir,"InkUI-skills-"+suffix+"-half-speed.mp4");encoder=new MediaEncoder(temp,new VideoTrackAttributes{frameRate=new MediaRational(5),width=(uint)frame.width,height=(uint)frame.height,includeAlpha=false,bitRateMode=VideoBitrateMode.Medium});}
                encoder.AddFrame(frame);
                if(i==15)File.WriteAllBytes(Path.Combine(dir,"UI_神通_after_"+suffix+".png"),frame.EncodeToPNG());if(i==7 || i==24 || i==38)File.WriteAllBytes(Path.Combine(dir,"UI_神通_after_"+suffix+"-frame-"+i+".png"),frame.EncodeToPNG());Object.Destroy(frame);frame=null;
            }encoder.Dispose();encoder=null;File.Copy(temp,final,true);File.Delete(temp);status="DONE "+final;
        }finally{encoder?.Dispose();if(frame!=null)Object.Destroy(frame);UIDragContext.End(true);if(saved==null)page.星图.data.ClearSlot(0);else page.星图.data.EquipToSlot(0,saved);if(second==null)page.星图.data.ClearSlot(1);else page.星图.data.EquipToSlot(1,second);foreach(var particle in page.星图.GetComponentsInChildren<UIInkStarParticle>(true))particle.调试局部鼠标=null;foreach(var star in page.星图.GetComponentsInChildren<UIInkPassiveStar>())star.OnPointerExit(new PointerEventData(EventSystem.current));panel.SetOpen(opened);if(opened)panel.ShowTab(oldTab);if(status=="recording")status="FAILED see Console";}
    }
}
