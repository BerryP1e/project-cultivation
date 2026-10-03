// 水墨淡出（遮挡透明用）：物体不是"变半透明"，而是像墨被水化开一样**边缘先散、最后留一层淡墨剪影**。
//
// 用在哪：`遮挡素材.cs` 在相机→主角被挡住时，把建筑/树冠的渲染器换成本 shader 的副本，
// 每帧用 MaterialPropertyBlock 推进 `_Fade`（0 = 实心，1 = 只剩剪影）。
//
// ============================================================
// 三件事凑出"墨"的味道（缺一件就还是塑料半透明）
// ============================================================
//  ① 淡出：`_Fade` 把不透明度从 1 收到 `_Ghost`（剪影）—— 不是消失，玩家仍知道那儿有东西
//  ② 墨蚀：世界空间**三平面**程序化噪声做阈值侵蚀（三平面是为了墙上不出现竖条拉伸；
//     用模型 UV 会被图集/光照贴图 UV 带偏）。边缘带压成墨色 = "边缘墨散"
//  ③ 墨感：去饱和 + 推向纸色 + 叠宣纸纸纹（复用 `宣纸纹理_纸纹` 那张归一化颗粒图）
//
// ============================================================
// 为什么不是"切开就完事"（两个必须记着的坑）
// ============================================================
//  · **Queue = Transparent + ZWrite On + Blend**：不透明物体淡出必然进透明队列，
//    ZWrite 一关，树叶/墙体自己穿插的排序就炸。这里保持 ZWrite On，
//    靠 `clip(alpha - 0.01)` 让"已经被侵蚀掉的像素"**不写深度**（否则空洞会挡住后面的东西）。
//  · **必须自带 ShadowCaster**：没有这个 pass，材质一换**影子立刻消失**（比物体淡出还显眼）。
//    这里影子里也做了同一套侵蚀，所以影子是**跟着一起化掉**的。
//
// ⚠️ Properties 块按"只能有 ASCII"写（中文属性名/中文注释会让 ShaderLab 解析崩且报错行号骗人，
//    见 docs/ai/踩坑总库.md）。SubShader/CGPROGRAM 里的中文注释是安全的。

