using UnityEngine;
using UnityEngine.UI;

/// <summary>与相机共享纸纹、屏幕坐标和平铺。仅覆盖父页面，不向世界再次叠纸。</summary>
public class UIInkPaperLayer : MonoBehaviour
{
    Material material;
    RawImage image;
    GameGlobalGrade grade;
    public float 强度=0.12f;
    public static void 应用(RectTransform window) {
        var layer=window.Find("InkSharedPaper")?.GetComponent<UIInkPaperLayer>();
        if(layer==null) {
            var rt=UIBuildUtils.CreateRect("InkSharedPaper",window); UIBuildUtils.Stretch(rt);
            layer=rt.gameObject.AddComponent<UIInkPaperLayer>();
        }
        layer.transform.SetAsLastSibling();
    }
    void Awake() {
        image=gameObject.AddComponent<RawImage>(); image.raycastTarget=false;
        var shader=Shader.Find("Cultivation/UI/InkPaper");
        if(shader!=null) { material=new Material(shader); image.material=material; }
        grade=FindObjectOfType<GameGlobalGrade>();
    }
    void LateUpdate() {
        if(material==null) return;
        var grain=grade!=null && grade.纸纹!=null ? grade.纸纹 : Resources.Load<Texture2D>("宣纸/宣纸纹理_纸纹");
        image.texture=grain; image.enabled=grain!=null;
        material.SetFloat("_Tiling",grade!=null ? grade.纸纹平铺 : 1);
        material.SetFloat("_Strength",强度);
        material.SetFloat("_Gain",grade!=null ? grade.纸纹对比 : 2);
        material.SetFloat("_Pre",grade==null || grade.纸纹已归一化 ? 1 : 0);
    }
    void OnDestroy() { if(material!=null) Destroy(material); }
}
