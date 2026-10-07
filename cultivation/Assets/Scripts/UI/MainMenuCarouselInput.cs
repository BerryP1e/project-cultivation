using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MainMenuCarouselInput : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    public MainMenuSaveCarousel target;
    public void OnBeginDrag(PointerEventData e) { target.OnBeginDrag(e); }
    public void OnEndDrag(PointerEventData e) { target.OnEndDrag(e); }
    public void OnScroll(PointerEventData e) { target.OnScroll(e); }
}
