// 辉光（Bloom）—— Built-in 渲染管线。
//
// 用法：由 GameGlobalBloom 组件在 OnRenderImage 里驱动，不单独挂。
//
// 为什么需要它：动漫感的"亮部发光"几乎全靠辉光 ——
// 法术、灯火、描边、金属高光，有辉光才有"发光"的感觉，
// 没有就只是一块块死白的色斑。
//
// ============================================================
// 【踩过的坑·2026-10-01】ShaderLab 的 Properties 块**只能写 ASCII**！
//
//   实测：Properties 块里出现**中文字符**（连注释里的中文都算），
//   就会报 "Unexpected directive '\n'"，而且**报错行号是"块内偏移"不是文件行号**，
//   所以它会指到一个完全无辜的位置，极难查。
//   同类坑还有：属性**显示名**里写中文、注释里用反引号包"像属性声明"的文本。
//
//   ⇒ 规矩：**Properties 块内一律 ASCII**，中文只写在 SubShader 里或文件头。
// ============================================================

Shader "Cultivation/Bloom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold   ("Threshold", Range(0, 4)) = 0.85
        _SoftKnee    ("Soft Knee", Range(0, 1)) = 0.5
        _Intensity   ("Intensity", Range(0, 4)) = 0.9
        _BlurRadius  ("Blur Radius", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // ============================================================
        // Pass 0 —— 亮部提取（Prefilter）
        // 只把超过阈值的部分取出来，阈值附近用一个"软膝"平滑过渡，
        // 避免硬阈值造成闪烁的边界（这是 bloom 最常见的瑕疵）。
        // 同时负责降采样：目标比源小，配合 bilinear 就等于一次廉价模糊。
        // ============================================================
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Threshold;
            float _SoftKnee;

            half4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float  br = max(c.r, max(c.g, c.b));

                // 软膝：threshold 附近过渡，而不是硬切
                float knee = max(_Threshold * _SoftKnee, 1e-4);
                float soft = clamp(br - _Threshold + knee, 0.0, 2.0 * knee);
                soft = soft * soft / (4.0 * knee);
                float contrib = max(soft, br - _Threshold) / max(br, 1e-4);

                return half4(c * contrib, 1.0);
            }
            ENDCG
        }

        // ============================================================
        // Pass 1 —— 可分离高斯模糊（一次一个方向）
        // C# 显式传入 _BlurDirection；横纵使用同一层的实际 TexelSize。
        // 9 抽头，权重和为 1，所以不会改变整体亮度。
        // ============================================================
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurRadius;
            float2 _BlurDirection;

            // 9 抽头高斯权重（和为 1，所以不改变整体亮度）
            #define W0 0.227027
            #define W1 0.1945946
            #define W2 0.1216216
            #define W3 0.054054
            #define W4 0.016216

            half4 frag(v2f_img i) : SV_Target
            {
                float2 dir = _BlurDirection * _MainTex_TexelSize.xy * _BlurRadius;

                float3 sum = tex2D(_MainTex, i.uv).rgb * W0;
                sum += (tex2D(_MainTex, i.uv + dir).rgb      + tex2D(_MainTex, i.uv - dir).rgb)      * W1;
                sum += (tex2D(_MainTex, i.uv + dir * 2.0).rgb + tex2D(_MainTex, i.uv - dir * 2.0).rgb) * W2;
                sum += (tex2D(_MainTex, i.uv + dir * 3.0).rgb + tex2D(_MainTex, i.uv - dir * 3.0).rgb) * W3;
                sum += (tex2D(_MainTex, i.uv + dir * 4.0).rgb + tex2D(_MainTex, i.uv - dir * 4.0).rgb) * W4;
                return half4(sum, 1.0);
            }
            ENDCG
        }

        // ============================================================
        // Pass 2 —— 在当前级分辨率合成两张独立输入，归一化亮度。
        // 不读取目标的旧内容；不能把所有级都画进最小的 RT。
        // ============================================================
        Pass
        {

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Source;
            sampler2D _MainTex;

            half4 frag(v2f_img i) : SV_Target
            {
                return half4((tex2D(_MainTex,i.uv).rgb+tex2D(_Source,i.uv).rgb)*.5,1);
            }
            ENDCG
        }

        // ============================================================
        // Pass 3 —— 把辉光叠回原图（加法）
        // ============================================================
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;      // 原图
            sampler2D _BloomTex;     // 累积好的辉光
            float _Intensity;

            half4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float3 b = tex2D(_BloomTex, i.uv).rgb;
                return half4(c + b * _Intensity, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
