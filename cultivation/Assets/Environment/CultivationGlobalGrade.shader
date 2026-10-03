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

        // --- Ink wash: ink value bands (mo fen wu se), paper white, paper grain ---
        _InkLevels   ("Ink Levels (0 = off)", Range(0, 9)) = 5
        _InkAmount   ("Ink Levels Amount", Range(0, 1)) = 0.35
        _PaperWhite  ("Paper White Colour", Color) = (0.96,0.95,0.94,1)
        _PaperAmount ("Paper White Amount", Range(0, 1)) = 0.25
        _PaperThr    ("Paper White Threshold", Range(0.5, 1)) = 0.86
        _GrainTex    ("Paper Grain Texture", 2D) = "white" {}
        _GrainAmount ("Paper Grain Amount", Range(0, 1)) = 0.30
        _GrainTiling ("Paper Grain Tiling", Range(0.5, 40)) = 3.0
        _GrainHasTex ("Paper Grain Has Texture", Range(0, 1)) = 0
        _GrainGain   ("Paper Grain Gain (high-pass)", Range(0.2, 40)) = 6.0
        _GrainPre    ("Paper Grain Pre-normalised", Range(0, 1)) = 1
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
            float  _InkLevels;
            float  _InkAmount;
            float4 _PaperWhite;
            float  _PaperAmount;
            float  _PaperThr;
            sampler2D _GrainTex;
            float4 _GrainTex_TexelSize;
            float  _GrainAmount;
            float  _GrainTiling;
            float  _GrainHasTex;
            float  _GrainGain;
            float  _GrainPre;

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 2-octave value noise. Used as the FALLBACK paper grain when no
            // grain texture is assigned, so the paper effect is never a black box
            // that silently does nothing.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
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

                // 5.5) Ink value bands ("mo fen wu se").
                // Ink on paper has only a few discrete values, so a picture made of
                // infinite smooth gradients reads as "3D render", not as ink.
                // We quantise LUMA to N bands and scale RGB by the ratio, so hue is
                // kept and only the value steps. _InkAmount blends between the
                // original and the banded value -- full 1.0 is a hard posterise,
                // which looks like a bug on 3D art, so the default is partial.
                if (_InkLevels > 0.5 && _InkAmount > 0.0001)
                {
                    float lum  = max(Luma(c), 0.0001);
                    float n    = max(_InkLevels, 2.0);
                    float band = floor(lum * n + 0.5) / n;
                    float target = lerp(lum, band, saturate(_InkAmount));
                    c *= target / lum;
                }

                // 5.6) Paper white ("liu bai"): the brightest areas become the paper
                // itself instead of a colour. This is what makes a sky or a bright
                // ground read as untouched paper.
                if (_PaperAmount > 0.0001)
                {
                    float w = smoothstep(_PaperThr, min(_PaperThr + 0.12, 1.0), Luma(c));
                    c = lerp(c, _PaperWhite.rgb, w * saturate(_PaperAmount));
                }

                // 5.7) Paper grain. Screen-space on purpose: real paper does not move
                // with the camera, and a screen-fixed grain also hides the fact that
                // the 3D surfaces underneath are flat.
                if (_GrainAmount > 0.0001)
                {
                    float2 g = i.uv * _GrainTiling;
                    float dev;   // 以 0 为中心的"颗粒偏离量"
                    if (_GrainHasTex > 0.5)
                    {
                        if (_GrainPre > 0.5)
                        {
                            // 已归一化的纸纹（`修仙/美术/宣纸纸纹预处理` 的产物）：
                            // 中点 0.5、±2σ ≈ ±0.25，直接取一次就够，不用再做高通。
                            dev = (Luma(tex2D(_GrainTex, g).rgb) - 0.5) * _GrainGain;
                        }
                        else
                        {
                            // 生图（直接拍的纸）：纸的"纹"是高频、但动态范围极小，必须先高通再放大。
                            // ⚠️ 不预处理就直接整张乘上去 = 只是整体变亮变暗 + 大块云斑变脏斑。
                            float2 dx = float2(_GrainTex_TexelSize.x, 0);
                            float2 dy = float2(0, _GrainTex_TexelSize.y);
                            float c0 = Luma(tex2D(_GrainTex, g).rgb);
                            float c1 = Luma(tex2D(_GrainTex, g + dx).rgb);
                            float c2 = Luma(tex2D(_GrainTex, g - dx).rgb);
                            float c3 = Luma(tex2D(_GrainTex, g + dy).rgb);
                            float c4 = Luma(tex2D(_GrainTex, g - dy).rgb);
                            dev = (c0 - (c1 + c2 + c3 + c4) * 0.25) * _GrainGain;
                        }
                    }
                    else
                    {
                        // 兜底：程序化纸纤维（没有贴图时也能出效果，不做静默失效的黑盒）
                        float n1 = ValueNoise(g * 6.0);
                        float n2 = ValueNoise(g * 23.0);
                        float fib = ValueNoise(float2(g.x * 60.0, g.y * 3.0));
                        dev = ((n1 * 0.55 + n2 * 0.25 + fib * 0.20) - 0.5) * _GrainGain * 0.2;
                    }
                    float grain = clamp(1.0 + dev, 0.2, 1.8);
                    // ⚠️ 2026-10-03 用户："为啥在摄像头前盖了一层宣纸纹理" —— 大面积**亮而平**的墙面上
                    //    纸纹会整片显出来，像贴了一张纸。真实纸纹只在**中间调**看得见（纸白与浓墨处看不见），
                    //    所以这里按亮度加权：0.5 处最强、趋近黑/白处归零。
                    //    ⚠️ 变量名必须 ASCII（写成中文标识符会报 "Unexpected directive"，而且行号骗人）
                    float lumNow = Luma(c);
                    float midWeight = 1.0 - abs(2.0 * lumNow - 1.0);
                    c *= lerp(float3(1, 1, 1), grain.xxx, saturate(_GrainAmount) * midWeight);
                }

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
