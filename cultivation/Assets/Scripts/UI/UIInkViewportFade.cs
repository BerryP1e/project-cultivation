using UnityEngine;

using UnityEngine.UI;

/// <summary>同一视口共享逐像素墨纹渐隐，覆盖图标、文字和衬影，不新增遮挡贴图。</summary>
public class UIInkViewportFade : MonoBehaviour
{
    public Material Material { get; private set; }
    public float FadeWidth=82;
    void Awake()
    {
        var canvas=GetComponentInParent<Canvas>();
        if(canvas!=null) canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
        var shader=Shader.Find("Cultivation/UI/InkViewportFade");
        if(shader!=null) Material=new Material(shader);
    }
    void LateUpdate()
    {
        if(Material==null) return;
        var rt=(RectTransform)transform;
        Material.SetMatrix("_WorldToViewport",rt.worldToLocalMatrix);
        Material.SetVector("_FadeBounds",new Vector4(rt.rect.yMin,rt.rect.yMax,FadeWidth,0));
    }
    void OnDestroy() { if(Material!=null) Destroy(Material); }
}

/// <summary>将视口坐标写入 UV1，避免 Canvas 合批改变 shader 的对象矩阵。</summary>
public class UIInkFadeCoordinates : BaseMeshEffect
{
    public RectTransform Viewport;
    Matrix4x4 previous;
    public override void ModifyMesh(VertexHelper vertices)
    {
        if(!IsActive() || Viewport==null) return;
        var matrix=Viewport.worldToLocalMatrix*graphic.rectTransform.localToWorldMatrix;
        UIVertex vertex=new UIVertex();
        for(int i=0;i<vertices.currentVertCount;i++) {
            vertices.PopulateUIVertex(ref vertex,i);
            vertex.uv1=matrix.MultiplyPoint3x4(vertex.position);
            vertices.SetUIVertex(vertex,i);
        }
    }
    void LateUpdate()
    {
        if(Viewport==null || graphic==null) return;
        var matrix=Viewport.worldToLocalMatrix*graphic.rectTransform.localToWorldMatrix;
        if(matrix!=previous) { previous=matrix; graphic.SetVerticesDirty(); }
    }
}
