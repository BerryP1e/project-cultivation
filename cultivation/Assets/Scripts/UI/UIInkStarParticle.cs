using UnityEngine;
using UnityEngine.UI;

public class UIInkStarParticle : MonoBehaviour
{
    public Vector2 牵引位移 {get;private set;}
    Image image;float phase;Color tint;Vector2 shift,velocity;
#if UNITY_EDITOR
    public Vector2? 调试局部鼠标;
#endif
    public void Initialize(int seed,Color color){phase=seed*1.71f;tint=color;image=UIBuildUtils.CreateImage("SoftStar",transform,color);image.sprite=InkUITheme.Load("SkillsDynamic/fx-star-particle");image.raycastTarget=false;UIBuildUtils.Stretch(image.rectTransform);}
    void LateUpdate(){
        if(image==null)return;var canvas=GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform,Input.mousePosition,cam,out var point);
#if UNITY_EDITOR
        if(调试局部鼠标.HasValue)point=调试局部鼠标.Value;
#endif
        var goal=UIInkMotion.减少动效?Vector2.zero:Vector2.ClampMagnitude(point,5)*Mathf.Clamp01(1-point.magnitude/110);
        shift=Vector2.SmoothDamp(shift,goal,ref velocity,.10f,100,Time.unscaledDeltaTime);牵引位移=shift;image.rectTransform.anchoredPosition=shift;
        var c=tint;c.a=UIInkMotion.减少动效?1:.76f+.24f*Mathf.Sin(Time.unscaledTime*1.4f+phase);image.color=c;
    }
}
