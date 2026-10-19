using System.Collections.Generic;
using UnityEngine;

/// <summary>法阵线条全部为共用几何；各仪轨有独立空间轨道，不使用整张阵图平面。</summary>
public static class SpiritArrayGeometry
{
    static readonly Dictionary<SpiritArrayOp,Mesh[]> cache=new Dictionary<SpiritArrayOp,Mesh[]>();
    static readonly Dictionary<SpiritArrayOp,Mesh> cores=new Dictionary<SpiritArrayOp,Mesh>();
    public static int Count(SpiritArrayOp op)=>op==SpiritArrayOp.修正方向?1:op==SpiritArrayOp.多重施法?3:2;
    public static string Name(SpiritArrayOp op,int layer)=>layer==1?"OrbitingParticles":layer==2?"SpatialFilaments":op==SpiritArrayOp.生成灵弹?"EmissionSeed":op==SpiritArrayOp.追踪?"GuidanceOrbit":op==SpiritArrayOp.分裂?"SatelliteCircles":op==SpiritArrayOp.强化?"InscribedBand":op==SpiritArrayOp.加速?"FlowingStreaks":op==SpiritArrayOp.修正方向?"SpatialFilaments":"RitualRings";
    public static float Speed(SpiritArrayOp op,int layer)=>op==SpiritArrayOp.加速?(layer==0?85:-110):layer==1?-35:op==SpiritArrayOp.修正方向?5:11;
    public static Mesh Layer(SpiritArrayOp op,int layer)
    {
        if(!cache.TryGetValue(op,out var set)){set=new Mesh[Count(op)];for(int i=0;i<set.Length;i++)set[i]=Build(op,i);cache.Add(op,set);}return set[layer];
    }
    sealed class Shape
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Color> colors=new List<Color>();readonly List<Vector2> uv=new List<Vector2>();
        public void Line(Vector3 a,Vector3 b,float width=.008f,float alpha=1)
        {
            void Strip(float w,float opacity){var side=Vector3.Cross(b-a,Vector3.forward);if(side.sqrMagnitude<1e-7f)side=Vector3.Cross(b-a,Vector3.up);side=side.normalized*w*.5f;int n=vertices.Count;vertices.Add(a+side);vertices.Add(a-side);vertices.Add(b-side);vertices.Add(b+side);uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});for(int j=0;j<4;j++)colors.Add(new Color(1,1,1,opacity));}
            Strip(width*4,alpha*.06f);Strip(width,alpha);
        }
        public void Dot(Vector3 p,float size,float brightness){Line(p-Vector3.right*size*.5f,p+Vector3.right*size*.5f,size,brightness);}
        public void Ring(float r,float z=0,float width=.008f,int count=96,float start=0,float end=Mathf.PI*2){for(int j=0;j<count;j++)Line(Polar(Mathf.Lerp(start,end,j/(float)count),r,z),Polar(Mathf.Lerp(start,end,(j+1f)/count),r,z),width);}
        public void SmallCircle(Vector3 centre,float radius){for(int j=0;j<32;j++)Line(centre+Polar(j*Mathf.PI/16,radius),centre+Polar((j+1)*Mathf.PI/16,radius),.005f);}
        public Mesh Mesh(string name){var m=new Mesh{name=name};m.SetVertices(vertices);m.SetUVs(0,uv);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateBounds();return m;}
    }
    static Vector3 Polar(float a,float r,float z=0)=>new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z);
    static Mesh Build(SpiritArrayOp op,int layer)
    {
        var g=new Shape();int kind=(int)op;
        if(layer==1){
            int count=op==SpiritArrayOp.加速?80:op==SpiritArrayOp.生成灵弹?18:36;
            float radius=op==SpiritArrayOp.生成灵弹?.42f:op==SpiritArrayOp.分裂?.7f:.96f;
            for(int j=0;j<count;j++){float a=j*Mathf.PI*2/count;float size=j%9==0?.028f:j%3==0?.014f:.006f;g.Dot(Polar(a,radius+.025f*Mathf.Sin(j*1.7f),Mathf.Sin(a*3+kind)*.09f),size,j%9==0?1:.25f+(j%5)*.12f);}
        }else if(op==SpiritArrayOp.生成灵弹){
            // 小型发光阵眼和向外发散的短芒，没有外围完整符阵。
            g.Ring(.18f,0,.012f);g.Ring(.24f,0,.004f);
            for(int j=0;j<14;j++){float a=j*Mathf.PI/7;g.Line(Polar(a,.3f),Polar(a,.48f+(j%3)*.05f),.006f,.65f);}
        }else if(op==SpiritArrayOp.追踪){
            // 只有薄轨与游动光点，中央留空。
            g.Ring(.92f,0,.005f);g.Ring(.98f,.07f,.003f,96,.3f,4.9f);
        }else if(op==SpiritArrayOp.分裂){
            // 三个分离的小仪轨，相互以细线连接，不画大外圆或中心星阵。
            for(int j=0;j<3;j++){float a=j*Mathf.PI*2/3;var p=Polar(a,.55f,Mathf.Sin(a)*.1f);g.SmallCircle(p,.15f);g.Line(Polar(a,.35f),Polar(a+Mathf.PI*2/3,.35f),.004f,.35f);}
        }else if(op==SpiritArrayOp.强化){
            g.Ring(.9f,0,.008f);g.Ring(1,0,.008f);
            for(int j=0;j<32;j++){float a=j*Mathf.PI/16;g.Line(Polar(a,.91f),Polar(a,.98f),.005f,.8f);g.Line(Polar(a-.018f,.93f),Polar(a+.025f,.96f),.005f,.8f);}
        }else if(op==SpiritArrayOp.加速){
            // 短弧与粒子构成流轨，不加完整实线光圈。
            for(int j=0;j<7;j++)for(int k=0;k<14;k++){float a=j*Mathf.PI*2/7+k*.018f;g.Line(Polar(a,.88f,j%2*.08f),Polar(a+.018f,.88f,j%2*.08f),.008f,k/14f);}
        }else if(op==SpiritArrayOp.修正方向 || layer==2){
            // 空间折线：高度沿轨迹变化，保留中间大面积空白。
            for(int arm=0;arm<3;arm++){float start=arm*Mathf.PI*2/3;var previous=Polar(start,.5f,-.2f);for(int j=1;j<=7;j++){float a=start+j*.22f;var next=Polar(a,j%2==0?.82f:.64f,(j%3-1)*.22f);g.Line(previous,next,.005f,.65f);if(j%2==0)g.Dot(next,.019f,.8f);previous=next;}}
        }else{
            g.Ring(.98f,.14f,.006f);g.Ring(.62f,-.14f,.004f,96,.4f,5.5f);
        }
        return g.Mesh("SpiritArray_"+op+"_Layer"+layer);
    }
    // 原中心图案独立保存，可供后续有明确功能的中心节点复用，不默认塞入每种仪轨。
    public static Mesh Core(SpiritArrayOp op)
    {
        if(cores.TryGetValue(op,out var saved))return saved;
        var g=new Shape();int sides=op==SpiritArrayOp.分裂?3:6;
        for(int j=0;j<sides;j++)g.Line(Polar(j*Mathf.PI*2/sides,.58f),Polar((j+1)*Mathf.PI*2/sides,.58f),.006f,.8f);
        if(op==SpiritArrayOp.分裂){for(int j=0;j<3;j++)g.Line(Vector3.zero,Polar(j*Mathf.PI*2/3,.8f),.009f);}
        else if(op==SpiritArrayOp.生成灵弹){g.Ring(.36f,0,.012f);for(int j=0;j<12;j++)g.Line(Polar(j*Mathf.PI/6,.43f),Polar(j*Mathf.PI/6,.74f),.006f,.65f);}
        else if(op==SpiritArrayOp.强化||op==SpiritArrayOp.加速)g.Ring(.48f,0,.012f);
        else if(op==SpiritArrayOp.修正方向)g.Ring(.25f,0,.012f);
        saved=g.Mesh("Core_"+op);cores.Add(op,saved);return saved;
    }
    static Mesh pulse;
    public static Mesh Pulse{
        get{if(!pulse){var g=new Shape();g.Ring(.8f,0,.014f);for(int j=0;j<20;j++){float a=j*Mathf.PI/10;g.Line(Polar(a,.84f),Polar(a,1.05f),.009f,.8f);}pulse=g.Mesh("SpiritArrayActivationWave");}return pulse;}
    }
}
