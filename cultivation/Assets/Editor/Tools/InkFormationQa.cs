using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class InkFormationQa
{
    static string status="idle";
    public static string Run(string command){
        if(command=="status")return status;if(!Application.isPlaying)return "FAIL requires Play";
        var panel=Object.FindObjectOfType<CharacterPanelUI>(true);var page=panel.tabs[5].page.GetComponent<UIInkFormationPage>();if(page==null)return "FAIL no formation page";var data=page.九宫.data;
        if(command=="inspect")return "models="+page.模型数+" perspective="+!page.透视相机.orthographic+" slots="+page.九宫.slots.Count+" entries="+page.瀑布流.EntryCount+" pool="+page.瀑布流.PoolCount;
        if(command=="record"){if(status=="recording")return "FAIL busy";status="recording";panel.StartCoroutine(Record(panel,page));return "OK recording";}
        if(command=="seed"){Populate(data);return "OK five real catalog spirits";}
        int passed=0,failed=0;var log=new StringBuilder();System.Action<bool,string> check=(ok,text)=>{log.AppendLine((ok?"PASS ":"FAIL ")+text);if(ok)passed++;else failed++;};
        check(!page.透视相机.orthographic,"actual perspective camera");check(page.模型数>0,"player or spirit model rendered");check(page.九宫.slots.Count==9 && !page.九宫.slots[4].button.interactable,"nine original slots with protected player center");
        float far=page.Project(new Vector3(1.5f,0,3)).x-page.Project(new Vector3(-1.5f,0,3)).x,near=page.Project(new Vector3(1.5f,0,-3)).x-page.Project(new Vector3(-1.5f,0,-3)).x;
        check(near>far*1.1f,"near row visibly larger than far row");check(page.真灵.GetComponent<ScrollRect>().verticalScrollbar!=null,"shared backpack scrollbar");
        var entries=data.GetSpirits();var saved=data.战阵站位.ToArray();var pending=data.待上阵真灵;var selected=page.真灵.Selected;var temp=new List<IPanelEntry>();
        try{
            for(int i=0;i<240;i++){var npc=ScriptableObject.CreateInstance<NpcDefinition>();npc.name="InkFormationQA-"+i;temp.Add(npc);}page.真灵.SetEntries(temp);page.瀑布流.Skip();
            check(page.瀑布流.EntryCount==240 && page.瀑布流.PoolCount<64,"240 spirits have bounded virtual pool");float offset=page.瀑布流.ScrollOffset;page.瀑布流.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-2)});check(page.瀑布流.ScrollOffset!=offset,"wheel works while paused");
            foreach(var cell in page.真灵.GetComponentsInChildren<UIInkWaterfallCell>()){check(cell.GetComponentInChildren<UIInkSpiritCloud>()!=null && !cell.transform.Find("ItemIcon").GetComponent<Image>().enabled,"spirit appears as particles without concrete icon");break;}
            page.真灵.SetEntries(entries);page.瀑布流.Skip();for(int i=0;i<saved.Length;i++)if(data.战阵站位[i]!=null)data.UnequipSpirit(data.战阵站位[i]);
            var first=entries[0] as NpcDefinition;var second=entries[1] as NpcDefinition;var canvas=page.GetComponentInParent<Canvas>();
            UIDragContext.Begin(first,canvas.rootCanvas.transform,page.真灵.font);UIDragContext.ApplySpiritGhost();UIDragContext.Move(new Vector2(300,300));
            check(UIDragContext.Ghost.GetComponentInChildren<UIInkSpiritCloud>()!=null && UIDragContext.Ghost.GetComponentInParent<RectMask2D>()==null,"particle drag ghost outside masks");
            page.九宫.slots[4].GetComponent<UIInkFormationSlot>().OnDrop(new PointerEventData(EventSystem.current));check(data.已上阵数量==0,"drag rejects player center");
            page.九宫.slots[0].GetComponent<UIInkFormationSlot>().OnDrop(new PointerEventData(EventSystem.current));check(data.战阵站位[0]==first && !UIDragContext.Dragging,"drop equips through original data");
            UIDragContext.Begin(second,canvas.rootCanvas.transform,page.真灵.font);page.九宫.slots[0].GetComponent<UIInkFormationSlot>().OnDrop(new PointerEventData(EventSystem.current));check(data.战阵站位[0]==first,"occupied drop preserves occupant");UIDragContext.End(true);
            UIDragContext.Begin(first,canvas.rootCanvas.transform,page.真灵.font);page.九宫.slots[1].GetComponent<UIInkFormationSlot>().OnDrop(new PointerEventData(EventSystem.current));check(data.战阵站位[1]==null,"already equipped spirit cannot duplicate");UIDragContext.End(true);
            page.详情.Show(first);page.详情.actionButton.onClick.Invoke();check(!data.IsSpiritOnField(first),"detail action unequips spirit");
            Populate(data);check(data.战阵已满 && data.已上阵数量==5,"five spirit cap preserved");data.BeginPendingSpirit(entries[5] as NpcDefinition);check(!data.HandleSpiritSlotClicked(7) && data.战阵站位[7]==null,"sixth spirit rejected");
        }finally{UIDragContext.End(true);data.CancelPendingSpirit();for(int i=0;i<9;i++)if(data.战阵站位[i]!=null)data.UnequipSpirit(data.战阵站位[i]);for(int i=0;i<9;i++)if(saved[i]!=null){data.BeginPendingSpirit(saved[i]);data.HandleSpiritSlotClicked(i);}if(pending!=null)data.BeginPendingSpirit(pending);page.真灵.SetEntries(entries);page.瀑布流.Skip();if(selected!=null)page.真灵.Select(selected);foreach(var entry in temp)Object.Destroy(entry as Object);}
        return log+"TOTAL "+passed+" passed / "+failed+" failed; Screen="+Screen.width+"x"+Screen.height;
    }
    static void Populate(UIPanelData data){for(int i=0;i<9;i++)if(data.战阵站位[i]!=null)data.UnequipSpirit(data.战阵站位[i]);var entries=data.GetSpirits();int[] slots={0,2,3,5,6};for(int i=0;i<5;i++){data.BeginPendingSpirit(entries[i] as NpcDefinition);data.HandleSpiritSlotClicked(slots[i]);}}
    static IEnumerator Record(CharacterPanelUI panel,UIInkFormationPage page){
        var data=page.九宫.data;var saved=data.战阵站位.ToArray();var pending=data.待上阵真灵;bool opened=panel.IsOpen;var tab=panel.CurrentTab;var selected=page.真灵.Selected;
        string dir=Path.Combine(Application.dataPath,"../screenshots"),temp=Path.Combine(Path.GetTempPath(),"inkformation-"+System.Guid.NewGuid().ToString("N")+".mp4"),final=null;MediaEncoder encoder=null;Texture2D frame=null;
        try{
            panel.SetOpen(true);panel.ShowTabByIndex(0);Populate(data);
            for(int i=0;i<35;i++){
                if(i>=2)panel.ShowTabByIndex(5);if(i==14)page.瀑布流.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-2)});
                if(i==19)foreach(var cell in page.真灵.GetComponentsInChildren<UIInkWaterfallCell>()){cell.OnPointerEnter(new PointerEventData(EventSystem.current));cell.GetComponent<Button>().onClick.Invoke();cell.OnBeginDrag(new PointerEventData(EventSystem.current){position=cell.transform.position});break;}
                if(i==21 && UIDragContext.Dragging)UIDragContext.Move(RectTransformUtility.WorldToScreenPoint(null,page.九宫.slots[7].transform.position));
                if(i==23)UIDragContext.End(true);if(i==26)data.UnequipSpirit(data.战阵站位[0]);
                yield return new WaitForSecondsRealtime(.1f);if(i>=2)panel.ShowTabByIndex(5);yield return new WaitForEndOfFrame();frame=ScreenCapture.CaptureScreenshotAsTexture();string suffix=frame.width+"x"+frame.height;
                if(encoder==null){final=Path.Combine(dir,"InkUI-formation-"+suffix+"-half-speed.mp4");encoder=new MediaEncoder(temp,new VideoTrackAttributes{frameRate=new MediaRational(5),width=(uint)frame.width,height=(uint)frame.height,includeAlpha=false,bitRateMode=VideoBitrateMode.Medium});}
                encoder.AddFrame(frame);if(i==12)File.WriteAllBytes(Path.Combine(dir,"UI_战阵_after_"+suffix+".png"),frame.EncodeToPNG());if(i==21)File.WriteAllBytes(Path.Combine(dir,"UI_战阵_drag_"+suffix+".png"),frame.EncodeToPNG());Object.Destroy(frame);frame=null;
            }encoder.Dispose();encoder=null;File.Copy(temp,final,true);File.Delete(temp);status="DONE "+final;
        }finally{encoder?.Dispose();if(frame!=null)Object.Destroy(frame);UIDragContext.End(true);data.CancelPendingSpirit();for(int i=0;i<9;i++)if(data.战阵站位[i]!=null)data.UnequipSpirit(data.战阵站位[i]);for(int i=0;i<9;i++)if(saved[i]!=null){data.BeginPendingSpirit(saved[i]);data.HandleSpiritSlotClicked(i);}if(pending!=null)data.BeginPendingSpirit(pending);if(selected!=null)page.真灵.Select(selected);panel.SetOpen(opened);if(opened)panel.ShowTab(tab);if(status=="recording")status="FAILED see Console";}
    }
}
