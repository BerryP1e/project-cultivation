using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>同一份材料支持点击投料与真实鼠标拖拽，取消拖拽由丹房视图归位。</summary>
public class UIInkAlchemyDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public UIInkAlchemyPage 视图;
    public ItemDefinition 材料;
    public bool 主材;
    bool dragged;
    public void OnBeginDrag(PointerEventData e)
    { if(e.button!=PointerEventData.InputButton.Left)return;dragged=视图.开始拖动(材料,(RectTransform)transform,e.position,主材); }
    public void OnDrag(PointerEventData e) { if(dragged)视图.拖动(e.position); }
    public void OnEndDrag(PointerEventData e) { if(dragged){视图.结束拖动(e.position);e.eligibleForClick=false;}dragged=false; }
    public void OnPointerClick(PointerEventData e)
    { if(e.button==PointerEventData.InputButton.Left && !dragged)视图.点击投料(材料,(RectTransform)transform,主材);dragged=false; }
}
