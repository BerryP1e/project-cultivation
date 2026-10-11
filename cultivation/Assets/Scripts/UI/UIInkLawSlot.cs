using UnityEngine;
using UnityEngine.EventSystems;
public sealed class UIInkLawSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, ICanvasRaycastFilter
{
    public int 槽位 { get; private set; }
    UIInkLawPage page; bool rotating;
    public void 初始化(UIInkLawPage owner, int slot) { page = owner; 槽位 = slot; }
    public void OnBeginDrag(PointerEventData e) { if (e.button != PointerEventData.InputButton.Left) return; var law = page.取槽(槽位); rotating = law == null; if (rotating) page.开始转环(); else page.开始拖拽(law, 槽位, e); }
    public void OnDrag(PointerEventData e) { if (e.button != PointerEventData.InputButton.Left) return; if (rotating) page.旋转(-e.delta.x * .3f); else page.拖动(e); }
    public void OnEndDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { if (rotating) page.结束转环(); else page.结束拖拽(); rotating = false; } }
    public void OnDrop(PointerEventData e) { page.放到槽(槽位); }
    public bool IsRaycastLocationValid(Vector2 point, Camera camera) { var rect = (RectTransform)transform; RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, camera, out var local); return local.sqrMagnitude < Mathf.Pow(rect.rect.width * .53f, 2); }
}
