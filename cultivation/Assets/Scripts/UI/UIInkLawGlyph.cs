using UnityEngine;

/// <summary>三维槽的晶体与两张透明装饰层，独立旋转和呼吸。</summary>
public sealed class UIInkLawGlyph : MonoBehaviour
{
    SpriteRenderer core,ink,spirit;
    LawFragment current;
    public void 初始化(SpriteRenderer glyph)
    {
        core=glyph;ink=Layer("InkWisp",.01f);spirit=Layer("SpiritRibbon",-.01f);
    }
    SpriteRenderer Layer(string name,float depth)
    {var go=new GameObject(name,typeof(SpriteRenderer));go.layer=31;go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.forward*depth;return go.GetComponent<SpriteRenderer>();}
    public void 设置(LawFragment law,float alpha)
    {
        if(current!=law){current=law;var cfg=LawFragmentCatalog.取();ink.sprite=law!=null&&cfg.墨纹图层!=null&&cfg.墨纹图层.Length>0?cfg.墨纹图层[law.境界%cfg.墨纹图层.Length]:null;spirit.sprite=law!=null&&cfg.灵气图层!=null&&cfg.灵气图层.Length>0?cfg.灵气图层[law.境界%cfg.灵气图层.Length]:null;Scale(ink);Scale(spirit);}
        ink.enabled=ink.sprite;spirit.enabled=spirit.sprite;if(law==null)return;
        float time=UIInkMotion.减少动效?0:Time.unscaledTime;
        ink.transform.localRotation=Quaternion.Euler(0,0,time*(3+law.境界*.4f));spirit.transform.localRotation=Quaternion.Euler(0,0,-time*(4+law.境界*.4f));
        ink.color=new Color(.68f,.75f,.70f,.42f*alpha);spirit.color=new Color(.85f,.92f,.81f,(.48f+.08f*Mathf.Sin(time*.7f))*alpha);
        core.transform.localRotation=Quaternion.Euler(Mathf.Sin(time*.55f+law.境界)*5,Mathf.Sin(time*.7f+law.境界)*7,0);
    }
    static void Scale(SpriteRenderer layer){if(layer.sprite){float size=Mathf.Max(layer.sprite.bounds.size.x,layer.sprite.bounds.size.y);layer.transform.localScale=Vector3.one*(.83f/Mathf.Max(.001f,size));}}
}
