using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>九宫的视觉和命中使用同一透视投影；阵型、人数及选中仍交给原数据组件。</summary>
public class UIInkFormationPage : MonoBehaviour
{
    public UISpiritFormationBar 九宫 {get;private set;}
    public UIEntryList 真灵 {get;private set;}
    public UIInkWaterfall 瀑布流 {get;private set;}
    public UIEntryInfo 详情 {get;private set;}
    public Camera 透视相机=>camera3d;
    public Image 墨底 {get;private set;}
    public UIInkFormationOrbit 查看控制 {get;private set;}
    public int 模型数 {get;private set;}
    Transform stage;Camera camera3d;RenderTexture target;RawImage preview;Mesh grid;Material ink;
    Transform[] models=new Transform[9];NpcDefinition[] previous=new NpcDefinition[9];float[] birth=new float[9];
    float[] modelScale=new float[9];
    readonly List<Mesh> owned=new List<Mesh>();RectTransform board;float age,lastRender;bool ready;
    readonly Dictionary<Renderer,bool> hiddenWorld=new Dictionary<Renderer,bool>();
    readonly List<Material> ownedVisuals=new List<Material>();MaterialPropertyBlock brush;
    static GameObject Player(){var actor=Object.FindObjectOfType<PlayerController>();return actor!=null?actor.gameObject:null;}
    public static void 应用(Transform root){
        if(!UIInkNavigation.启用)return;var panel=root.GetComponent<CharacterPanelUI>();if(panel==null || panel.tabs.Count<6)return;
        var page=panel.tabs[5].page;if(page==null)return;var view=page.GetComponent<UIInkFormationPage>();if(view==null)view=page.AddComponent<UIInkFormationPage>();view.Build();
    }
    void Build(){
        if(ready)return;brush=new MaterialPropertyBlock();九宫=GetComponentInChildren<UISpiritFormationBar>(true);真灵=transform.Find("SpiritList").GetComponent<UIEntryList>();详情=真灵.infoTarget;九宫.infoTarget=详情;
        foreach(var motion in GetComponentsInChildren<UIInkMotion>(true))motion.enabled=false;
        board=transform.Find("FormationGrid") as RectTransform;
        UIBuildUtils.Place(board,new Vector2(.015f,.34f),new Vector2(.71f,.99f),Vector2.zero,Vector2.zero);
        foreach(var obj in new Transform[]{board,真灵.transform,详情.transform}){var image=obj.GetComponent<Image>();if(image!=null)image.enabled=false;var title=obj.Find("Title");if(title!=null)title.gameObject.SetActive(false);}
        var area=board.Find("GridArea") as RectTransform;UIBuildUtils.Stretch(area);UIBuildUtils.Stretch(九宫.transform as RectTransform);
        foreach(var slot in 九宫.slots){
            slot.button.transition=Selectable.Transition.None;slot.background.sprite=null;slot.background.color=Color.clear;
            if(slot.swatch!=null)slot.swatch.enabled=false;if(slot.lockText!=null)slot.lockText.enabled=false;
            slot.label.raycastTarget=false;slot.label.fontSize=17;slot.label.color=new Color(.96f,.96f,.87f);slot.label.gameObject.AddComponent<Shadow>().effectColor=Color.black;
            UIBuildUtils.Place(slot.label.rectTransform,new Vector2(0,0),new Vector2(1,.22f),Vector2.zero,Vector2.zero);
            if(slot.clearButton!=null){slot.clearButton.image.enabled=false;var text=slot.clearButton.GetComponentInChildren<Text>();if(text!=null){text.color=new Color(.96f,.96f,.87f);text.fontSize=18;}UIBuildUtils.Place(slot.clearButton.transform as RectTransform,new Vector2(.8f,.2f),new Vector2(1,.4f),Vector2.zero,Vector2.zero);}
            slot.gameObject.AddComponent<UIInkFormationSlot>().Initialize(this,slot);
        }
        stage=new GameObject("FormationPreview_Runtime").transform;stage.position=new Vector3(13000,13000,13000);
        var cam=new GameObject("FormationPerspectiveCamera",typeof(Camera));cam.transform.SetParent(stage,false);cam.transform.localPosition=new Vector3(0,12,-14);cam.transform.LookAt(stage.position+new Vector3(0,.7f,0));
        camera3d=cam.GetComponent<Camera>();camera3d.enabled=false;camera3d.orthographic=false;camera3d.fieldOfView=32;camera3d.cullingMask=1<<31;camera3d.clearFlags=CameraClearFlags.SolidColor;camera3d.backgroundColor=Color.clear;camera3d.nearClipPlane=.1f;camera3d.farClipPlane=50;
        var lightObject=new GameObject("PreviewLight",typeof(Light));lightObject.transform.SetParent(stage,false);lightObject.transform.localRotation=Quaternion.Euler(45,-35,0);var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<31;
        var surface=UIBuildUtils.CreateRect("PerspectiveModels",board);surface.SetAsFirstSibling();UIBuildUtils.Stretch(surface);preview=surface.gameObject.AddComponent<RawImage>();preview.raycastTarget=false;
        墨底=UIBuildUtils.CreateImage("FormationInkBackdrop",board,Color.white);墨底.sprite=InkUITheme.Load("Bag/bag-info-ink");墨底.raycastTarget=true;墨底.transform.SetAsFirstSibling();UIBuildUtils.Stretch(墨底.rectTransform);
        查看控制=board.gameObject.AddComponent<UIInkFormationOrbit>();查看控制.Initialize(camera3d,stage);
        grid=new Mesh{name="FormationInkLines"};ink=new Material(Shader.Find("UI/InkStarVolume"));var floor=new GameObject("NineCellLines",typeof(MeshFilter),typeof(MeshRenderer));floor.layer=31;floor.transform.SetParent(stage,false);floor.GetComponent<MeshFilter>().sharedMesh=grid;floor.GetComponent<MeshRenderer>().sharedMaterial=ink;
        var player=Player();if(player!=null)models[4]=CopyVisual(player,4);
        UIBuildUtils.Place(真灵.transform as RectTransform,new Vector2(.735f,.035f),new Vector2(.995f,.96f),Vector2.zero,Vector2.zero);
        真灵.inkCards=false;瀑布流=真灵.gameObject.AddComponent<UIInkWaterfall>();瀑布流.Initialize(真灵);瀑布流.Skip();真灵.RebuildFromSource();
        UIBuildUtils.Place(详情.transform as RectTransform,new Vector2(.015f,.015f),new Vector2(.71f,.32f),Vector2.zero,Vector2.zero);
        var backing=UIBuildUtils.CreateImage("SpiritInfoInk",详情.transform,Color.white);backing.sprite=InkUITheme.Load("Bag/bag-info-ink");backing.raycastTarget=false;backing.transform.SetAsFirstSibling();UIBuildUtils.Stretch(backing.rectTransform);
        Place(详情.nameText,.055f,.69f,.72f,.91f,24);Place(详情.tierText,.055f,.53f,.25f,.68f,16);Place(详情.kindText,.26f,.53f,.55f,.68f,16);Place(详情.descriptionText,.055f,.16f,.72f,.52f,18);
        if(详情.iconImage!=null)详情.iconImage.gameObject.SetActive(false);
        UIBuildUtils.Place(详情.actionButton.transform as RectTransform,new Vector2(.74f,.29f),new Vector2(.96f,.64f),Vector2.zero,Vector2.zero);UIInkActionButton.Apply(详情.actionButton);
        详情.ActionClicked+=Action;ready=true;age=0;
    }
    static void Place(Text text,float x,float y,float maxX,float maxY,int size){if(text==null)return;UIBuildUtils.Place(text.rectTransform,new Vector2(x,y),new Vector2(maxX,maxY),Vector2.zero,Vector2.zero);text.fontSize=size;text.color=new Color(.96f,.96f,.87f);text.alignment=TextAnchor.UpperLeft;}
    void Action(IPanelEntry entry){if(entry is NpcDefinition npc)九宫.data.ToggleSpirit(npc);}
    public Vector3 Cell(int index)=>new Vector3((index%3-1)*3,0,(1-index/3)*3);
    public Vector2 Project(Vector3 point){var p=camera3d.WorldToViewportPoint(stage.TransformPoint(point));return new Vector2(p.x,p.y);}
    Transform CopyVisual(GameObject source,int index){
        if(!source.scene.IsValid()){
            // 在未激活的临时层级烘焙，保证蒙皮骨骼属于场景；NPC 的 Awake/AI 不会运行。
            var holder=new GameObject("InactiveFormationBake");holder.SetActive(false);holder.transform.position=stage.position;
            var instance=Instantiate(source,holder.transform,false);
            foreach(var script in instance.GetComponentsInChildren<MonoBehaviour>(true))if(!(script is NpcInstance))DestroyImmediate(script);
            foreach(var script in instance.GetComponentsInChildren<NpcInstance>(true))DestroyImmediate(script);
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true))DestroyImmediate(collider);
            foreach(var audio in instance.GetComponentsInChildren<AudioSource>(true))audio.enabled=false;
            holder.SetActive(true);foreach(var animator in instance.GetComponentsInChildren<Animator>(true)){animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);}
            var result=CopyVisual(instance,index);DestroyImmediate(holder);return result;
        }
        if(index==4){var appearance=source.GetComponent<玩家外观>();var visual=appearance!=null?appearance.玩家视觉:source.transform.Find("Player_Visual");if(visual!=null)source=visual.gameObject;}
        // 只复制渲染网格，不实例化 NPC/Player 行为、碰撞体或 AI。
        var node=new GameObject("FormationModel_"+index).transform;node.SetParent(stage,false);var bounds=new Bounds();bool any=false;
        foreach(var r in source.GetComponentsInChildren<Renderer>(true)){
            if(!r.enabled)continue;bool active=true;for(var t=r.transform;t!=source.transform && t!=null;t=t.parent)if(!t.gameObject.activeSelf){active=false;break;}if(!active)continue;
            Mesh mesh=null;var skinned=r as SkinnedMeshRenderer;
            if(skinned!=null && skinned.sharedMesh!=null){mesh=new Mesh();skinned.BakeMesh(mesh);mesh.RecalculateBounds();owned.Add(mesh);}else if(r is MeshRenderer){var filter=r.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;}
            if(mesh==null)continue;var part=new GameObject(r.name,typeof(MeshFilter),typeof(MeshRenderer));part.layer=31;part.transform.SetParent(node,false);
            var matrix=source.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;part.transform.localPosition=matrix.GetColumn(3);part.transform.localRotation=matrix.rotation;part.transform.localScale=matrix.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.GetComponent<MeshRenderer>();var materials=new Material[r.sharedMaterials.Length];
            for(int m=0;m<materials.Length;m++){var original=r.sharedMaterials[m];var material=new Material(Shader.Find("Cultivation/UI/FormationModelBrush"));if(original!=null){if(original.HasProperty("_MainTex")){material.mainTexture=original.mainTexture;material.mainTextureScale=original.mainTextureScale;material.mainTextureOffset=original.mainTextureOffset;}if(original.HasProperty("_Color"))material.color=original.color;}ownedVisuals.Add(material);materials[m]=material;}renderer.sharedMaterials=materials;
            var local=mesh.bounds;for(int corner=0;corner<8;corner++){
                var point=local.center+Vector3.Scale(local.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));point=matrix.MultiplyPoint3x4(point);
                if(!any){bounds=new Bounds(point,Vector3.zero);any=true;}else bounds.Encapsulate(point);
            }
        }
        if(!any){Destroy(node.gameObject);return null;}
        float scale=1.7f/Mathf.Max(.1f,bounds.size.y);var center=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        foreach(Transform part in node)part.localPosition-=center;node.localScale=Vector3.one*scale;modelScale[index]=scale;node.localPosition=Cell(index);if(index==4)node.localRotation=Quaternion.Euler(0,90,0);birth[index]=Time.unscaledTime;return node;
    }
    void LateUpdate(){
        if(!ready)return;age+=Time.unscaledDeltaTime;var rect=board.rect;if(rect.width<1 || rect.height<1)return;
        camera3d.aspect=rect.width/rect.height;int width=Mathf.Clamp(Mathf.RoundToInt(rect.width*GetComponentInParent<Canvas>().scaleFactor),256,1280),height=Mathf.Clamp(Mathf.RoundToInt(rect.height*GetComponentInParent<Canvas>().scaleFactor),256,1080);
        if(target==null || target.width!=width || target.height!=height){if(target!=null){target.Release();Destroy(target);}target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="FormationPerspective",antiAliasing=2};target.Create();camera3d.targetTexture=target;preview.texture=target;}
        if(models[4]==null){var player=Player();if(player!=null)models[4]=CopyVisual(player,4);}
        var actor=Player();if(actor!=null)HideWorld(actor);foreach(var spirit in Object.FindObjectsOfType<FormationSpirit>())HideWorld(spirit.gameObject);
        模型数=0;for(int i=0;i<9;i++){
            var npc=九宫.data.战阵站位[i];if(i!=4 && npc!=previous[i]){
                if(models[i]!=null){foreach(var filter in models[i].GetComponentsInChildren<MeshFilter>())if(owned.Remove(filter.sharedMesh))Destroy(filter.sharedMesh);foreach(var renderer in models[i].GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)if(ownedVisuals.Remove(material))Destroy(material);Destroy(models[i].gameObject);}models[i]=null;previous[i]=npc;
                if(npc!=null){var prefab=Resources.Load<GameObject>(npc.模型资源路径);if(prefab!=null)models[i]=CopyVisual(prefab,i);}
                UIInkPulse.Emit(九宫.slots[i].transform as RectTransform,Vector2.zero,.24f);
            }
            if(models[i]!=null){
                模型数++;float t=UIInkMotion.减少动效?1:Mathf.Clamp01((Time.unscaledTime-birth[i])/.2f);
                models[i].localScale=Vector3.one*modelScale[i];
                if(brush==null)brush=new MaterialPropertyBlock();
                brush.SetFloat("_Reveal",t);
                foreach(var renderer in models[i].GetComponentsInChildren<Renderer>())if(renderer!=null)renderer.SetPropertyBlock(brush);
            }
            var slot=九宫.slots[i];var center=Cell(i);var min=Vector2.one;var max=Vector2.zero;foreach(var x in new[]{-1.5f,1.5f})foreach(var z in new[]{-1.5f,1.5f}){var p=Project(center+new Vector3(x,0,z));min=Vector2.Min(min,p);max=Vector2.Max(max,p);}var rt=slot.transform as RectTransform;rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;slot.background.color=Color.clear;
            if(slot.swatch!=null)slot.swatch.enabled=false;if(slot.lockText!=null)slot.lockText.enabled=false;
            if(slot.label!=null)slot.label.color=new Color(.96f,.96f,.87f);
        }
        DrawLines();if(Time.unscaledTime-lastRender>=1f/30){camera3d.Render();lastRender=Time.unscaledTime;}
        if(详情.Current is NpcDefinition selected){详情.actionButton.gameObject.SetActive(true);详情.actionButton.interactable=九宫.data.IsSpiritOnField(selected) || !九宫.data.战阵已满;详情.actionLabel.text=九宫.data.IsSpiritOnField(selected)?"下阵":"上阵";}
    }
    void DrawLines(){
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        for(int n=0;n<4;n++){float c=-4.5f+n*3;float t=UIInkMotion.减少动效?1:Mathf.Clamp01((age-Mathf.Abs(c)*.025f)/.3f);Line(new Vector3(c,0,-4.5f*t),new Vector3(c,0,4.5f*t),new Color(.72f,.82f,.75f,.8f),vertices,colors,triangles);Line(new Vector3(-4.5f*t,0,c),new Vector3(4.5f*t,0,c),new Color(.72f,.82f,.75f,.8f),vertices,colors,triangles);}
        if(九宫.data.战阵已满)for(int i=0;i<9;i++)if(i!=4 && 九宫.data.战阵站位[i]!=null){var c=Cell(i);var red=new Color(.86f,.30f,.20f,.9f);var corners=new[]{new Vector3(-1.4f,.01f,-1.4f),new Vector3(1.4f,.01f,-1.4f),new Vector3(1.4f,.01f,1.4f),new Vector3(-1.4f,.01f,1.4f)};for(int j=0;j<4;j++)Line(c+corners[j],c+corners[(j+1)%4],red,vertices,colors,triangles);}
        grid.Clear();grid.SetVertices(vertices);grid.SetColors(colors);grid.SetTriangles(triangles,0);grid.RecalculateNormals();grid.RecalculateBounds();
    }
    static void Line(Vector3 a,Vector3 b,Color tint,List<Vector3> v,List<Color> c,List<int> indices){var side=Vector3.Cross(b-a,Vector3.up).normalized*.018f;int i=v.Count;v.Add(a-side);v.Add(a+side);v.Add(b+side);v.Add(b-side);for(int j=0;j<4;j++)c.Add(tint);indices.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
    void OnEnable(){age=0;if(stage!=null)stage.gameObject.SetActive(true);if(ready){birth[4]=Time.unscaledTime;瀑布流.Open();查看控制.复位();}}
    void HideWorld(GameObject obj){foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true)){if(!hiddenWorld.ContainsKey(renderer))hiddenWorld.Add(renderer,renderer.enabled);renderer.enabled=false;}}
    void RestoreWorld(){foreach(var pair in hiddenWorld)if(pair.Key!=null)pair.Key.enabled=pair.Value;hiddenWorld.Clear();}
    void OnDisable(){RestoreWorld();if(stage!=null)stage.gameObject.SetActive(false);}
    void OnDestroy(){RestoreWorld();if(详情!=null)详情.ActionClicked-=Action;if(stage!=null)Destroy(stage.gameObject);if(target!=null){target.Release();Destroy(target);}if(grid!=null)Destroy(grid);if(ink!=null)Destroy(ink);foreach(var mesh in owned)if(mesh!=null)Destroy(mesh);foreach(var material in ownedVisuals)if(material!=null)Destroy(material);}
}

