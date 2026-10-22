using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>宣纸上的双层真实3D装备轨道；复用法则与主动星位的环体和原汇入星光。</summary>
public sealed class UIPaperEquipmentOrbit : MonoBehaviour
{
    sealed class Node
    {
        public bool Law;public int Index;public Transform Model;public Transform[] Rings;public SpriteRenderer Icon;public UIInkLawGlyph Art;
        public UIPaperEquipmentSlot Hit;public Text Label,Fallback;public CanvasGroup Group;public MaterialPropertyBlock Block=new MaterialPropertyBlock();public float Depth,Hover,Press;
    }
    readonly List<Node> nodes=new List<Node>();readonly List<LineRenderer> strands=new List<LineRenderer>();readonly List<Material> materials=new List<Material>();MaterialPropertyBlock strandBlock;
    UIInkBagPage owner;UIPanelData data;Font font;Transform stage,ring;SpriteRenderer portrait;Camera camera3d;RawImage image;
    RenderTexture nearTarget,farTarget;Material spriteMaterial,portraitMaterial,lawMaterial,skillMaterial,blur,composite;Mesh lawMesh,skillMesh;
    float lastRender,angle,speed,damping,lawFocus,skillFocus=1,lawFocusVelocity,skillFocusVelocity;bool held;int lawOrigin=-1;public UIInkLawPreview Starlight {get;private set;}
    public float LawFocus=>lawFocus;public float SkillFocus=>skillFocus;
    UIPaperWeaponPreview weaponPreview;
    ParticleSystem focusStars;ParticleSystem.Particle[] focusParticles=new ParticleSystem.Particle[72];
    public bool Ready=>nearTarget&&stage;public float Angle=>angle;public Camera PreviewCamera=>camera3d;public Transform Stage=>stage;
    public IReadOnlyList<UIPaperEquipmentSlot> LawSlots=>nodes.Where(n=>n.Law).OrderBy(n=>n.Index).Select(n=>n.Hit).ToArray();
    public IReadOnlyList<UIPaperEquipmentSlot> SkillSlots=>nodes.Where(n=>!n.Law).OrderBy(n=>n.Index).Select(n=>n.Hit).ToArray();
    public void Initialize(UIInkBagPage view,UIPanelData source,Font textFont)
    {
        owner=view;data=source;font=textFont;data.EnsureLists();strandBlock=new MaterialPropertyBlock();
        image=gameObject.AddComponent<RawImage>();image.raycastTarget=true;gameObject.AddComponent<UIPaperEquipmentOrbitDrag>().Owner=this;
        composite=Material("PaperOrbitComposite");image.material=composite;blur=Material("PaperOrbitBlur");blur.SetFloat("_Radius",1.65f);
        spriteMaterial=Material("PaperOrbitSprite");portraitMaterial=Material("PaperOrbitSprite");portraitMaterial.SetFloat("_Opacity",.46f);portraitMaterial.SetFloat("_Blur",10);portraitMaterial.SetFloat("_Portrait",1);
        lawMaterial=Material("PaperOrbitRing");lawMaterial.SetColor("_Tint",new Color(.63f,.50f,.23f));skillMaterial=Material("PaperOrbitRing");skillMaterial.SetColor("_Tint",new Color(.23f,.58f,.46f));
        stage=new GameObject("PaperEquipmentStage_Runtime").transform;stage.position=new Vector3(8000,8000,-8000);
        camera3d=new GameObject("EquipmentOrbitCamera",typeof(Camera)).GetComponent<Camera>();camera3d.transform.SetParent(stage,false);camera3d.transform.localPosition=new Vector3(0,3.6f,-7.8f);camera3d.transform.LookAt(stage.position+Vector3.up*1.4f);camera3d.enabled=false;camera3d.clearFlags=CameraClearFlags.SolidColor;camera3d.backgroundColor=Color.clear;camera3d.nearClipPlane=.1f;camera3d.farClipPlane=30;camera3d.fieldOfView=31;camera3d.allowHDR=false;
        ring=new GameObject("EquipmentTiltedOrbit").transform;ring.SetParent(stage,false);ring.localPosition=Vector3.up*1.4f;
        portrait=new GameObject("BlurredOutfitPortrait",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();portrait.gameObject.layer=31;portrait.transform.SetParent(stage,false);portrait.transform.localPosition=new Vector3(0,1.5f,0);portrait.transform.rotation=camera3d.transform.rotation;portrait.sharedMaterial=portraitMaterial;SyncPortrait();
        lawMesh=UIInkLawPage.CreateRing();skillMesh=UIInkSkillVolume.CreateRing();
        for(int i=0;i<data.法则装备.Count;i++)BuildNode(true,i);for(int i=0;i<data.主动技能.Count;i++)BuildNode(false,i);
        var strandMaterial=new Material(Shader.Find("Sprites/Default"));materials.Add(strandMaterial);
        for(int i=0;i<8;i++){var go=new GameObject(i<5?"WanderingStrand_"+i:"OuterSkillStarlight_"+i,typeof(LineRenderer));go.layer=31;go.transform.SetParent(ring,false);var line=go.GetComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=72;line.widthMultiplier=i==0?.018f:.01f;line.sharedMaterial=strandMaterial;var color=i<5?new Color(.42f,.46f,.29f,.57f):new Color(.25f,.48f,.43f,.48f);var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(color.a,.18f),new GradientAlphaKey(color.a,.78f),new GradientAlphaKey(0,1)});line.colorGradient=gradient;strands.Add(line);}
        Starlight=stage.gameObject.AddComponent<UIInkLawPreview>();Starlight.初始化(null,ring);
        var stars=new GameObject("FocusedOrbitStarlight",typeof(ParticleSystem));stars.layer=31;stars.transform.SetParent(ring,false);focusStars=stars.GetComponent<ParticleSystem>();var main=focusStars.main;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=focusParticles.Length;var emission=focusStars.emission;emission.enabled=false;var starMaterial=new Material(Shader.Find("Sprites/Default"));var starSprite=InkUITheme.Load("SkillsDynamic/fx-star-particle");if(starSprite)starMaterial.mainTexture=starSprite.texture;materials.Add(starMaterial);focusStars.GetComponent<ParticleSystemRenderer>().sharedMaterial=starMaterial;focusStars.Play();
        weaponPreview=stage.gameObject.AddComponent<UIPaperWeaponPreview>();weaponPreview.Initialize(this,owner,data,font);
        stage.gameObject.SetActive(gameObject.activeInHierarchy);lastRender=Time.unscaledTime;
    }
    Material Material(string name){var shader=Resources.Load<Shader>("UI/InkUI/Bag/"+name);var result=new Material(shader);materials.Add(result);return result;}
    void BuildNode(bool law,int index)
    {
        var n=new Node{Law=law,Index=index};n.Model=new GameObject((law?"LawSlot_":"ActiveSlot_")+index).transform;n.Model.SetParent(ring,false);n.Rings=new Transform[2];
        for(int k=0;k<2;k++){var go=new GameObject("OrbitalRing_"+k,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(n.Model,false);go.GetComponent<MeshFilter>().sharedMesh=law?lawMesh:skillMesh;go.GetComponent<MeshRenderer>().sharedMaterial=law?lawMaterial:skillMaterial;n.Rings[k]=go.transform;}
        n.Icon=new GameObject("EquipmentGlyph",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();n.Icon.transform.SetParent(n.Model,false);n.Icon.sharedMaterial=spriteMaterial;
        if(law){n.Art=n.Model.gameObject.AddComponent<UIInkLawGlyph>();n.Art.初始化(n.Icon);foreach(var renderer in n.Model.GetComponentsInChildren<SpriteRenderer>())renderer.sharedMaterial=spriteMaterial;}
        var button=UIBuildUtils.CreateButton((law?"LawSlot_":"ActiveSlot_")+index,transform,font,"",14);button.transition=Selectable.Transition.None;button.image.color=Color.clear;button.image.raycastTarget=true;var keep=n;button.onClick.AddListener(()=>Click(keep.Law,keep.Index));
        n.Hit=button.gameObject.AddComponent<UIPaperEquipmentSlot>();n.Hit.Owner=this;n.Hit.Law=law;n.Hit.Index=index;n.Group=button.gameObject.AddComponent<CanvasGroup>();
        n.Label=button.GetComponentInChildren<Text>();n.Label.color=UIPaperArt.Ink;UIPaperArt.Place(n.Label.rectTransform,-.25f,-.43f,1.25f,-.03f);n.Fallback=UIPaperArt.Label(button.transform,"MissingIconName","",13,TextAnchor.MiddleCenter,UIPaperArt.Ink);UIPaperArt.Place(n.Fallback.rectTransform,.02f,.1f,.98f,.9f);n.Fallback.resizeTextForBestFit=true;n.Fallback.resizeTextMinSize=9;n.Fallback.resizeTextMaxSize=13;nodes.Add(n);
    }
    public void SyncPortrait(){if(!portrait)return;var appearance=FindObjectOfType<玩家外观>();var id=appearance&&appearance.当前外观?appearance.当前外观.id:"app_player";portrait.sprite=Resources.Load<Sprite>("UI/InkUI/Appearance/portrait-"+id);portrait.enabled=portrait.sprite;if(portrait.sprite)portrait.transform.localScale=Vector3.one*(3.9f/Mathf.Max(.01f,portrait.sprite.bounds.size.y));}
    public IPanelEntry Entry(bool law,int index)=>law?LawFragmentSystem.取槽(data,index):data.主动技能[index] as IPanelEntry;
    public float Depth(bool law,int index)=>nodes.First(n=>n.Law==law&&n.Index==index).Depth;
    public void Hover(bool law,int index){var entry=Entry(law,index);if(entry!=null)owner.ShowTooltip(entry,false);}
    public void Leave()=>owner.LeaveCell();
    public void Click(bool law,int index){if(!law&&data.待装备神通){data.HandleSlotClicked(index);return;}var entry=Entry(law,index);if(entry!=null)owner.ShowTooltip(entry,true);}
    public void Hold(bool value)=>held=value;public void Rotate(float delta)=>angle=Mathf.Repeat(angle+delta,360);
    public void BeginDrag(IPanelEntry entry,int origin,bool law,PointerEventData e)
    {
        if(entry==null)return;if(entry is WeaponDefinition w&&!data.已拥有武器(w))return;if(entry is PassiveDivineAbility){ToastUI.提示("被动神通可在简介旁启用，不占主动槽位");return;}
        if(entry is LawFragment fragment&&!data.法则碎片.Contains(fragment))return;if(entry is ActiveDivineAbility active&&!data.已获得主动(active))return;
        owner.HideTooltip();lawOrigin=law?origin:-1;UIDragContext.BeginFromEquipment(entry,data,law?-1:origin,GetComponentInParent<Canvas>().rootCanvas.transform,font);UIDragContext.ApplyInkGhost();if(UIDragContext.Ghost){var group=UIDragContext.Ghost.GetComponent<CanvasGroup>();if(!group)group=UIDragContext.Ghost.AddComponent<CanvasGroup>();group.blocksRaycasts=group.interactable=false;foreach(var graphic in UIDragContext.Ghost.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;}UIDragContext.Move(e.position);held=true;
    }
    public void Drag(PointerEventData e){if(UIDragContext.Dragging)UIDragContext.Move(e.position);}
    public void EndDrag(bool accepted=false){UIDragContext.End(accepted);lawOrigin=-1;held=false;}
    public void Drop(bool law,int index)
    {
        var entry=UIDragContext.Entry;if(entry==null)return;
        if(law){if(!(entry is LawFragment fragment)||!data.法则碎片.Contains(fragment)||data.炉中法则.Contains(fragment.id))return;LawFragmentSystem.装备(data,fragment.id,index);}
        else {if(!(entry is ActiveDivineAbility active)||!data.已获得主动(active))return;int origin=UIDragContext.OriginData==data?UIDragContext.OriginSlot:-1;if(origin>=0){if(!data.交换主动槽(origin,index,active))return;}else data.EquipToSlot(index,active);ToastUI.提示("装备神通  "+active.DisplayName);}
        EndDrag(true);
    }
    public void ReturnToInventory(){if(UIDragContext.Entry is WeaponDefinition && UIDragContext.OriginSlot==-3){data.装备武器(null);EndDrag(true);return;}if(UIDragContext.Entry is LawFragment&&lawOrigin>=0)LawFragmentSystem.卸下(data,lawOrigin);else if(UIDragContext.Entry is ActiveDivineAbility&&UIDragContext.OriginData==data&&UIDragContext.OriginSlot>=0)data.ClearSlot(UIDragContext.OriginSlot);else return;EndDrag(true);}
    void LateUpdate()
    {
        if(!stage||Time.unscaledTime-lastRender<1f/30)return;float delta=Mathf.Min(.05f,Time.unscaledTime-lastRender);lastRender=Time.unscaledTime;var r=((RectTransform)transform).rect;if(r.width<1||r.height<1)return;
        bool reduced=UIInkMotion.减少动效;float time=reduced?0:Time.unscaledTime;float lawGoal=owner.Category==1?1:owner.Category==2?.35f:0,skillGoal=owner.Category==0?1:owner.Category==2?.35f:0;
        lawFocus=reduced?lawGoal:Mathf.SmoothDamp(lawFocus,lawGoal,ref lawFocusVelocity,.17f,Mathf.Infinity,delta);skillFocus=reduced?skillGoal:Mathf.SmoothDamp(skillFocus,skillGoal,ref skillFocusVelocity,.17f,Mathf.Infinity,delta);
        if(reduced)speed=damping=0;else speed=Mathf.SmoothDamp(speed,held||UIDragContext.Dragging?0:7,ref damping,held?.12f:.65f,Mathf.Infinity,delta);angle=Mathf.Repeat(angle+speed*delta,360);ring.localRotation=Quaternion.Euler(-18+Mathf.Sin(time*.35f)*2,0,-9+Mathf.Sin(time*.3f)*2);camera3d.aspect=r.width/r.height;
        float scale=GetComponentInParent<Canvas>().scaleFactor;int width=Mathf.Clamp(Mathf.RoundToInt(r.width*scale),256,1024),height=Mathf.Clamp(Mathf.RoundToInt(r.height*scale),256,1024);if(!nearTarget||nearTarget.width!=width||nearTarget.height!=height){ReleaseTargets();nearTarget=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="PaperEquipmentFront",antiAliasing=2};farTarget=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="PaperEquipmentRear",antiAliasing=2};nearTarget.Create();farTarget.Create();image.texture=nearTarget;}
        foreach(var n in nodes){float focus=n.Law?lawFocus:skillFocus;float phase=(angle+(n.Law?n.Index*60:n.Index*60+25))*Mathf.Deg2Rad;float rx=n.Law?Mathf.Lerp(1.35f,1.95f,focus):Mathf.Lerp(2.60f,2.38f,focus),rz=n.Law?Mathf.Lerp(.83f,1.16f,focus):Mathf.Lerp(1.55f,1.42f,focus);n.Model.localPosition=new Vector3(Mathf.Sin(phase)*rx,Mathf.Lerp(-.07f,.09f,focus)+Mathf.Sin(time*1.1f+n.Index)*.028f,Mathf.Cos(phase)*rz+(1-focus)*.20f);n.Model.rotation=camera3d.transform.rotation;float depth=Vector3.Dot(n.Model.position-portrait.transform.position,camera3d.transform.forward);n.Depth=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,rz*.85f,depth));int layer=depth>.12f?30:31;foreach(var child in n.Model.GetComponentsInChildren<Transform>())child.gameObject.layer=layer;
            var entry=Entry(n.Law,n.Index);bool selected=entry!=null&&owner.已选中(entry);n.Hover=Mathf.Lerp(n.Hover,n.Hit.Hovered?1:0,1-Mathf.Exp(-delta/ .07f));n.Press=Mathf.Lerp(n.Press,n.Hit.Pressed?1:0,1-Mathf.Exp(-delta/ .035f));float emphasis=Mathf.Lerp(.78f,1.30f,focus);n.Model.localScale=Vector3.one*emphasis*(1+n.Hover*.06f-n.Press*.09f);n.Block.Clear();n.Block.SetFloat("_Opacity",Mathf.Lerp(.24f,1,focus)*Mathf.Lerp(1,Mathf.Lerp(.35f,.76f,focus),n.Depth)*(1-n.Press*.20f));n.Block.SetFloat("_Focus",focus);n.Block.SetFloat("_Selected",selected?1:n.Hover*.45f);n.Block.SetFloat("_Blur",Mathf.Lerp(7,0,focus)+n.Depth*Mathf.Lerp(9,3,focus));
            for(int k=0;k<2;k++){n.Rings[k].localRotation=Quaternion.Euler(58+k*48,25+k*55,time*(k==0?15:-12)+n.Index*37);n.Rings[k].localScale=Vector3.one*(n.Law?(k==0?.60f:.67f):(k==0?.006f:.0067f));n.Rings[k].GetComponent<MeshRenderer>().SetPropertyBlock(n.Block);}
            var sprite=UIInkAbilityArt.Icon(entry);n.Icon.sprite=sprite;n.Icon.enabled=sprite;n.Fallback.text=entry!=null&&!sprite?entry.DisplayName:"";if(sprite)n.Icon.transform.localScale=Vector3.one*((n.Law?.50f:.58f)/Mathf.Max(.001f,Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y)));n.Icon.color=entry is LawFragment law?law.颜色:Color.white;if(n.Art)n.Art.设置(entry as LawFragment,1);
            // SpriteRenderer 的原贴图也保存在属性块中；深度参数不能覆盖掉它。
            foreach(var renderer in n.Model.GetComponentsInChildren<SpriteRenderer>()){if(renderer.sprite)n.Block.SetTexture("_MainTex",renderer.sprite.texture);renderer.SetPropertyBlock(n.Block);}
            var point=camera3d.WorldToViewportPoint(n.Model.position);var hit=(RectTransform)n.Hit.transform;hit.anchorMin=hit.anchorMax=new Vector2(point.x,point.y);hit.sizeDelta=Vector2.one*Mathf.Lerp(n.Law?53:58,n.Law?43:48,n.Depth)*emphasis;hit.anchoredPosition=Vector2.zero;n.Group.alpha=Mathf.Lerp(.27f,.96f,focus)*Mathf.Lerp(1,.58f,n.Depth);n.Label.text=(n.Law?"法 ":"技 ")+(n.Index+1);
        }
        nodes.Sort((a,b)=>b.Depth.CompareTo(a.Depth));foreach(var n in nodes)n.Hit.transform.SetAsLastSibling();
        for(int s=0;s<strands.Count;s++){bool outer=s>=5;float focus=outer?skillFocus:lawFocus;var line=strands[s];line.widthMultiplier=Mathf.Lerp(.006f,.018f,focus)*(s==0?1.25f:1);strandBlock.Clear();strandBlock.SetColor("_Color",new Color(1,1,1,Mathf.Lerp(.15f,1.1f,focus)));line.SetPropertyBlock(strandBlock);for(int j=0;j<72;j++){float a=angle*Mathf.Deg2Rad+s*1.35f+j/71f*(s==0?5.6f:3.5f)+time*(s%2==0?.10f:-.16f);float drift=Mathf.Sin(a*3+time*.7f+s)*.025f;float rx=outer?Mathf.Lerp(2.60f,2.38f,focus):Mathf.Lerp(1.35f,1.95f,focus),rz=outer?Mathf.Lerp(1.55f,1.42f,focus):Mathf.Lerp(.83f,1.16f,focus);line.SetPosition(j,new Vector3(Mathf.Sin(a)*(rx+s*.006f+drift),Mathf.Lerp(-.07f,.09f,focus)+Mathf.Sin(a*2+time*.5f+s)*(.022f+s*.009f),Mathf.Cos(a)*(rz+drift)+(1-focus)*.20f));}}
        UpdateFocusStars(time,reduced);
        Starlight.推进(delta,angle,reduced);
        camera3d.targetTexture=farTarget;camera3d.cullingMask=1<<30;camera3d.clearFlags=CameraClearFlags.SolidColor;camera3d.Render();Graphics.Blit(farTarget,nearTarget,blur);camera3d.targetTexture=nearTarget;camera3d.cullingMask=1<<31;camera3d.clearFlags=CameraClearFlags.Depth;camera3d.Render();
    }
    void UpdateFocusStars(float time,bool reduced)
    {
        if(!focusStars)return;if(reduced){focusStars.SetParticles(focusParticles,0);return;}
        for(int group=0;group<2;group++)for(int j=0;j<36;j++){
            bool law=group==0;float focus=law?lawFocus:skillFocus;float rx=law?Mathf.Lerp(1.35f,1.95f,focus):Mathf.Lerp(2.60f,2.38f,focus),rz=law?Mathf.Lerp(.83f,1.16f,focus):Mathf.Lerp(1.55f,1.42f,focus);
            int trail=j%3;float a=angle*Mathf.Deg2Rad+j/3*(Mathf.PI*2/12)+time*(law?.42f:-.32f)-trail*.028f;float pulse=.72f+.28f*Mathf.Sin(time*2.2f+j);var color=law?new Color(.91f,.72f,.30f):new Color(.57f,.95f,.82f);color.a=focus*focus*(1-trail*.27f)*pulse*.90f;
            focusParticles[group*36+j]=new ParticleSystem.Particle{position=new Vector3(Mathf.Sin(a)*rx,Mathf.Lerp(-.07f,.09f,focus)+Mathf.Sin(a*3+time)*.035f,Mathf.Cos(a)*rz+(1-focus)*.20f),startColor=color,startSize=(trail==0?.065f:.037f)*Mathf.Lerp(.6f,1,focus),startLifetime=10,remainingLifetime=10};
        }
        focusStars.SetParticles(focusParticles,focusParticles.Length);
    }
    void ReleaseTargets(){if(nearTarget){nearTarget.Release();Destroy(nearTarget);}if(farTarget){farTarget.Release();Destroy(farTarget);}}
    void OnEnable(){if(stage){stage.gameObject.SetActive(true);lastRender=Time.unscaledTime;SyncPortrait();}}
    void OnDisable(){if(stage)stage.gameObject.SetActive(false);if(data)EndDrag(true);speed=damping=0;held=false;}
    void OnDestroy(){ReleaseTargets();if(stage)Destroy(stage.gameObject);foreach(var m in materials)if(m)Destroy(m);if(lawMesh)Destroy(lawMesh);if(skillMesh)Destroy(skillMesh);}
}

