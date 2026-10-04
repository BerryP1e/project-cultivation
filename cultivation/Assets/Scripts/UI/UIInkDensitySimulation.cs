using UnityEngine;

/// <summary>二维墨密度与速度的有状态平流/扩散近似。不是刚体，也不是不可压缩流体求解器。</summary>
public sealed class UIInkDensitySimulation
{
    const int Size=128;
    RenderTexture read,write;
    Material solver;
    float accumulated,clock;
    public RenderTexture 纹理 => read;
    public int 步数 { get; private set; }
    public UIInkDensitySimulation(Texture shape,float seed)
    {
        var shader=Shader.Find("Hidden/Cultivation/InkDensity");
        if(shader==null || !shader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return;
        solver=new Material(shader){hideFlags=HideFlags.DontSave};
        solver.SetTexture("_BaseTex",shape); solver.SetFloat("_Seed",seed);
        read=Create("InkDensityRead"); write=Create("InkDensityWrite");
        solver.SetFloat("_Reset",1); Graphics.Blit(Texture2D.blackTexture,read,solver);
        solver.SetFloat("_Reset",0);
    }
    static RenderTexture Create(string name)
    {
        var rt=new RenderTexture(Size,Size,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
        rt.Create(); return rt;
    }
    public void 推进(float delta,Vector2 pointer,Vector2 velocity,bool inside,Vector2 click,float clickAge)
    {
        if(solver==null) return;
        accumulated+=Mathf.Min(delta,.1f);
        const float step=1f/30f;
        int count=0;
        while(accumulated>=step && count++<3)
        {
            accumulated-=step; clock+=step;
            solver.SetFloat("_Step",step); solver.SetFloat("_Clock",clock);
            solver.SetVector("_Pointer",new Vector4(pointer.x,pointer.y,inside ? 1 : 0,0));
            solver.SetVector("_Drag",new Vector4(velocity.x,velocity.y,0,0));
            solver.SetVector("_Click",new Vector4(click.x,click.y,Mathf.Clamp01(clickAge/1.1f),1));
            Graphics.Blit(read,write,solver); var swap=read; read=write; write=swap; 步数++;
        }
    }
    public void 释放()
    {
        if(read!=null){read.Release(); Object.Destroy(read);}
        if(write!=null){write.Release(); Object.Destroy(write);}
        if(solver!=null) Object.Destroy(solver);
        read=write=null; solver=null;
    }
}
