Shader "Cultivation/Combat/VfxPaletteAdditive"
{
    Properties
    {
        _MainTex("Original Texture",2D)="white"{}
        _TintColor("Original Tint",Color)=(1,1,1,1)
        _TargetColor("Reference Hue",Color)=(.45,.65,1,1)
        _Intensity("Native Intensity",Float)=1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float4 _TintColor,_TargetColor; float _Intensity;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float4 color=tex2D(_MainTex,i.uv)*i.color*_TintColor;
                float value=max(color.r,max(color.g,color.b));
                float low=min(color.r,min(color.g,color.b));
                float strength=smoothstep(.025,.12,(value-low)/max(.0001,value));
                float3 hue=_TargetColor.rgb/max(.0001,max(_TargetColor.r,max(_TargetColor.g,_TargetColor.b)));
                color.rgb=lerp(color.rgb,hue*value,strength)*_Intensity;
                return color;
            }
            ENDCG
        }
    }
}
