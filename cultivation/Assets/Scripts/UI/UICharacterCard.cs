using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>统一物品卡底：圆角细框、暗底纹样、品质渐光、独立名称底栏。</summary>
public sealed class UICharacterCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    Material owned;Sprite uv;Image image;bool selected,hovered;
    public void 初始化(Image target)
    {
        if(owned)return;image=target;
        var shader=Resources.Load<Shader>("UI/InkUI/Dynamic/CharacterCard");if(!shader)return;
        owned=new Material(shader);
        uv=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f);
        image.sprite=uv;image.overrideSprite=null;image.type=Image.Type.Simple;image.material=owned;image.color=Color.white;
    }
    public void 绑定(Color quality,bool active)
    {
        selected=active;
        if(owned){image.sprite=uv;image.overrideSprite=null;image.type=Image.Type.Simple;image.material=owned;image.color=Color.white;owned.SetColor("_Quality",quality);owned.SetFloat("_Selected",selected?1:0);}
    }
    public void 渐隐(RectTransform viewport)
    {
        if(!owned)return;var coords=image.GetComponent<UIInkFadeCoordinates>();if(!coords)coords=image.gameObject.AddComponent<UIInkFadeCoordinates>();coords.Viewport=viewport;
        owned.SetFloat("_Fade",1);owned.SetVector("_FadeBounds",new Vector4(viewport.rect.yMin,viewport.rect.yMax,36,0));
    }
    void LateUpdate(){if(owned){var size=image.rectTransform.rect.size;owned.SetVector("_Size",new Vector4(size.x,size.y,0,0));owned.SetFloat("_Hover",hovered?.35f:0);}}
    public void OnPointerEnter(PointerEventData e)=>hovered=true;
    public void OnPointerExit(PointerEventData e)=>hovered=false;
    void OnDisable()=>hovered=false;
    void OnDestroy(){if(owned)Destroy(owned);if(uv)Destroy(uv);}
}
