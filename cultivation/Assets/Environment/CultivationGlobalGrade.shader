// 全局调色后处理（Built-in 渲染管线）。
//
// 流程：Tonemap(高光滚降) → 曝光 → Lift/Gamma/Gain → 对比度 → 饱和度 → 色调染色 → 暗角
//
// 用法：挂到主相机上（GameGlobalGrade 组件），场景自举会自动补。
// 参数调好后**不需要重新导入资产**，改动立刻全场生效 —— 这是它最大的好处。
//
// ============================================================
// 【踩过的坑】ShaderLab 的 Properties 块**极其娇气**，三个坑都踩过：
//
//  坑1：属性**显示名**里不要写中文。写了会让解析崩，报 "Unexpected directive"，
//       而且**报错行号是错的**（会指到 Properties 块后面的无辜行）。
//
//  坑2：注释里不要出现**反引号**包起来的"看起来像属性声明"的文本。
//       解析器会被带偏，之后行号开始乱飘。
//
//  坑3（本条最坑，2026-10-01 实测定位）：**Properties 块内部不要出现中文字符**，
//       连注释里的中文都不行。前两条都改掉之后仍然报错，最后逐步定位到：
//       只要 Properties 块里有中文注释，就报 "Unexpected directive \n"，
//       且行号固定在"块内偏移"而不是文件行号，极难查。
//
//  ⇒ 结论：**Properties 块按"只能有 ASCII"来写。**
//     要中文说明就放在文件头（这里）或 SubShader 外面。
//     中文写在 SubShader/CGPROGRAM 的注释里是**可以的**（实测无问题）。
// ============================================================

Shader "Cultivation/GlobalGrade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        // --- Tonemap: soft highlight rolloff.
        // Without this the final saturate() hard-clips everything above 1,
        // so the 2~3 intensity fire lights blow out to flat white.
        _TonemapOn     ("Tonemap Enable", Range(0, 1)) = 1
        _TonemapAmount ("Tonemap Amount", Range(0, 1)) = 0.85
        // Hable white point: larger = stronger highlight compression. 11.2 = Uncharted2.
        _TonemapWhite  ("Tonemap White Point", Range(1, 30)) = 11.2

        // --- Exposure: >1 brightens. ---
        _Exposure ("Exposure", Range(0.2, 3)) = 1.12

        // --- Lift / Gamma / Gain ---
        _Gain   ("Gain (highlights)", Color) = (1,1,1,1)
        _Gamma  ("Gamma (midtones)",  Color) = (1,1,1,1)
        _Lift   ("Lift (shadows, additive)", Color) = (0.015,0.018,0.028,0)

        // --- Color ---
        _Contrast   ("Contrast", Range(0.5, 2)) = 1.06
        _Saturation ("Saturation", Range(0, 2)) = 1.10
        _Tint       ("Tint Colour", Color) = (1,0.97,0.92,1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.18

        // --- Vignette (0 = off) ---
        _Vignette     ("Vignette", Range(0, 1.5)) = 0.35
        _VignetteSoft ("Vignette Softness", Range(0.05, 1)) = 0.45
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float  _TonemapOn;
            float  _TonemapAmount;
            float  _TonemapWhite;
            float  _Exposure;
            float4 _Gain;
            float4 _Gamma;
            float4 _Lift;
            float  _Contrast;
            float  _Saturation;
            float4 _Tint;
            float  _TintAmount;
            float  _Vignette;
            float  _VignetteSoft;

            float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            // Hable / Uncharted2 filmic curve.
            // Chosen because the highlight rolloff is smooth and it works per-channel,
            // so bright areas drift toward white instead of turning grey.
            float3 HablePartial(float3 x)
            {
                const float A = 0.15, B = 0.50, C = 0.10, D = 0.20, E = 0.02, F = 0.30;
                return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
            }

            // Input is the gamma-encoded (0..1) render target.
            // Convert to linear, apply Hable, convert back -- Hable is defined in
            // linear space; applying it straight to gamma values over-hardens contrast.
            float3 TonemapHable(float3 cGamma, float whitePoint)
            {
                float3 lin  = pow(max(cGamma, 0.0), 2.2);
                float3 tone = HablePartial(lin * 2.0) / HablePartial(whitePoint);
                return pow(max(tone, 0.0), 1.0 / 2.2);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;

                // 0) Tonemap first: pull the over-range highlights back before grading.
                if (_TonemapOn > 0.5 && _TonemapAmount > 0.0001)
                {
                    float3 tone = TonemapHable(c, max(_TonemapWhite, 1.0));
                    c = lerp(c, tone, saturate(_TonemapAmount));
                }

                // 1) Exposure
                c *= _Exposure;

                // 2) Lift / Gamma / Gain
                c = c * _Gain.rgb + _Lift.rgb;
                c = pow(max(c, 0.0), 1.0 / max(_Gamma.rgb, 0.01));

                // 3) Contrast around mid grey (0.5)
                c = (c - 0.5) * _Contrast + 0.5;

                // 4) Saturation
                float l = Luma(c);
                c = lerp(float3(l, l, l), c, _Saturation);

                // 5) Tint
                c = lerp(c, c * _Tint.rgb, _TintAmount);

                // 6) Vignette, centred on screen
                if (_Vignette > 0.0001)
                {
                    float2 d = i.uv - 0.5;
                    float  r = length(d) * 1.41421356;      // corner = 1
                    float  v = smoothstep(1.0, _VignetteSoft, r);
                    c *= lerp(1.0, v, saturate(_Vignette));
                }

                return fixed4(saturate(c), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
