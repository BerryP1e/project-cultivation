Shader "Cultivation/UI/PaperWeapon" {
 Properties { _MainTex("Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Opacity("Opacity",Float)=1 _Focus("Focus",Float)=0 }
 SubShader {Tags{"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite On Blend One OneMinusSrcAlpha
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float3 normal:NORMAL;};struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;};
 sampler2D _MainTex;float4 _MainTex_ST;fixed4 _Color;float _Opacity,_Focus;
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=TRANSFORM_TEX(i.uv,_MainTex);o.normal=UnityObjectToWorldNormal(i.normal);return o;}
 fixed4 frag(v i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv)*_Color;float light=.62+.38*abs(dot(normalize(i.normal),normalize(float3(-.4,.7,-.6))));c.rgb*=light*(1+_Focus*.12);c.a*=_Opacity;c.rgb*=c.a;return c;}
 ENDCG }
 }
}
