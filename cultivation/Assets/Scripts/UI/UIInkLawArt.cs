using UnityEngine;
using UnityEngine.UI;

/// <summary>独立晶体/墨纹/灵气图层，各自运动；属性换色只作用于晶体。</summary>
public sealed class UIInkLawArt : MonoBehaviour
{
    public LawFragment 法则 { get; private set; }
    Image owner, crystal, ink, spirit;
    UIInkLawFilaments flow;
    bool large;
    public static UIInkLawArt 应用(Image image,LawFragment law,bool detail=false)
    {
        var art=image.GetComponent<UIInkLawArt>();if(!art)art=image.gameObject.AddComponent<UIInkLawArt>();
        if(!art.owner)art.Build(image,detail);art.设置(law);return art;
    }
    void Build(Image image,bool detail)
    {
        owner=image;large=detail;owner.enabled=false;
        ink=Layer("InkWisp",1.06f);var rect=UIBuildUtils.CreateRect("LawFlow",transform);UIBuildUtils.Stretch(rect);flow=rect.gameObject.AddComponent<UIInkLawFilaments>();flow.raycastTarget=false;
        crystal=Layer("CrystalCore",.82f);spirit=Layer("SpiritRibbon",1.02f);
    }
    Image Layer(string name,float scale)
    {var image=UIBuildUtils.CreateImage(name,transform,Color.white);UIBuildUtils.Stretch(image.rectTransform);image.rectTransform.localScale=Vector3.one*scale;image.preserveAspect=true;image.raycastTarget=false;return image;}
    public void 设置(LawFragment law)
    {
        法则=law;owner.enabled=false;foreach(Transform child in transform)child.gameObject.SetActive(law!=null);if(law==null)return;
        crystal.sprite=law.图标;crystal.color=law.颜色;var cfg=LawFragmentCatalog.取();
        ink.sprite=cfg.墨纹图层!=null&&cfg.墨纹图层.Length>0?cfg.墨纹图层[law.境界%cfg.墨纹图层.Length]:null;ink.enabled=ink.sprite;
        spirit.sprite=cfg.灵气图层!=null&&cfg.灵气图层.Length>0?cfg.灵气图层[law.境界%cfg.灵气图层.Length]:null;spirit.enabled=spirit.sprite;
        flow.境界=law.境界;flow.清晰度=large?1:.68f;
    }
    void LateUpdate()
    {
        if(法则==null)return;float time=UIInkMotion.减少动效?0:Time.unscaledTime,phase=法则.境界*.73f;
        crystal.rectTransform.anchoredPosition=Vector2.up*Mathf.Sin(time*.9f+phase)*(large?2:1);
        crystal.rectTransform.localRotation=Quaternion.Euler(Mathf.Sin(time*.55f+phase)*5,Mathf.Sin(time*.4f+phase)*6,Mathf.Sin(time*.7f+phase)*2);
        ink.rectTransform.localRotation=Quaternion.Euler(0,0,time*(3+法则.境界*.35f)+phase*25);
        spirit.rectTransform.localRotation=Quaternion.Euler(0,0,-time*(4+法则.境界*.4f)-phase*20);
        ink.color=new Color(.62f,.69f,.66f,.38f+.08f*Mathf.Sin(time*.6f+phase));
        spirit.color=new Color(.83f,.90f,.80f,.47f+.10f*Mathf.Sin(time*.8f+phase));
    }
}
