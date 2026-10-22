Shader "Cultivation/UI/PaperOrbitComposite" {
 Properties{[PerRendererData]_MainTex("Image",2D)="black"{} _Color("Tint",Color)=(1,1,1,1)}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"}Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend One OneMinusSrcAlpha
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};sampler2D _MainTex;fixed4 _Color;
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color*_Color;return o;}
 fixed4 frag(v i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv);c.rgb*=i.color.rgb*i.color.a;c.a*=i.color.a;return c;}
 ENDCG}
 }
}
