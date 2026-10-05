using UnityEngine;
using UnityEngine.UI;

/// <summary>独立透明渲染舞台：三维环、图标平面和深度遮挡。固定UI命中、文字与数据不变。</summary>
public class UIInkSkillVolume : MonoBehaviour
{
    UIActiveSkillBar bar;UIInkSkillStar[] stars;Transform stage;Camera camera3d;RawImage image;RenderTexture target;Mesh ring;Material material;
    Transform[] nodes;Transform[,] rings;SpriteRenderer[] icons;Sprite[] previous;float lastRender;
    PlayerHud hud; UIInkHudSkill[] hudStars; Material[] hudMaterials;
    static int stageSequence;
    public bool 三维就绪=>target!=null && camera3d!=null && nodes!=null;
    public int 三维星位数=>nodes==null?0:nodes.Length;
    public void InitializeHud(PlayerHud source, UIInkHudSkill[] visual)
    {
        hud=source;hudStars=visual;
        Initialize(null,new UIInkSkillStar[visual.Length]);
        if(nodes==null)return;
        hudMaterials=new Material[visual.Length];
        var shader=Resources.Load<Shader>("UI/InkUI/Dynamic/InkCooldown");
        for(int i=0;i<visual.Length;i++)if(shader!=null){hudMaterials[i]=new Material(shader);hudMaterials[i].SetFloat("_ZWrite",1);hudMaterials[i].SetFloat("_ZTest",4);icons[i].sharedMaterial=hudMaterials[i];}
    }
    public void Initialize(UIActiveSkillBar original,UIInkSkillStar[] visual){
        var shader=Shader.Find("UI/InkStarVolume");if(shader==null)return;bar=original;stars=visual;material=new Material(shader);ring=CreateRing();
        stage=new GameObject("SkillVolumeStage_Runtime").transform;stage.position=new Vector3(12000+(++stageSequence)*4000,12000,12000);
        var cameraObject=new GameObject("SkillVolumeCamera");cameraObject.transform.SetParent(stage,false);cameraObject.transform.localPosition=new Vector3(0,0,-1000);
        camera3d=cameraObject.AddComponent<Camera>();camera3d.enabled=false;camera3d.orthographic=true;camera3d.clearFlags=CameraClearFlags.SolidColor;camera3d.backgroundColor=Color.clear;camera3d.nearClipPlane=.1f;camera3d.farClipPlane=2000;camera3d.cullingMask=1<<31;camera3d.allowHDR=false;camera3d.allowMSAA=true;
        var rect=UIBuildUtils.CreateRect("Volume3D",transform);rect.SetAsFirstSibling();UIBuildUtils.Stretch(rect);image=rect.gameObject.AddComponent<RawImage>();image.raycastTarget=false;
        nodes=new Transform[stars.Length];rings=new Transform[stars.Length,2];icons=new SpriteRenderer[stars.Length];previous=new Sprite[stars.Length];
        for(int i=0;i<stars.Length;i++){
            nodes[i]=new GameObject("VolumeStar"+i).transform;nodes[i].SetParent(stage,false);
            for(int n=0;n<2;n++){var go=new GameObject("OrbitalRing",typeof(MeshFilter),typeof(MeshRenderer));go.layer=31;go.transform.SetParent(nodes[i],false);go.GetComponent<MeshFilter>().sharedMesh=ring;go.GetComponent<MeshRenderer>().sharedMaterial=material;rings[i,n]=go.transform;}
            var glyph=new GameObject("SkillGlyph",typeof(SpriteRenderer));glyph.layer=31;glyph.transform.SetParent(nodes[i],false);icons[i]=glyph.GetComponent<SpriteRenderer>();
        }
    }
    static Mesh CreateRing(){
        const int slices=96,tube=6;var vertices=new Vector3[(slices+1)*(tube+1)];var normals=new Vector3[vertices.Length];var colors=new Color[vertices.Length];var triangles=new int[slices*tube*6];int t=0;
        for(int i=0;i<=slices;i++)for(int j=0;j<=tube;j++){float a=i*2*Mathf.PI/slices,b=j*2*Mathf.PI/tube;int k=i*(tube+1)+j;var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);normals[k]=radial*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b);vertices[k]=radial*52+normals[k]*.75f;colors[k]=new Color(.42f,.80f,.72f,.80f);if(i==slices || j==tube)continue;int next=k+tube+1;triangles[t++]=k;triangles[t++]=next;triangles[t++]=k+1;triangles[t++]=k+1;triangles[t++]=next;triangles[t++]=next+1;}
        var mesh=new Mesh{name="InkOrbitalTorus"};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
    }
    void LateUpdate(){
        if(camera3d==null)return;var rect=((RectTransform)transform).rect;if(rect.width<1 || rect.height<1)return;
        float scale=GetComponentInParent<Canvas>().scaleFactor;int width=Mathf.Clamp(Mathf.RoundToInt(rect.width*scale),256,1024),height=Mathf.Clamp(Mathf.RoundToInt(rect.height*scale),256,1536);
        if(target==null || target.width!=width || target.height!=height){if(target!=null){target.Release();Destroy(target);}target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="InkSkills3D",antiAliasing=2};target.Create();camera3d.targetTexture=target;image.texture=target;}
        camera3d.orthographicSize=rect.height*.5f;camera3d.aspect=rect.width/rect.height;
        float time=UIInkMotion.减少动效?0:Time.unscaledTime;
        for(int i=0;i<nodes.Length;i++){
            Vector3 p=transform.InverseTransformPoint(hud!=null?hudStars[i].视觉位置:stars[i].视觉位置);if(hud!=null)p-=(Vector3)rect.center;p.z=0;nodes[i].localPosition=p;
            nodes[i].localScale=Vector3.one*(hud!=null?.62f:1);
            var shift=hud!=null?hudStars[i].视差位移:stars[i].视差位移;nodes[i].localRotation=Quaternion.Euler(-shift.y*1.6f,shift.x*1.6f,0);
            for(int n=0;n<2;n++){rings[i,n].localRotation=Quaternion.Euler(58+n*48,25+n*55,time*(n==0?15:-12)+i*37);rings[i,n].localScale=Vector3.one*(n==0?1:1.12f);}
            var slot=hud==null?bar.slots[i]:null;
            var content=hud!=null?(hud.面板数据!=null&&i<hud.面板数据.主动技能.Count?hud.面板数据.主动技能[i] as IPanelEntry:null):slot.Content as IPanelEntry;
            var sprite=UIInkAbilityArt.Icon(content);
            if(sprite!=previous[i]){icons[i].sprite=sprite;previous[i]=sprite;if(sprite!=null){float size=Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y);icons[i].transform.localScale=Vector3.one*(76/Mathf.Max(.001f,size));}}
            icons[i].color=new Color(1,1,1,hud==null&&UIDragContext.OriginData==bar.data && UIDragContext.OriginSlot==i?.25f:1);
            icons[i].transform.localRotation=Quaternion.Euler(Mathf.Sin(time*.7f+i)*9,Mathf.Sin(time*.5f+i)*16,0);
            if(hud!=null){if(hudMaterials[i]!=null)hudMaterials[i].SetFloat("_Cooldown",hudStars[i].墨晕比例);hudStars[i].使用三维=true;if(hud.技能格们[i].图标!=null)hud.技能格们[i].图标.enabled=false;}
            else {if(slot.icon!=null)slot.icon.enabled=false;var orbit=slot.transform.Find("FloatingSurface/Orbit")?.GetComponent<Image>();if(orbit!=null)orbit.enabled=false;}
        }
        if(Time.unscaledTime-lastRender>=1f/30){camera3d.Render();lastRender=Time.unscaledTime;}
    }
    void OnDisable(){if(stage!=null)stage.gameObject.SetActive(false);}
    void OnEnable(){if(stage!=null)stage.gameObject.SetActive(true);}
    void OnDestroy(){if(stage!=null)Destroy(stage.gameObject);if(target!=null){target.Release();Destroy(target);}if(material!=null)Destroy(material);if(ring!=null)Destroy(ring);if(hudMaterials!=null)foreach(var m in hudMaterials)if(m!=null)Destroy(m);}
}
