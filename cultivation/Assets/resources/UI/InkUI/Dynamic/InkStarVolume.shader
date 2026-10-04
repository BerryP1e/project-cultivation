Shader "UI/InkStarVolume" {
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" }
 Cull Off ZWrite On Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a {float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;};
 struct v {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;fixed4 color:COLOR;};
 v vert(a x){v o;o.vertex=UnityObjectToClipPos(x.vertex);o.normal=UnityObjectToWorldNormal(x.normal);o.color=x.color;return o;}
 fixed4 frag(v x):SV_Target {float light=.4+.6*abs(dot(normalize(x.normal),normalize(float3(-.3,.6,-1))));return fixed4(x.color.rgb*light,x.color.a);}
 ENDCG
 }
 }
}
