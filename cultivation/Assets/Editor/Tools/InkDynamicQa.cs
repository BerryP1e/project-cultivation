using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Object;

/// <summary>父导航实际 Play 验收、半速录像；不写存档或场景。</summary>
public static class InkDynamicQa
{
    static string status="idle";
    public static string Run(string command) {
        if(command=="status") return status;
        if(command=="shaders") {
            var result=new StringBuilder();
            foreach(var name in new[]{"Cultivation/UI/InkFluid","Hidden/Cultivation/InkDensity"}) {
                var shader=Shader.Find(name); result.AppendLine(name+" supported="+(shader!=null && shader.isSupported));
                if(shader!=null) foreach(var message in ShaderUtil.GetShaderMessages(shader)) result.AppendLine(message.severity+" "+message.message+" line="+message.line);
            }
            return result.ToString();
        }
        if(!Application.isPlaying) return "FAIL requires Play";
        var panel=FindObjectOfType<CharacterPanelUI>(true);
        if(command=="edgeprobe") {
            var ink=panel.GetComponentsInChildren<UIInkFluid>(true)[0].transform.Find("InkNavBlot").GetComponent<Image>();
            var material=new Material(ink.material);
            var target=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);
            try {
                material.SetVector("_Ripple",new Vector4(.5f,.5f,1,0));
                material.SetVector("_Pointer",new Vector4(-10,-10,0,0));
                material.SetFloat("_Reveal",1); material.SetColor("_Color",new Color(.38f,.42f,.39f,1));
                float[] area=new float[2];
                for(int pass=0;pass<2;pass++) {
                    material.SetFloat("_Bleed",pass);
                    RenderTexture.active=target; GL.Clear(true,true,Color.clear);
                    Graphics.Blit(ink.sprite.texture,target,material);
                    RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                    foreach(var color in pixels.GetPixels()) if(color.a>.04f) area[pass]++;
                    File.WriteAllBytes(Path.Combine(Application.dataPath,"../screenshots/InkUI-edge-probe-"+pass+".png"),pixels.EncodeToPNG());
                }
                string result=(area[0]>0 && area[1]>area[0]*1.05f ? "PASS" : "FAIL")+" ink silhouette area (ring disabled) "+area[0]+" -> "+area[1];
                File.WriteAllText(Path.Combine(Application.dataPath,"../screenshots/InkUI-edge-probe.txt"),result);
                return result;
            } finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); Destroy(pixels); Destroy(material); }
        }
        if(command=="parent") {
            panel.SetOpen(true);
            var content=panel.panelRoot.transform.Find("Window/Content");
            if(content==null) return "FAIL missing content";
            content.gameObject.SetActive(false);
            var hint=panel.panelRoot.transform.Find("Window/HintBar"); if(hint!=null) hint.gameObject.SetActive(false);
            return "OK isolated parent navigation; restart Play to restore content";
        }
        if(command=="paperon" || command=="paperoff") {
            var layer=panel.GetComponentInChildren<UIInkPaperLayer>(true);
            if(layer==null) return "FAIL missing paper layer";
            layer.gameObject.SetActive(command=="paperon"); return "OK "+command;
        }
        if(status=="recording") return "FAIL recording active";
        status="recording"; panel.StartCoroutine(Record(panel)); return "OK dynamic recording started";
    }
    static IEnumerator Record(CharacterPanelUI panel) {
        string directory=Path.Combine(Application.dataPath,"../screenshots"); Directory.CreateDirectory(directory);
        string suffix=Screen.width+"x"+Screen.height;
        string temp=Path.Combine(Path.GetTempPath(),"inknav-"+Guid.NewGuid().ToString("N")+".mp4");
        string final=Path.Combine(directory,"InkUI-navigation-dynamic-"+suffix+"-half-speed.mp4");
        var log=new StringBuilder(); int failures=0;
        Action<string,bool> check=(label,ok)=>{log.AppendLine((ok ? "PASS " : "FAIL ")+label); if(!ok) failures++;};
        bool reduced=UIInkMotion.减少动效; var oldTab=panel.CurrentTab; bool opened=panel.IsOpen;
        MediaEncoder encoder=null; Texture2D frame=null; UIInkFluid[] points=null;
        float originalScale=Time.timeScale;
        try {
            UIInkMotion.减少动效=false; panel.SetOpen(false); yield return new WaitForSecondsRealtime(.4f); originalScale=Time.timeScale;
            encoder=new MediaEncoder(temp,new VideoTrackAttributes { frameRate=new MediaRational(5),width=(uint)Screen.width,height=(uint)Screen.height,includeAlpha=false,bitRateMode=VideoBitrateMode.Medium });
            Vector2 home=Vector2.zero; Vector2 center=Vector2.zero; Vector2 pulled=Vector2.zero; float clock=0;
            float[] idleDensity=null; int idleSteps=0;
            for(int i=0;i<130;i++) {
                if(i==5) {
                    panel.SetOpen(true); panel.ShowTabByIndex(0);
                    points=panel.GetComponentsInChildren<UIInkFluid>(true);
                    check("eight independent ink points",points.Length==8);
                    var fluidShader=Shader.Find("Cultivation/UI/InkFluid");
                    check("ink display shader supported and compiles",fluidShader!=null && fluidShader.isSupported && !ShaderUtil.ShaderHasError(fluidShader));
                    var parentImage=points[0].transform.parent.parent.GetComponent<Image>();
                    check("parent has no backing plate",parentImage!=null && !parentImage.enabled);
                    check("no full-page paper overlay",panel.GetComponentInChildren<UIInkPaperLayer>()==null);
                    check("pause uses unscaled time",Time.timeScale==0);
                    foreach(var point in points) point.调试鼠标=new Vector2(-10000,-10000);
                }
                if(i==10) { idleDensity=ReadDensity(points[0].墨密度纹理 as RenderTexture); idleSteps=points[0].模拟步数; }
                if(i==20) {
                    float difference=DensityDifference(idleDensity,ReadDensity(points[0].墨密度纹理 as RenderTexture));
                    check("GPU density evolves without pointer; mean delta="+difference.ToString("F6"),difference>.0001f && points[0].模拟步数>idleSteps);
                    home=(points[0].transform as RectTransform).anchoredPosition;
                    clock=points[0].形变时钟;
                    center=RectTransformUtility.WorldToScreenPoint(null,points[0].transform.Find("InkNavBlot").position);
                }
                if(i>=21 && i<40) {
                    points[0].调试鼠标=center+new Vector2((i-21)*3-18,12);
                }
                if(i==39) {
                    pulled=points[0].鼠标牵引;
                    check("nearby point attracts locally",pulled.x>0 && pulled.magnitude<=8);
                    check("ambient shader advances while paused",points[0].形变时钟>clock);
                    check("stable button geometry during attraction",(points[0].transform as RectTransform).anchoredPosition==home);
                    check("motion remains subtle",points[0].绘制偏移.magnitude<=11);
                    check("name text never scales",points[0].GetComponentInChildren<Text>().transform.localScale==Vector3.one);
                    check("no independent rotating water trace",points[0].transform.Find("InkNavWaterTrace")==null);
                    var ink=points[0].transform.Find("InkNavBlot").GetComponent<Image>();
                    check("pointer changes ink shader itself",ink.material.GetVector("_Pointer").z>.5f);
                    check("transparent drawing margin reserved",ink.rectTransform.rect.width>=140);
                }
                if(i==40) {
                    points[0].调试鼠标=new Vector2(-10000,-10000);
                    points[0].OnPointerClick(new PointerEventData(EventSystem.current){position=center});
                    var image=points[0].transform.Find("InkNavBlot").GetComponent<Image>();
                    check("click radial ripple starts at pointer",image.material.GetVector("_Ripple").z==0);
                }
                if(i==47) check("attraction returns after pointer leaves",points[0].鼠标牵引.magnitude<.01f);
                if(i==41) check("click spreads silhouette edge",points[0].transform.Find("InkNavBlot").GetComponent<Image>().material.GetFloat("_Bleed")>.3f);
                if(i>=50 && i<98 && (i-50)%6==0) {
                    int index=(i-50)/6;
                    panel.tabs[index].button.onClick.Invoke();
                    int active=0; foreach(var binding in panel.tabs) if(binding.page.activeSelf) active++;
                    check("tab "+index+" original button switches exactly one page",panel.CurrentTab==(CharacterTab)index && active==1);
                    check("tab "+index+" background stays clear",panel.tabs[index].background.color.a==0);
                }
                if(i==100) {
                    var button=panel.tabs[0].button; button.interactable=false;
                    var image=points[0].transform.Find("InkNavBlot").GetComponent<Image>();
                    float progress=image.material.GetVector("_Ripple").z;
                    points[0].OnPointerClick(new PointerEventData(EventSystem.current){position=center});
                    check("disabled button rejects feedback",image.material.GetVector("_Ripple").z==progress);
                    button.interactable=true; UIInkMotion.减少动效=true;
                }
                if(i==106) {
                    bool still=true;
                    foreach(var point in points) still &= point.绘制偏移==Vector2.zero && point.形变时钟==0;
                    check("reduced motion stops all ambient movement",still);
                }
                if(i==110) { UIInkMotion.减少动效=false; panel.SetOpen(false); }
                yield return new WaitForSecondsRealtime(.1f);
                yield return new WaitForEndOfFrame();
                frame=ScreenCapture.CaptureScreenshotAsTexture();
                if(frame==null) throw new InvalidOperationException("No screenshot frame");
                if(i==10 || i==20 || i==39 || i==41 || i==43)
                    File.WriteAllBytes(Path.Combine(directory,"InkUI-fluid-"+suffix+"-frame-"+i+".png"),frame.EncodeToPNG());
                encoder.AddFrame(frame); Destroy(frame); frame=null;
            }
            encoder.Dispose(); encoder=null; File.Copy(temp,final,true); File.Delete(temp);
            check("closing restores timeScale",Time.timeScale==originalScale);
            log.AppendLine("Frames=130 CaptureFps<=10 EncodeFps=5 Failures="+failures+" Video="+final);
            status="DONE Failures="+failures+" Video="+final;
        } finally {
            encoder?.Dispose(); if(frame!=null) Destroy(frame);
            if(points!=null) foreach(var point in points) if(point!=null) point.调试鼠标=null;
            UIInkMotion.减少动效=reduced; panel.SetOpen(opened); if(opened) panel.ShowTab(oldTab);
            File.WriteAllText(Path.Combine(directory,"InkUI-navigation-dynamic-checks-"+suffix+".txt"),log.ToString());
            if(status=="recording") status="FAILED; see checks and Console";
        }
    }
    static float[] ReadDensity(RenderTexture texture) {
        if(texture==null) return null;
        var previous=RenderTexture.active;
        var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBAFloat,false,true);
        try {
            RenderTexture.active=texture; copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); copy.Apply();
            var colors=copy.GetPixels(); var density=new float[colors.Length];
            for(int i=0;i<density.Length;i++) density[i]=colors[i].r;
            return density;
        } finally { RenderTexture.active=previous; Destroy(copy); }
    }
    static float DensityDifference(float[] a,float[] b) {
        if(a==null || b==null || a.Length!=b.Length) return 0;
        float difference=0; for(int i=0;i<a.Length;i++) difference+=Mathf.Abs(a[i]-b[i]);
        return difference/a.Length;
    }
}
