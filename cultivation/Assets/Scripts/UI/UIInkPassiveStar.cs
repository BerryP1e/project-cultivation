using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>生效被动的可交互卫星：悬停预览，点击沿用神通列表选中逻辑。</summary>
public class UIInkPassiveStar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    UIInkSkillsPage page;IPanelEntry entry;RectTransform tooltip;Image halo;bool hovered;
    public IPanelEntry Entry=>entry;
    public bool 信息可见=>tooltip!=null && tooltip.gameObject.activeSelf;
    public void Initialize(UIInkSkillsPage owner,IPanelEntry source){
        page=owner;entry=source;
        halo=UIBuildUtils.CreateImage("PassiveHalo",transform,new Color(.55f,1,1,.65f));halo.sprite=InkUITheme.Load("Realm/fx-ink-circle");halo.material=page.星点描边材质;halo.raycastTarget=false;UIBuildUtils.Stretch(halo.rectTransform,4);halo.transform.SetAsFirstSibling();
        tooltip=UIBuildUtils.CreateRect("PassiveStarTooltip",page.transform);tooltip.sizeDelta=new Vector2(270,155);
        var ink=UIBuildUtils.CreateImage("Ink",tooltip,new Color(.035f,.055f,.055f,.98f));ink.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4");UIBuildUtils.Stretch(ink.rectTransform,-20);
        var title=UIBuildUtils.CreateText("Name",tooltip,page.已悟神通.font,entry.DisplayName+" · 已启用",18,TextAnchor.MiddleLeft,new Color(.75f,1,1));
        UIBuildUtils.Place(title.rectTransform,new Vector2(0,.72f),Vector2.one,new Vector2(14,0),new Vector2(-14,-8));
        var desc=UIBuildUtils.CreateText("Description",tooltip,page.已悟神通.font,entry.DisplayDescription,16,TextAnchor.UpperLeft,new Color(.96f,.95f,.88f));
        UIBuildUtils.Place(desc.rectTransform,Vector2.zero,new Vector2(1,.72f),new Vector2(14,10),new Vector2(-14,-4));desc.verticalOverflow=VerticalWrapMode.Truncate;
        foreach(var text in tooltip.GetComponentsInChildren<Text>()){var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.95f);shadow.effectDistance=new Vector2(1,-1);}
        tooltip.gameObject.SetActive(false);
    }
    public void OnPointerEnter(PointerEventData e){hovered=true;tooltip.gameObject.SetActive(true);tooltip.SetAsLastSibling();PositionTooltip();}
    public void OnPointerExit(PointerEventData e){hovered=false;if(tooltip!=null)tooltip.gameObject.SetActive(false);}
    void PositionTooltip(){
        var parent=page.transform as RectTransform;var point=(Vector2)parent.InverseTransformPoint(transform.position)+new Vector2(150,65);
        point.x=Mathf.Clamp(point.x,parent.rect.xMin+145,parent.rect.xMax-145);point.y=Mathf.Clamp(point.y,parent.rect.yMin+90,parent.rect.yMax-90);tooltip.anchoredPosition=point;
    }
    void LateUpdate(){
        bool selected=page.详情.Current==entry;
        halo.color=new Color(.55f,1,1,selected||hovered?1:.55f);
        halo.rectTransform.localScale=Vector3.one*(selected||hovered?1.18f:1);
        if(hovered)PositionTooltip();
    }
    void OnDisable(){hovered=false;if(tooltip!=null)tooltip.gameObject.SetActive(false);}
    void OnDestroy(){if(tooltip!=null)Destroy(tooltip.gameObject);}
}
