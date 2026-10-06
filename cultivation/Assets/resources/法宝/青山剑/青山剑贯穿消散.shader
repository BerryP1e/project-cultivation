Shader "Cultivation/QingshanSwordCut"
{
 Properties {
  _MainTex("Albedo",2D)="white"{} _Color("Color",Color)=(1,1,1,1)
  _Metallic("Metallic",Range(0,1))=0 _Glossiness("Smoothness",Range(0,1))=.5
  _CutHeight("Cut height",Float)=-10000 _CutEnabled("Cut enabled",Float)=0
 }
 SubShader {
  Tags { "RenderType"="Opaque" } LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  sampler2D _MainTex;fixed4 _Color;half _Metallic,_Glossiness;float _CutHeight,_CutEnabled;
  struct Input { float2 uv_MainTex;float3 worldPos; };
  void surf(Input IN,inout SurfaceOutputStandard o){
   float edge=IN.worldPos.y-_CutHeight;if(_CutEnabled>.5)clip(edge);
   fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=c.a;o.Metallic=_Metallic;o.Smoothness=_Glossiness;
   o.Emission=_CutEnabled>.5?fixed3(.2,.65,1)*pow(saturate(1-edge/.18),2)*2:0;
  }
  ENDCG
 }
 Fallback "Standard"
}
