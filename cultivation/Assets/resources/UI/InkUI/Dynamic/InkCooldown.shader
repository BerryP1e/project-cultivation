Shader "UI/InkCooldown" {
 Properties { [PerRendererData] _MainTex("Icon",2D)="white"{} _Color("Color",Color)=(1,1,1,1) _Cooldown("Ink diffusion",Range(0,1))=0 }
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
 Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct v {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 sampler2D _MainTex;fixed4 _Color;float _Cooldown;
 v vert(a x){v o;o.vertex=UnityObjectToClipPos(x.vertex);o.uv=x.uv;o.color=x.color*_Color;return o;}
 fixed4 frag(v x):SV_Target {
 float k=saturate(_Cooldown),radius=.075*k;
 float2 drift=float2(sin(x.uv.y*36),cos(x.uv.x*31))*.014*k;
 float2 uv=x.uv+drift;fixed4 c=tex2D(_MainTex,uv)*.24;
 c+=tex2D(_MainTex,uv+float2(radius,0))*.12;c+=tex2D(_MainTex,uv-float2(radius,0))*.12;
 c+=tex2D(_MainTex,uv+float2(0,radius))*.12;c+=tex2D(_MainTex,uv-float2(0,radius))*.12;
 c+=tex2D(_MainTex,uv+float2(radius,radius))*.07;c+=tex2D(_MainTex,uv-float2(radius,radius))*.07;
 c+=tex2D(_MainTex,uv+float2(radius,-radius))*.07;c+=tex2D(_MainTex,uv-float2(radius,-radius))*.07;
 c.rgb=lerp(c.rgb,float3(.18,.24,.22),k*.78);c.a*=lerp(1,.62,k);
 return c*x.color;
 }
 ENDCG
 }
 }
}
