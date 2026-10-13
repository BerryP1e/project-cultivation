Shader "Cultivation/FlightDodgeGhost"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Tint", Color) = (.45,.85,1,.3) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float rim:TEXCOORD1; fixed4 color:COLOR; };
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.rim=pow(1-saturate(abs(dot(UnityObjectToWorldNormal(v.normal),normalize(_WorldSpaceCameraPos-world)))),2);
                o.color=v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 tex=tex2D(_MainTex,i.uv);
                return fixed4(lerp(tex.rgb,_Color.rgb,.8)*(1+i.rim*.45)*i.color.rgb, tex.a*_Color.a*i.color.a*(.4+.6*i.rim));
            }
            ENDCG
        }
    }
}
