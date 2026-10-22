Shader "Cultivation/UI/PaperOrbitRing" {
 Properties{_Tint("Tint",Color)=(1,1,1,1) _Opacity("Depth opacity",Float)=1 _Selected("Selected",Float)=0 _Focus("Category focus",Float)=0}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"}Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a{float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;};struct v{float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;fixed4 color:COLOR;};fixed4 _Tint;float _Opacity,_Selected,_Focus;
 v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.normal=UnityObjectToWorldNormal(i.normal);o.color=i.color;return o;}
 fixed4 frag(v i):SV_Target{float facing=abs(dot(normalize(i.normal),normalize(float3(-.3,.6,-1))));float light=.4+.6*facing;float3 color=lerp(i.color.rgb*_Tint.rgb*light,_Tint.rgb*(.65+light*.45),_Focus*.75);color+=pow(facing,12)*_Focus*.16;return fixed4(lerp(color,float3(.83,.66,.26),_Selected*.75),i.color.a*_Opacity);}
 ENDCG}
 }
}