public class UIInkFormationSlot : MonoBehaviour,IDropHandler,ICanvasRaycastFilter
{
    UIInkFormationPage page;UISpiritSlot slot;
    public void Initialize(UIInkFormationPage view,UISpiritSlot original){page=view;slot=original;}
    public void OnDrop(PointerEventData e){
        if(!(UIDragContext.Entry is NpcDefinition npc))return;var data=slot.ResolveData();
        if(slot.是玩家格 || data.IsSpiritOnField(npc) || slot.Spirit!=null || data.战阵已满){data.ShowHint("请选择未占用的外围站位（最多五名真灵）");return;}
        var old=data.待上阵真灵;data.BeginPendingSpirit(npc);bool accepted=data.HandleSpiritSlotClicked(slot.slot);
        if(accepted){page.真灵.Select(npc);UIDragContext.End(true);}else{data.CancelPendingSpirit();if(old!=null)data.BeginPendingSpirit(old);}
    }
    public bool IsRaycastLocationValid(Vector2 screen,Camera eventCamera){
        if(page==null || page.透视相机==null)return true;var rect=page.transform.Find("FormationGrid") as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,eventCamera,out var p);p=new Vector2((p.x-rect.rect.xMin)/rect.rect.width,(p.y-rect.rect.yMin)/rect.rect.height);
        var center=page.Cell(slot.slot);var corners=new[]{new Vector3(-1.5f,0,-1.5f),new Vector3(1.5f,0,-1.5f),new Vector3(1.5f,0,1.5f),new Vector3(-1.5f,0,1.5f)};float sign=0;
        for(int i=0;i<4;i++){var a=page.Project(center+corners[i]);var b=page.Project(center+corners[(i+1)%4]);float cross=(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);if(Mathf.Abs(cross)<.00001f)continue;if(sign!=0 && sign*cross<0)return false;sign=cross;}return true;
    }
}
