using UnityEngine;
using UnityEngine.UI;

/// <summary>宣纸分类与物品格的绘制层；沉降不移动按钮命中框和网格布局。</summary>
public sealed class UIPaperRelief : MonoBehaviour
{
    RawImage face,shadow;
    RectTransform content;
    Material faceMaterial,shadowMaterial;
    float hoverValue,pressValue,selectedValue,hoverVelocity,pressVelocity,selectedVelocity;
    int updatedFrame=-1;

    public void 绘制(bool category,bool occupied,bool selected,bool hover,bool pressed,Color tone,bool round=false)
    {
        if (!face) Build(category,round);
        if (!faceMaterial) return;
        if(updatedFrame!=Time.frameCount)
        {
            updatedFrame=Time.frameCount;float delta=Mathf.Min(Time.unscaledDeltaTime,.04f);
            hoverValue=Mathf.SmoothDamp(hoverValue,hover?1:0,ref hoverVelocity,.035f,Mathf.Infinity,delta);
            pressValue=Mathf.SmoothDamp(pressValue,pressed?1:0,ref pressVelocity,pressed?.018f:.038f,Mathf.Infinity,delta);
            selectedValue=Mathf.SmoothDamp(selectedValue,selected?1:0,ref selectedVelocity,.035f,Mathf.Infinity,delta);
            if(UIInkMotion.减少动效){hoverValue=hover?1:0;pressValue=pressed?1:0;selectedValue=selected?1:0;}
        }
        var rect=(RectTransform)transform;
        faceMaterial.SetVector("_Size",new Vector4(rect.rect.width,rect.rect.height,0,0));
        faceMaterial.SetFloat("_Kind",round?4:category?0:1);
        faceMaterial.SetFloat("_Occupied",occupied?1:0);
        faceMaterial.SetFloat("_Selected",selectedValue);
        faceMaterial.SetFloat("_Hover",hoverValue);
        faceMaterial.SetFloat("_Pressed",pressValue);
        faceMaterial.SetColor("_Tint",tone);
        float depth=(category?2.7f:round?1.6f:0)*pressValue+(category?.7f:0)*selectedValue*(1-pressValue);
        face.rectTransform.anchoredPosition=new Vector2(0,-depth);
        if(content)content.anchoredPosition=new Vector2(0,-depth);
        if(shadowMaterial)
        {
            shadowMaterial.SetVector("_Size",new Vector4(rect.rect.width,category?12:rect.rect.height,0,0));
            shadowMaterial.SetFloat("_Pressed",pressValue);
            shadowMaterial.SetFloat("_Selected",selectedValue);
            shadowMaterial.SetFloat("_Occupied",occupied?1:0);
        }
    }

    void Build(bool category,bool round)
    {
        var shader=Resources.Load<Shader>("UI/InkUI/Bag/PaperControl");
        if(!shader)return;
        if(!round)
        {
            shadow=UIBuildUtils.CreateRect(category?"CategorySoftShadow":"CellOuterShadow",transform).gameObject.AddComponent<RawImage>();
            shadow.texture=Texture2D.whiteTexture;shadow.raycastTarget=false;
            if(category){UIPaperArt.Place(shadow.rectTransform,0,0,1,0);shadow.rectTransform.offsetMin=new Vector2(-3,-8);shadow.rectTransform.offsetMax=new Vector2(3,5);}
            else{UIBuildUtils.Stretch(shadow.rectTransform);shadow.rectTransform.offsetMin=new Vector2(-5,-7);shadow.rectTransform.offsetMax=new Vector2(7,4);}
            shadow.transform.SetAsFirstSibling();shadowMaterial=new Material(shader);shadowMaterial.SetFloat("_Kind",category?2:3);shadow.material=shadowMaterial;
        }
        face=UIBuildUtils.CreateRect("PaperReliefFace",transform).gameObject.AddComponent<RawImage>();
        face.texture=Texture2D.whiteTexture;face.raycastTarget=false;UIBuildUtils.Stretch(face.rectTransform);
        faceMaterial=new Material(shader);face.material=faceMaterial;face.transform.SetAsFirstSibling();
        if(category){faceMaterial.SetTexture("_DetailTex",Resources.Load<Texture2D>("UI/InkUI/Bag/category-face-scroll"));}
        if(shadow)shadow.transform.SetAsFirstSibling();
        if(category||round)
        {
            content=UIBuildUtils.CreateRect("CategoryContent",transform);UIBuildUtils.Stretch(content);
            var label=transform.Find("Label");if(label)label.SetParent(content,false);
            var icon=transform.Find("CategoryIcon");if(icon)icon.SetParent(content,false);
            var appearance=transform.Find("AppearanceIcon");if(appearance)appearance.SetParent(content,false);
        }
    }

    void OnDisable(){hoverValue=pressValue=selectedValue=hoverVelocity=pressVelocity=selectedVelocity=0;updatedFrame=-1;}
    void OnDestroy(){if(faceMaterial)Destroy(faceMaterial);if(shadowMaterial)Destroy(shadowMaterial);}
}

public sealed class UIPaperRoundHit : MonoBehaviour,ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 point,Camera eventCamera)
    {
        var rect=(RectTransform)transform;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,point,eventCamera,out var local))return false;
        var offset=local-rect.rect.center;return new Vector2(offset.x/rect.rect.width,offset.y/rect.rect.height).sqrMagnitude<=.25f;
    }
}

/// <summary>普通按钮仅下移绘制顶点，点击区域保持固定。</summary>
public sealed class UIPaperPressOffset : BaseMeshEffect
{
    float offset;
    public void 下沉(float value){if(Mathf.Approximately(offset,value))return;offset=value;if(graphic)graphic.SetVerticesDirty();}
    public override void ModifyMesh(VertexHelper mesh)
    {
        if(!IsActive()||offset==0)return;
        UIVertex vertex=default;
        for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);vertex.position.y-=offset;mesh.SetUIVertex(vertex,i);}
    }
}
