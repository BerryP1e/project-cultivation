Shader "Cultivation/WorldAuthoring/ShoreMistBuiltin"
{
    Properties{_MainTex("Scene",2D)="white"{}}
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _MainTex,_ShoreField;sampler3D _NoiseVolume;
            float4 _MainTex_TexelSize;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float WorldMistSampleNoise(float3 p){return tex3Dlod(_NoiseVolume,float4(p,0)).r;}
            float2 WorldMistSampleField(float2 uv){return tex2Dlod(_ShoreField,float4(uv,0,0)).rg;}
            #include "WorldMistVolume.hlsl"
            half4 frag(v2f_img input):SV_Target
            {
                float2 uv=input.uv,depthUV=uv;
                float depth=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,depthUV);
                #if !UNITY_REVERSED_Z
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                // CommandBuffer.Blit 已统一颜色与深度的屏幕 UV；不能再按临时颜色 RT 翻转一次。
                float4 world=mul(_MistInverseViewProjection,float4(uv*2-1,depth,1));
                return WorldMistComposite(tex2D(_MainTex,uv),world.xyz/world.w,uv*_ScreenParams.xy);
            }
            ENDCG
        }
    }
}
