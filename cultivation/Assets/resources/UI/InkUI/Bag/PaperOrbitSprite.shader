Shader "Cultivation/UI/PaperOrbitSprite" {
 Properties{[PerRendererData]_MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Opacity("Opacity",Float)=1 _Blur("Blur texels",Float)=0 _Portrait("Portrait fade",Float)=0}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 sampler2D _MainTex;float4 _MainTex_TexelSize;fixed4 _Color;float _Opacity,_Blur,_Portrait;
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color*_Color;return o;}
 fixed4 frag(v i):SV_Target{float2 d=_MainTex_TexelSize.xy*_Blur;fixed4 c=tex2D(_MainTex,i.uv)*.28;c+=(tex2D(_MainTex,i.uv+float2(d.x,0))+tex2D(_MainTex,i.uv-float2(d.x,0))+tex2D(_MainTex,i.uv+float2(0,d.y))+tex2D(_MainTex,i.uv-float2(0,d.y)))*.12;c+=(tex2D(_MainTex,i.uv+d)+tex2D(_MainTex,i.uv-d)+tex2D(_MainTex,i.uv+float2(d.x,-d.y))+tex2D(_MainTex,i.uv+float2(-d.x,d.y)))*.06;c*=i.color;c.a*=_Opacity*lerp(1,smoothstep(.02,.23,i.uv.y)*(1-smoothstep(.96,1,i.uv.y)),_Portrait);c.rgb*=c.a;return c;}
 ENDCG}
 }
}
