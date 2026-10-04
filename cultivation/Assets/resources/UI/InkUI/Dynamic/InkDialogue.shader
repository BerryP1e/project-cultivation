Shader "UI/InkDialogue" {
 Properties {
  [PerRendererData] _MainTex("Texture",2D)="white"{} _Color("Color",Color)=(1,1,1,1)
  _RectSize("Size",Vector)=(1000,240,0,0)
  _ClipLocal("Local viewport",Vector)=(-32767,-32767,32767,32767)
  _ClipRect("Clip rectangle",Vector)=(-32767,-32767,32767,32767)
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha Clip",Float)=0
 }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
 Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
 Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
 #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
 #include "UnityCG.cginc"
 #include "UnityUI.cginc"
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float4 world:TEXCOORD1;};
 fixed4 _Color;float4 _RectSize,_ClipRect,_ClipLocal;
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 v vert(a x){v o;o.world=x.vertex;o.vertex=UnityObjectToClipPos(x.vertex);o.uv=x.uv;o.color=x.color*_Color;return o;}
 fixed4 frag(v i):SV_Target {
  float2 size=max(_RectSize.xy,1),p=(i.uv-.5)*size;
  float2 q=abs(p)-(size*.5-float2(38,18))+18;
  float sd=length(max(q,0))+min(max(q.x,q.y),0)-18;
  float n=noise(p*.034),fine=noise(p*.16);
  float alpha=1-smoothstep(-12,17,sd+(n-.5)*24+(fine-.5)*6);
  fixed4 c=i.color;c.rgb*=.72+.28*noise(p*.025);c.a*=alpha;
  float edge=min(min(p.x-_ClipLocal.x,_ClipLocal.z-p.x),min(p.y-_ClipLocal.y,_ClipLocal.w-p.y));
  c.a*=smoothstep(0,8+fine*5,edge);
  #ifdef UNITY_UI_CLIP_RECT
  c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
  #endif
  #ifdef UNITY_UI_ALPHACLIP
  clip(c.a-.001);
  #endif
  return c;
 }
 ENDCG
 }
 }
}
