using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections;
using System.IO;
using UnityEditor.Media;

public static class InkRealmQa
{
    static string status="idle";
    public static string Run(string command)
    {
        if(command=="status")return status;
        if(command=="import") {
            var importer=AssetImporter.GetAtPath("Assets/resources/UI/InkUI/Realm/fx-ink-circle.png") as TextureImporter;
            if(importer==null)return "FAIL missing circle";
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp; importer.maxTextureSize=4096;
            importer.SaveAndReimport();return "OK realm alpha sprite imported; no scene writes";
        }
        if(!Application.isPlaying)return "FAIL requires Play";
        var panel=Object.FindObjectOfType<CharacterPanelUI>(true);
        var page=panel.tabs[1].page.GetComponent<UIInkRealmPage>();
        if(page==null || page.墨圈==null)return "FAIL missing realm view";
        if(command=="record") {
            if(status=="recording")return "FAIL recording active";
            status="recording";panel.StartCoroutine(Record(panel));return "OK realm recording started";
        }
        if(command=="inspect") {
            var detail=new StringBuilder();
            foreach(var text in page.属性.container.GetComponentsInChildren<Text>()) {
                if(detail.Length>2200)break;
                detail.AppendLine(text.name+" text="+text.text+" color="+text.color+" rect="+text.rectTransform.rect+" world="+text.transform.position+" reveal="+(text.GetComponent<UIInkReveal>()!=null?text.GetComponent<UIInkReveal>().进度:1));
            }
            return detail.ToString();
        }
        var log=new StringBuilder(); int passed=0,failed=0;
        System.Action<bool,string> check=(ok,name)=>{log.AppendLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;};
        check(Mathf.Approximately(UIInkRealmPage.绘圈时长,1.56f),"brush duration doubled");
        check(Mathf.Approximately(UIInkRealmPage.绘圈进度(.20f),UIInkRealmPage.绘圈进度(.45f)),"brush pauses after initial stroke");
        check(UIInkRealmPage.绘圈进度(1.5f)-UIInkRealmPage.绘圈进度(1.3f)>UIInkRealmPage.绘圈进度(.9f)-UIInkRealmPage.绘圈进度(.7f),"brush accelerates into finish");
        check(page.属性.transform.localPosition.x>page.墨圈.transform.parent.localPosition.x,"attributes on right");
        check(page.属性滚动!=null && page.属性滚动.verticalScrollbar!=null,"scroll and visible thumb retained");
        check(page.属性滚动.verticalScrollbar.handleRect.GetComponent<Image>().sprite==InkUITheme.Load("Dynamic/nav-ink-blot-4"),"shared backpack scrollbar");
        check(page.境界条.progressFill.type==Image.Type.Filled && page.境界条.progressFill.sprite!=null,"real fill with sprite retained");
        if(page.境界条.Cultivation!=null) check(Mathf.Abs(page.境界条.progressFill.fillAmount-page.境界条.Cultivation.进度)<.0001f,"live cultivation progress");
        check(page.墨圈.fillClockwise && page.墨圈.fillOrigin==(int)Image.Origin360.Left,"clockwise brush from left");
        check(page.墨圈.sprite!=null && page.墨圈.material.shader.name=="UI/InkCircle","alpha ink circle bound");
        check(!UnityEditor.ShaderUtil.ShaderHasError(page.墨圈.material.shader),"ring shader compiles");
        var oldCount=page.属性.container.childCount; var row=page.属性.container.GetChild(0);
        page.属性.Rebuild();
        check(page.属性.container.childCount==oldCount && page.属性.container.GetChild(0)==row,"attribute rebuild reuses rows");
        check(oldCount<=AttributeUtil.Count+4,"no duplicated serialized attributes");
        var originalY=page.属性滚动.content.anchoredPosition;
        page.属性滚动.verticalNormalizedPosition=0; Canvas.ForceUpdateCanvases();
        check(page.属性滚动.content.anchoredPosition.y>originalY.y,"scroll reaches remaining attributes");
        page.属性滚动.verticalNormalizedPosition=1;
        check(page.属性.GetComponent<Image>()==null || !page.属性.GetComponent<Image>().enabled,"no old attribute paper panel");
        check(page.境界条.infoText!=null && !string.IsNullOrEmpty(page.境界条.infoText.text),"complete original cultivation information");
        log.AppendLine("TOTAL "+passed+" passed / "+failed+" failed; Screen="+Screen.width+"x"+Screen.height+"; Rows="+oldCount+"; Circle="+page.墨圈.fillAmount);
        return log.ToString();
    }
    static IEnumerator Record(CharacterPanelUI panel)
    {
        string suffix=Screen.width+"x"+Screen.height;
        string dir=Path.Combine(Application.dataPath,"../screenshots");Directory.CreateDirectory(dir);
        string temp=Path.Combine(Path.GetTempPath(),"inkrealm-"+System.Guid.NewGuid().ToString("N")+".mp4");
        string final=Path.Combine(dir,"InkUI-realm-"+suffix+"-half-speed.mp4");
        var oldTab=panel.CurrentTab;bool opened=panel.IsOpen;
        MediaEncoder encoder=null;Texture2D frame=null;
        try {
            panel.SetOpen(true); panel.ShowTab(CharacterTab.背包);yield return new WaitForSecondsRealtime(.3f);
            for(int i=0;i<45;i++) {
                if(i>=2 && panel.CurrentTab!=CharacterTab.境界)panel.ShowTab(CharacterTab.境界);
                if(i==2)panel.ShowTab(CharacterTab.境界);
                if(i==22)panel.tabs[1].page.GetComponent<UIInkRealmPage>().属性滚动.verticalNormalizedPosition=0;
                if(i==32)panel.tabs[1].page.GetComponent<UIInkRealmPage>().属性滚动.verticalNormalizedPosition=1;
                yield return new WaitForSecondsRealtime(.1f);if(i>=2 && panel.CurrentTab!=CharacterTab.境界)panel.ShowTab(CharacterTab.境界);yield return new WaitForEndOfFrame();
                frame=ScreenCapture.CaptureScreenshotAsTexture();
                if(encoder==null){suffix=frame.width+"x"+frame.height;final=Path.Combine(dir,"InkUI-realm-"+suffix+"-half-speed.mp4");encoder=new MediaEncoder(temp,new VideoTrackAttributes{frameRate=new MediaRational(5),width=(uint)frame.width,height=(uint)frame.height,includeAlpha=false,bitRateMode=VideoBitrateMode.Medium});}
                encoder.AddFrame(frame);
                if(i==20)File.WriteAllBytes(Path.Combine(dir,"UI_境界_after_"+suffix+".png"),frame.EncodeToPNG());
                if(i==7 || i==26)File.WriteAllBytes(Path.Combine(dir,"UI_境界_after_"+suffix+"-frame-"+i+".png"),frame.EncodeToPNG());
                Object.Destroy(frame);frame=null;
            }
            encoder.Dispose();encoder=null;File.Copy(temp,final,true);File.Delete(temp);status="DONE "+final;
        }finally{encoder?.Dispose();if(frame!=null)Object.Destroy(frame);panel.SetOpen(opened);if(opened)panel.ShowTab(oldTab);if(status=="recording")status="FAILED see Console";}
    }
}
