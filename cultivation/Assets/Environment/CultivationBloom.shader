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

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float  br = max(c.r, max(c.g, c.b));

                // 软膝：threshold 附近过渡，而不是硬切
                float knee = max(_Threshold * _SoftKnee, 1e-4);
                float soft = clamp(br - _Threshold + knee, 0.0, 2.0 * knee);
                soft = soft * soft / (4.0 * knee);
                float contrib = max(soft, br - _Threshold) / max(br, 1e-4);

                return fixed4(c * contrib, 1.0);
            }
            ENDCG
        }

        // ============================================================
        // Pass 1 —— 可分离高斯模糊（一次一个方向）
        // 用 _MainTex_TexelSize 判断当前是横还是纵：
        // 横模糊时 TexelSize.x > TexelSize.y。
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

            // 9 抽头高斯权重（和为 1，所以不改变整体亮度）
            #define W0 0.227027
            #define W1 0.1945946
            #define W2 0.1216216
            #define W3 0.054054
            #define W4 0.016216

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 dir = (_MainTex_TexelSize.x > _MainTex_TexelSize.y)
                           ? float2(_MainTex_TexelSize.x * _BlurRadius, 0)
                           : float2(0, _MainTex_TexelSize.y * _BlurRadius);

                float3 sum = tex2D(_MainTex, i.uv).rgb * W0;
                sum += (tex2D(_MainTex, i.uv + dir).rgb      + tex2D(_MainTex, i.uv - dir).rgb)      * W1;
                sum += (tex2D(_MainTex, i.uv + dir * 2.0).rgb + tex2D(_MainTex, i.uv - dir * 2.0).rgb) * W2;
                sum += (tex2D(_MainTex, i.uv + dir * 3.0).rgb + tex2D(_MainTex, i.uv - dir * 3.0).rgb) * W3;
                sum += (tex2D(_MainTex, i.uv + dir * 4.0).rgb + tex2D(_MainTex, i.uv - dir * 4.0).rgb) * W4;
                return fixed4(sum, 1.0);
            }
            ENDCG
        }

        // ============================================================
        // Pass 2 —— 升采样并**加算**到累积纹理
        //
        // ⚠️ 这里刻意**只输出采样值**，加法交给 Blend One One：
        //    因为升采样是"把小的加到大的上"，而 _MainTex 和渲染目标
        //    是**同一张纹理**（不同 mip 其实是不同 RT，但写法上很容易踩到
        //    "读自己写自己" → 未定义行为）。交给 blend 就完全避开了这个问题。
        //    这是 Unity PostProcessing 栈里 Upsample 的标准做法。
        // ============================================================
        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Source;
            float _Intensity;

            fixed4 frag(v2f_img i) : SV_Target
            {
                return fixed4(tex2D(_Source, i.uv).rgb * _Intensity, 1.0);
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

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float3 b = tex2D(_BloomTex, i.uv).rgb;
                return fixed4(c + b * _Intensity, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
