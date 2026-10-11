using UnityEngine;
using UnityEngine.EventSystems;
public sealed class UIInkLawInventoryDrop : MonoBehaviour, IDropHandler
{
    UIInkLawPage page; public void 初始化(UIInkLawPage owner) { page = owner; }
    public void OnDrop(PointerEventData e) { page.放回收获(); }
}
