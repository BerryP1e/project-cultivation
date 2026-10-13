using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
/// <summary>沿弧拖动滚动，向弧内拖动装备。</summary>
public sealed class UIInkLawItemDrag : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public LawFragment 法则 { get; private set; }
    UIInkLawPage page; ScrollRect scroll; UIInkLawArc arc; bool scrolling;
    LawFragment pressedLaw;
    public void 初始化(UIInkLawPage owner, LawFragment law, ScrollRect list) { page = owner; 法则 = law; scroll = list; }
    public void 初始化(UIInkLawPage owner, LawFragment law, UIInkLawArc list) { page = owner; 法则 = law; arc = list; }
    public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;pressedLaw=法则;if(scroll)scroll.StopMovement();}
    public void OnBeginDrag(PointerEventData e) {
        if(e.button!=PointerEventData.InputButton.Left)return;
        // 新背包卡片始终拖法则，滚动由空白处拖动或滚轮负责，不要求预先选中。
        if(scroll&&!arc){scrolling=false;var source=pressedLaw??法则;page.选择(source);page.开始拖拽(source,-1,e);return;}
        var movement=e.position-e.pressPosition;var tangent=arc?arc.切线(e.pressPosition,e.pressEventCamera):Vector2.up;
        scrolling=Mathf.Abs(Vector2.Dot(movement,tangent))>Mathf.Abs(Vector2.Dot(movement,new Vector2(-tangent.y,tangent.x)))*1.4f;
        if(scrolling){if(arc)arc.OnBeginDrag(e);else if(scroll)scroll.OnBeginDrag(e);}else page.开始拖拽(pressedLaw??法则,-1,e);
    }
    public void OnDrag(PointerEventData e) { if (e.button != PointerEventData.InputButton.Left) return; if (scrolling){if(arc)arc.OnDrag(e);else if(scroll)scroll.OnDrag(e);}else page.拖动(e); }
    public void OnEndDrag(PointerEventData e) { if (e.button != PointerEventData.InputButton.Left) return; if (scrolling){if(arc)arc.OnEndDrag(e);else if(scroll)scroll.OnEndDrag(e);}else page.结束拖拽();scrolling=false; }
}
