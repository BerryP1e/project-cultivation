Shader "Hidden/Cultivation/PaperOrbitBlur" {
 Properties{_MainTex("Image",2D)="black"{} _Radius("Radius",Float)=1.5}
 SubShader{Cull Off ZWrite Off ZTest Always Pass{CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;float _Radius;
 fixed4 frag(v2f_img i):SV_Target{float2 d=_MainTex_TexelSize.xy*_Radius;fixed4 c=tex2D(_MainTex,i.uv)*.28;c+=(tex2D(_MainTex,i.uv+float2(d.x,0))+tex2D(_MainTex,i.uv-float2(d.x,0))+tex2D(_MainTex,i.uv+float2(0,d.y))+tex2D(_MainTex,i.uv-float2(0,d.y)))*.12;c+=(tex2D(_MainTex,i.uv+d)+tex2D(_MainTex,i.uv-d)+tex2D(_MainTex,i.uv+float2(d.x,-d.y))+tex2D(_MainTex,i.uv+float2(-d.x,d.y)))*.06;return c;}
 ENDCG}}
}
