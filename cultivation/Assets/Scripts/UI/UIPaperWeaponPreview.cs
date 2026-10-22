using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>双环圆心的独立武器预览；只实例化武器资源，不复制玩家或战斗组件。</summary>
public sealed class UIPaperWeaponPreview : MonoBehaviour
{
    UIPaperEquipmentOrbit orbit;UIInkBagPage owner;UIPanelData data;
    Transform pivot,model;WeaponDefinition current;Button hit;CanvasGroup group;
    readonly List<Material> materials=new List<Material>();readonly List<Mesh> meshes=new List<Mesh>();
    float focus,velocity;public float Focus=>focus;public Transform Model=>model;
    public void Initialize(UIPaperEquipmentOrbit o,UIInkBagPage page,UIPanelData source,Font font)
    {
        orbit=o;owner=page;data=source;pivot=new GameObject("CentralWeaponPivot").transform;pivot.SetParent(transform,false);pivot.localPosition=new Vector3(0,1.5f,-.3f);
        hit=UIBuildUtils.CreateButton("CentralWeaponSlot",o.transform,font,"",14);hit.image.color=Color.clear;hit.transition=Selectable.Transition.None;
        group=hit.gameObject.AddComponent<CanvasGroup>();hit.onClick.AddListener(()=>{if(data.当前武器)owner.ShowTooltip(data.当前武器,true);});
        hit.gameObject.AddComponent<UIPaperWeaponSlot>().Owner=this;
    }
    void LateUpdate()
    {
        if(!data||!orbit.Ready)return;if(current!=data.当前武器){current=data.当前武器;Rebuild();}
        float goal=owner.Category==2?1:0;focus=UIInkMotion.减少动效?goal:Mathf.SmoothDamp(focus,goal,ref velocity,.17f,Mathf.Infinity,Time.unscaledDeltaTime);
        float time=UIInkMotion.减少动效?0:Time.unscaledTime;
        pivot.localScale=Vector3.one*Mathf.Lerp(.67f,1.13f,focus);
        pivot.localRotation=Quaternion.Euler(0,0,-23)*Quaternion.Euler(0,time*12,0);
        foreach(var mat in materials){mat.SetFloat("_Opacity",Mathf.Lerp(.29f,1,focus));mat.SetFloat("_Focus",focus);}
        if(model)foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=focus>.5f?31:30;
        var p=orbit.PreviewCamera.WorldToViewportPoint(pivot.position);var rt=(RectTransform)hit.transform;rt.anchorMin=rt.anchorMax=new Vector2(p.x,p.y);rt.anchoredPosition=Vector2.zero;rt.sizeDelta=new Vector2(Mathf.Lerp(44,86,focus),Mathf.Lerp(100,210,focus));group.alpha=current?1:.3f;
        hit.GetComponentInChildren<Text>().text=current?"":"武器";
    }
    void Rebuild()
    {
        if(model)Destroy(model.gameObject);foreach(var m in materials)Destroy(m);materials.Clear();foreach(var m in meshes)Destroy(m);meshes.Clear();
        pivot.localRotation=Quaternion.identity;pivot.localScale=Vector3.one;
        if(!current)return;var prefab=Resources.Load<GameObject>(current.模型资源路径);if(!prefab)return;
        model=Instantiate(prefab,pivot,false).transform;model.name="EquippedWeaponModel";
        foreach(var c in model.GetComponentsInChildren<Collider>(true))Destroy(c);foreach(var a in model.GetComponentsInChildren<Animator>(true))a.enabled=false;
        // Freeze skinned weapon geometry in its authored pose so no preview animator is needed.
        foreach(var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            var mesh=new Mesh();skinned.BakeMesh(mesh);meshes.Add(mesh);var part=new GameObject("WeaponMesh",typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(skinned.transform,false);part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterials=skinned.sharedMaterials;skinned.enabled=false;
        }
        Bounds bounds=new Bounds();bool first=true;
        foreach(var r in model.GetComponentsInChildren<MeshRenderer>()){
            if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);
            var copy=new Material[r.sharedMaterials.Length];for(int i=0;i<copy.Length;i++){var source=r.sharedMaterials[i];var m=new Material(Resources.Load<Shader>("UI/InkUI/Bag/PaperWeapon"));if(source&&source.HasProperty("_MainTex"))m.mainTexture=source.mainTexture;if(source&&source.HasProperty("_Color"))m.color=source.color;copy[i]=m;materials.Add(m);}r.sharedMaterials=copy;
        }
        if(first)return;
        var size=bounds.size;Vector3 axis=size.x>size.y&&size.x>size.z?Vector3.right:size.z>size.y?Vector3.forward:Vector3.up;
        // Authored weapon pivot is at the grip; the longer half points toward the blade.
        Vector3 fromGrip=bounds.center-model.position;if(Vector3.Dot(fromGrip,axis)<0)axis=-axis;
        var align=Quaternion.FromToRotation(axis,Vector3.down);Vector3 offset=pivot.InverseTransformPoint(bounds.center);float scale=2.65f/Mathf.Max(.001f,Mathf.Max(size.x,Mathf.Max(size.y,size.z)));
        model.localRotation=align*model.localRotation;model.localScale*=scale;model.localPosition=-(align*offset)*scale;
    }
    public void Hover(){if(current)owner.ShowTooltip(current,false);}public void Leave()=>owner.LeaveCell();
    public void Begin(PointerEventData e){if(!current)return;orbit.BeginDrag(current,-3,false,e);}
    public void Drop(){if(UIDragContext.Entry is WeaponDefinition weapon&&data.已拥有武器(weapon)){data.装备武器(weapon);orbit.EndDrag(true);}}
    public void Drag(PointerEventData e)=>orbit.Drag(e);public void End()=>orbit.EndDrag();
    void OnDestroy(){if(hit)Destroy(hit.gameObject);foreach(var m in materials)if(m)Destroy(m);foreach(var m in meshes)if(m)Destroy(m);}
}
public sealed class UIPaperWeaponSlot : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IDropHandler
{
    public UIPaperWeaponPreview Owner;
    public void OnPointerEnter(PointerEventData e)=>Owner.Hover();public void OnPointerExit(PointerEventData e)=>Owner.Leave();
    public void OnBeginDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.Begin(e);}public void OnDrag(PointerEventData e)=>Owner.Drag(e);public void OnEndDrag(PointerEventData e)=>Owner.End();public void OnDrop(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.Drop();}
}
