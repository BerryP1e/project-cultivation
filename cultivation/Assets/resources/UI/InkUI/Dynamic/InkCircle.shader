Shader "UI/InkCircle" {
 Properties { [PerRendererData] _MainTex("Ink alpha",2D)="white"{} _Color("Color",Color)=(1,1,1,1) }
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
 Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 sampler2D _MainTex; fixed4 _Color;
 v vert(a x){v o;o.vertex=UnityObjectToClipPos(x.vertex);o.uv=x.uv;o.color=x.color*_Color;return o;}
 fixed4 frag(v x):SV_Target {fixed alpha=tex2D(_MainTex,x.uv).a;return fixed4(x.color.rgb,alpha*x.color.a);}
 ENDCG
 }
 }
}
