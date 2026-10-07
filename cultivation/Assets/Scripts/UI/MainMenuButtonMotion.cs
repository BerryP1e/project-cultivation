using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Only the ink drawing moves; the button hit rectangle stays fixed.</summary>
public sealed class MainMenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    Image curtain;
    bool hover, pressed, focused;
    float blend, press;
    public void Initialize(Image drawing) { curtain=drawing; }
    public void OnPointerEnter(PointerEventData e) { hover=true; }
    public void OnPointerExit(PointerEventData e) { hover=pressed=false; }
    public void OnPointerDown(PointerEventData e) { pressed=true; }
    public void OnPointerUp(PointerEventData e) { pressed=false; }
    public void OnSelect(BaseEventData e) { focused=true; }
    public void OnDeselect(BaseEventData e) { focused=false; }
    void Update()
    {
        if(curtain==null) return;
        float dt=Time.unscaledDeltaTime;
        blend=Mathf.MoveTowards(blend,hover||focused?1:0,dt*7);
        press=Mathf.MoveTowards(press,pressed?1:0,dt*16);
        float movement=UIInkMotion.减少动效?0:1;
        curtain.rectTransform.localScale=new Vector3(1+blend*.055f-press*.045f,1+blend*.035f-press*.03f,1);
        curtain.rectTransform.anchoredPosition=new Vector2(blend*6,Mathf.Sin(Time.unscaledTime*.65f+transform.GetSiblingIndex())*1.3f*movement);
        curtain.color=Color.Lerp(new Color(.17f,.24f,.23f,.94f),new Color(.27f,.38f,.34f,1),blend);
    }
}