public sealed class UIPaperEquipmentSlot : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IDropHandler,ICanvasRaycastFilter
{
    public UIPaperEquipmentOrbit Owner;public bool Law;public int Index;public bool Hovered {get;private set;}public bool Pressed {get;private set;}bool rotating;
    public void OnPointerEnter(PointerEventData e){Hovered=true;Owner.Hover(Law,Index);}public void OnPointerExit(PointerEventData e){Hovered=false;Owner.Leave();}public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left){Pressed=true;Owner.Hold(true);}}public void OnPointerUp(PointerEventData e){if(e.button==PointerEventData.InputButton.Left){Pressed=false;Owner.Hold(false);}}
    public void OnBeginDrag(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;var entry=Owner.Entry(Law,Index);rotating=entry==null;if(!rotating)Owner.BeginDrag(entry,Index,Law,e);}
    public void OnDrag(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;if(rotating)Owner.Rotate(-e.delta.x*.3f);else Owner.Drag(e);}public void OnEndDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left){Owner.EndDrag();rotating=false;}}public void OnDrop(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.Drop(Law,Index);}
    public bool IsRaycastLocationValid(Vector2 point,Camera camera){var r=(RectTransform)transform;return RectTransformUtility.ScreenPointToLocalPointInRectangle(r,point,camera,out var p)&&(p-r.rect.center).sqrMagnitude<r.rect.width*r.rect.width*.28f;}
    void OnDisable(){Hovered=Pressed=rotating=false;}
}
public sealed class UIPaperEquipmentOrbitDrag : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler,IBeginDragHandler,IEndDragHandler
{
    public UIPaperEquipmentOrbit Owner;public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.Hold(true);}public void OnPointerUp(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.Hold(false);}public void OnBeginDrag(PointerEventData e)=>OnPointerDown(e);public void OnDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.Rotate(-e.delta.x*.3f);}public void OnEndDrag(PointerEventData e)=>OnPointerUp(e);
}
public sealed class UIPaperEquipmentReturnDrop : MonoBehaviour,IDropHandler
{public UIPaperEquipmentOrbit Owner;public void OnDrop(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.ReturnToInventory();}}
