// 描边**合成**：拿 `[Cultivation/OutlineMask]` 那两张图（法线+深度 / 组 ID），
// 在**屏幕空间**沿 8 个方向 × 2 个半径采样，比较 深度 / 法线 / ID 的差值 ⇒ 画线。
//
// 为什么这条路比"外扩网格"（`CharacterOutlineRim` + `CharacterOutlineFill`）好：
//   · 线宽按**像素**算 ⇒ 角色在画面里变小也不会糊；
//   · 一次画完**所有**角色的轮廓，不用给每个角色复制网格（蒙皮网格外扩宽度本来就不均）；
//   · 想"只描玩家/只描敌人"只要改掩码那一趟画谁（见 `GameGlobalOutline`）。
//
// 阈值口径（和 URP 那个视频同一套思路）：
//   _DepthThr  深度**相对**差（绝对差在远处会误判，所以除以中心深度）
//   _NormalThr 法线夹角差（1 - dot；默认给得很松 ⇒ 只管轮廓，不管脸上那些面与面的折痕）
//   _IDThr     组 ID 差（超过就一定是两个不同组的东西交界 ⇒ 画线）
//   _Soft      边缘软化（抗锯齿）
//
// ⚠️ CGPROGRAM 里的标识符必须全 ASCII（HLSL 不认中文），中文只放注释里。
Shader "Cultivation/OutlineComposite"
{
    Properties
    {
        _MainTex ("场景颜色", 2D) = "white" {}
        _MaskTex ("法线+深度（OutlineMask.RT0）", 2D) = "black" {}
        _IDTex ("组 ID（OutlineMask.RT1）", 2D) = "black" {}

        _PlayerColor ("玩家线色", Color) = (1.0, 0.86, 0.45, 1)
        _OtherColor ("其他线色", Color) = (0.55, 0.75, 1.0, 1)
        _PlayerID ("玩家那一组的 ID", Range(0, 1)) = 0.25

        [Header(Sampling)]
        _Radius ("采样半径（像素 = 线粗细）", Range(0.4, 8)) = 1.6
        _Soft ("边缘软化（抗锯齿）", Range(0.01, 1)) = 0.45
        _Strength ("线的强度", Range(0, 1)) = 0.9

        [Header(Thresholds)]
        _DepthThr ("深度阈值（相对差）", Range(0.0005, 0.3)) = 0.02
        _NormalThr ("法线阈值（1-dot）", Range(0.01, 2)) = 0.85
        _IDThr ("组 ID 阈值", Range(0.001, 1)) = 0.02
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _IDTex;
            float4 _MaskTex_TexelSize;

            fixed4 _PlayerColor;
            fixed4 _OtherColor;
            float _PlayerID;
            float _Radius;
            float _Soft;
            float _Strength;
            float _DepthThr;
            float _NormalThr;
            float _IDThr;

            // 一个邻居给出的"这里算不算一条边"（0 = 不是，1 = 肯定是）
            // ⚠️ 方向必须写成**字面量**逐个调用，不能搞 `static const float2 DIRS[8]` + 循环索引 ——
            //    Cg 下"用循环变量索引静态数组"会让这个 shader 在运行时**直接不生效（整屏粉）**，
            //    而且编辑器里不报错（踩过：截图整屏品红，控制台一条 shader 错误都没有）。
            float EdgeAt(float2 uv, float4 c, float cid, float2 dir, float radius)
            {
                float2 off = dir * _MaskTex_TexelSize.xy * radius;
                float4 m  = tex2D(_MaskTex, uv + off);
                float  id = tex2D(_IDTex, uv + off).r;

                // 组不同 ⇒ 一定是两个东西的交界（背景 = 组 0，所以角色轮廓也走这条）
                float idEdge = (abs(id - cid) > _IDThr) ? 1.0 : 0.0;

                // 深度：用**相对**差 —— 远处物体的绝对差天生就大，直接比会满屏都是线
                float dRel  = abs(m.a - c.a) / max(c.a, 0.0005);
                float depEdge = saturate((dRel - _DepthThr) / max(_DepthThr, 0.0005));

                // 法线：默认阈值很松（0.85 ≈ 夹角 80°）⇒ 只管轮廓，不管面上折痕
                float3 n0 = normalize(c.rgb * 2 - 1);
                float3 n1 = normalize(m.rgb * 2 - 1);
                float nrmEdge = saturate((1.0 - saturate(dot(n0, n1)) - _NormalThr) / max(_NormalThr, 0.01));

                return max(idEdge, max(depEdge, nrmEdge));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                float4 cen = tex2D(_MaskTex, i.uv);
                float  cid = tex2D(_IDTex, i.uv).r;

                // 背景像素（组 0）不画线：线只贴在角色**内侧**，不然角色外一圈会糊在场景上
                if (cid <= 0.001) return col;

                float r1 = _Radius * 0.75;   // 内圈：给实
                float r2 = _Radius;          // 外圈：给柔（斜边不会有台阶）

                float e = 0;
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 1,  0), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2(-1,  0), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0,  1), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0, -1), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0.7071,  0.7071), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2(-0.7071,  0.7071), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0.7071, -0.7071), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2(-0.7071, -0.7071), r1));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 1,  0), r2));
                e = max(e, EdgeAt(i.uv, cen, cid, float2(-1,  0), r2));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0,  1), r2));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0, -1), r2));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0.7071,  0.7071), r2));
                e = max(e, EdgeAt(i.uv, cen, cid, float2(-0.7071,  0.7071), r2));
                e = max(e, EdgeAt(i.uv, cen, cid, float2( 0.7071, -0.7071), r2));
                e = max(e, EdgeAt(i.uv, cen, cid, float2(-0.7071, -0.7071), r2));

                // 软化：把"是/不是边"变成 0~1，抗锯齿
                float a = smoothstep(1.0 - _Soft, 1.0, e) * _Strength;
                if (a <= 0.001) return col;

                // 线色按**中心像素属于哪一组**选（玩家暖金、其他冷蓝）。⚠️ 变量别叫 line —— HLSL 保留字
                fixed4 edgeColor = (abs(cid - _PlayerID) < _IDThr) ? _PlayerColor : _OtherColor;
                col.rgb = lerp(col.rgb, edgeColor.rgb, a);
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
