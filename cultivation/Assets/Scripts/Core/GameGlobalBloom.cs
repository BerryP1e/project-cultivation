using UnityEngine;

/// <summary>Built-in HDR 辉光：等尺寸双向模糊、逐级归一化合成，不依赖目标 RT 的历史内容。</summary>
[ExecuteAlways,RequireComponent(typeof(Camera)),ImageEffectAllowedInSceneView,DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class GameGlobalBloom : MonoBehaviour
{
    public bool 启用=true;
    [Range(0,4)] public float 阈值=1.05f;
    [Range(0,1)] public float 软膝=.45f;
    [Range(0,4)] public float 强度=.28f;
    [Range(0,4)] public float 模糊半径=1;
    [Range(2,7)] public int 级数=5;
    Material 材质;
    RenderTexture[] 降采样,升采样,模糊;
    int 宽,高;
    void OnEnable()
    {
        if(材质)return;var shader=Shader.Find("Cultivation/Bloom");
        if(shader&&shader.isSupported)材质=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
    }
    void OnDisable(){释放();if(材质)DestroyImmediate(材质);材质=null;}
    void 释放()
    {
        foreach(var group in new[]{降采样,升采样,模糊})if(group!=null)foreach(var rt in group)if(rt){rt.Release();DestroyImmediate(rt);}
        降采样=升采样=模糊=null;
    }
    static RenderTexture 创建(int width,int height)
    {
        var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.Default;
        var rt=new RenderTexture(width,height,0,format){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};rt.Create();return rt;
    }
    void 准备(int width,int height)
    {
        int count=Mathf.Clamp(级数,2,7);
        if(降采样!=null&&降采样.Length==count&&宽==width&&高==height)return;
        释放();宽=width;高=height;降采样=new RenderTexture[count];升采样=new RenderTexture[count];模糊=new RenderTexture[count];
        for(int i=0;i<count;i++){width=Mathf.Max(1,width/2);height=Mathf.Max(1,height/2);降采样[i]=创建(width,height);升采样[i]=创建(width,height);模糊[i]=创建(width,height);}
    }
    void OnRenderImage(RenderTexture 源,RenderTexture 目标)
    {
        if(!启用||强度<=.0001f){释放();Graphics.Blit(源,目标);return;}
        if(!材质)OnEnable();if(!材质){Graphics.Blit(源,目标);return;}
        准备(源.width,源.height);
        材质.SetFloat("_Threshold",阈值);材质.SetFloat("_SoftKnee",软膝);材质.SetFloat("_Intensity",强度);材质.SetFloat("_BlurRadius",模糊半径);
        Graphics.Blit(源,降采样[0],材质,0);
        for(int i=0;i<降采样.Length;i++)
        {
            if(i>0)Graphics.Blit(降采样[i-1],降采样[i]);
            材质.SetVector("_BlurDirection",new Vector4(1,0,0,0));Graphics.Blit(降采样[i],模糊[i],材质,1);
            材质.SetVector("_BlurDirection",new Vector4(0,1,0,0));Graphics.Blit(模糊[i],降采样[i],材质,1);
        }
        var current=降采样[降采样.Length-1];
        for(int i=降采样.Length-2;i>=0;i--){材质.SetTexture("_Source",降采样[i]);Graphics.Blit(current,升采样[i],材质,2);current=升采样[i];}
        材质.SetTexture("_BloomTex",current);Graphics.Blit(源,目标,材质,3);
    }
    public bool Enabled {get=>启用;set=>启用=value;}
}