Shader "Cultivation/InkDissolve"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour", Color) = (1,1,1,1)

        _Fade ("Fade (0 = solid, 1 = ghost only)", Range(0,1)) = 0
        _Ghost ("Ghost Alpha at full fade", Range(0,1)) = 0.28

        _Dissolve ("Dissolve Amount", Range(0,1)) = 0.55
        _DissolveScale ("Dissolve Scale", Range(0.05,3)) = 0.4
        _EdgeWidth ("Ink Edge Width", Range(0.01,0.5)) = 0.16
        _EdgeInk ("Ink Edge Strength", Range(0,1)) = 0.85
        _InkColor ("Ink Colour", Color) = (0.06,0.06,0.07,1)

        _PaperColor ("Paper Colour", Color) = (0.93,0.92,0.90,1)
        _Wash ("Wash to paper", Range(0,1)) = 0.6
        _PaperTex ("Paper Grain", 2D) = "white" {}
        _PaperTiling ("Paper Tiling", Range(0.05,4)) = 0.35
        _PaperAmount ("Paper Amount", Range(0,1)) = 0.35
        _PaperGain ("Paper Gain", Range(0.2,20)) = 2
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }

        Pass
        {
            Name "InkForward"
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            float _Fade;
            float _Ghost;
            float _Dissolve;
            float _DissolveScale;
            float _EdgeWidth;
            float _EdgeInk;
            fixed4 _InkColor;

            fixed4 _PaperColor;
            float _Wash;
            sampler2D _PaperTex;
            float _PaperTiling;
            float _PaperAmount;
            float _PaperGain;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 wnrm : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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

            /// 三平面程序化墨蚀：低频团块（像水渍扩散）+ 一点细纹（像纸纤维咬边）
            float InkNoise(float3 p)
            {
                float3 n = abs(normalize(p + 0.0001));
                n /= max(n.x + n.y + n.z, 0.0001);

                float2 yz = p.yz * _DissolveScale;
                float2 xz = p.xz * _DissolveScale;
                float2 xy = p.xy * _DissolveScale;

                float lo = n.x * ValueNoise(yz * 1.7)
                         + n.y * ValueNoise(xz * 1.7)
                         + n.z * ValueNoise(xy * 1.7);
                float hi = n.x * ValueNoise(yz * 7.0)
                         + n.y * ValueNoise(xz * 7.0)
                         + n.z * ValueNoise(xy * 7.0);

                return saturate(lo * 0.78 + hi * 0.22);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float f = saturate(_Fade);

                // ---- 底色 + 一点自己的光照（没有法线贴图/光照贴图，淡出期间够用）----
                float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float3 n = normalize(i.wnrm);
                float3 amb = ShadeSH9(float4(n, 1));
                float3 ldir = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = saturate(dot(n, ldir));
                float3 lit = albedo * (amb + _LightColor0.rgb * ndl);

                // ---- ① 墨感：去饱和 + 向纸色偏一点，但**保留它自己的明暗** ----
                // ⚠️ 一开始写成"整体提亮到纸色"，结果暗场景里淡出的柱子亮得像块白板，
                //    比周围亮一大截、非常跳。改成"保留亮度结构、只去饱和 + 25% 纸色 + 轻微提亮"。
                float lum = dot(lit, float3(0.299, 0.587, 0.114));
                float3 washed = lum.xxx;
                washed = lerp(washed, _PaperColor.rgb, 0.25);
                washed *= 1.15;
                float3 col = lerp(lit, washed, f);

                // ---- ② 墨蚀：阈值侵蚀 + 边缘墨散 ----
                float noise = InkNoise(i.wpos);
                float th = _Dissolve * f;
                float edge = 1.0 - saturate((noise - th) / max(_EdgeWidth, 0.001));
                edge *= f;                                   // 只在淡出时出现

                col = lerp(col, _InkColor.rgb, saturate(edge * _EdgeInk));

                // ---- ③ 不透明度：整体渐隐到剪影；墨边保留得更实 ----
                float a = lerp(1.0, _Ghost, smoothstep(0.0, 1.0, f));
                a = max(a, edge * _EdgeInk * 0.9);

                // ---- 纸纹（复用宣纸的归一化颗粒图，世界空间铺）----
                float3 grain = 1.0 + (tex2D(_PaperTex, i.wpos.xz * _PaperTiling).r - 0.5) * _PaperGain;
                col *= lerp(1.0, grain, _PaperAmount * f);

                // ⚠️ 被侵蚀掉的像素 alpha=0：**不能写深度**，否则空洞会挡住它后面的东西
                clip(a - 0.01);

                fixed4 o = fixed4(saturate(col), saturate(a));
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }

        // 影子里也做同一套侵蚀 —— 否则材质一换影子当场消失，比物体淡出还显眼
        Pass
        {
            Name "InkShadow"
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            float _Fade;
            float _Dissolve;
            float _DissolveScale;
            float _EdgeWidth;

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float3 wpos : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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

            float InkNoise(float3 p)
            {
                float3 n = abs(normalize(p + 0.0001));
                n /= max(n.x + n.y + n.z, 0.0001);
                float2 yz = p.yz * _DissolveScale;
                float2 xz = p.xz * _DissolveScale;
                float2 xy = p.xy * _DissolveScale;
                float lo = n.x * ValueNoise(yz * 1.7) + n.y * ValueNoise(xz * 1.7) + n.z * ValueNoise(xy * 1.7);
                float hi = n.x * ValueNoise(yz * 7.0) + n.y * ValueNoise(xz * 7.0) + n.z * ValueNoise(xy * 7.0);
                return saturate(lo * 0.78 + hi * 0.22);
            }

            float4 frag(v2f i) : SV_Target
            {
                float f = saturate(_Fade);
                float th = _Dissolve * f;
                float noise = InkNoise(i.wpos);
                clip(noise - th - 0.001);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback Off
}
