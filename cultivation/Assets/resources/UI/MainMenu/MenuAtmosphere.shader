Shader "Cultivation/UI/MenuAtmosphere"
{
 Properties {
  [PerRendererData] _MainTex("Texture",2D)="white" {}
  _Color("Tint",Color)=(1,1,1,1)
  _Mode("Mode",Float)=0
  _Clock("Unscaled clock",Float)=0
  _Strength("Strength",Float)=1
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   sampler2D _MainTex; float4 _Color; float _Mode,_Clock,_Strength;
   float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   float noise(float2 p) {
    float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
   }
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
   fixed4 frag(v2f i):SV_Target {
    float2 uv=i.uv; float t=_Clock;
    if(_Mode>6.5) {
     fixed4 seal=tex2D(_MainTex,uv);
     float density=dot(seal.rgb,float3(.3,.59,.11));
     float grain=lerp(.78,1,noise(uv*320));
     float3 pigment=lerp(float3(.13,.17,.15),float3(.66,.68,.58),saturate(density*1.05));
     return float4(pigment*grain,seal.a*lerp(.72,1,noise(uv*85)))*i.color;
    }
    if(_Mode>4.5) {
     uv+=float2(sin(uv.y*13+t*.7),cos(uv.x*11-t*.55))*.008;
     fixed4 ink=tex2D(_MainTex,uv);
     float flow=.87+.13*sin(uv.x*8+uv.y*7-t*.6);
     return float4(i.color.rgb*flow,ink.a*i.color.a);
    }
    if(_Mode>3.5) {
     float light=exp(-dot((uv-float2(.57,.52))*float2(1.1,1.5),(uv-float2(.57,.52))*float2(1.1,1.5))*4);
     return float4(lerp(float3(.008,.013,.011),float3(.027,.044,.038),light),1)*i.color;
    }
    if(_Mode<.5) {
     // The head and feet stay anchored; hair, sleeve and hem have different wind phases.
     float hem=pow(saturate(1-uv.y),1.3)*smoothstep(.12,.3,uv.y); float hair=exp(-pow((uv.y-.72)*9,2));
     uv.x+=_Strength*(.085*hem*sin(t*1.9+uv.y*8)+.018*hair*sin(t*2.4+uv.y*15));
     uv.y+=.009*_Strength*hem*sin(t*1.5+uv.x*8);
     return tex2D(_MainTex,uv)*i.color;
    }
    if(_Mode<1.5) {
     uv+=float2(sin(uv.y*12+t*.4),cos(uv.x*9-t*.3))*.009*_Strength;
     fixed4 cloud=tex2D(_MainTex,uv)*i.color;
     cloud.a*=smoothstep(0,.09,uv.x)*smoothstep(0,.09,1-uv.x);
     return cloud;
    }
    float2 p=(uv-.5)*2; float r=length(p); float a=atan2(p.y,p.x);
    if(_Mode>2.5) {
     float ribbon=0;
     for(int k=0;k<3;k++) {
      float path=.78+sin(a*7+t*.34+k*2)*.025+sin(a*13-t*.61+k)*.012+k*.03;
      float delta=abs(r-path);
      float flow=pow(saturate(.5+.5*sin(a*3-t*.42+k*2)),4);
      ribbon+=(exp(-delta*300)*.95+exp(-delta*65)*.15)*flow;
     }
     return float4(i.color.rgb,ribbon*i.color.a);
    }
    float wet=noise(p*4+float2(t*.035,-t*.027));
    float fibre=noise(p*43)*.55+noise(p*109)*.45;
    float edge=1-smoothstep(.80+wet*.06,.96+wet*.04,r);
    float swirl=pow(saturate(.5+.5*sin(a*4+r*16-t*.30+wet*2.4+sin(a*3-r*6+t*.09)*.4)),2.6);
    float cloudy=noise(p*9+float2(t*.014,t*.021));
    float wash=swirl*(.45+.3*cloudy)*(.8+.2*fibre);
    float3 color=lerp(float3(.021,.034,.028),float3(.19,.225,.185),wash);
    return float4(color,edge)*i.color;
   }
   ENDCG
  }
 }
}
