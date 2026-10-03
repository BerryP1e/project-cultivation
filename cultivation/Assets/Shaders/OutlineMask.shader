// 描边用的「掩码图」—— 一趟 MRT 同时写两张图：
//   RT0（ARGBHalf）：RGB = **相机空间法线**（编码到 0..1），A = **归一化线性深度**
//   RT1（ARGB32）  ：R = **对象组 ID**（0 = 背景，1 = 玩家，2 = 其他角色…）
//
// 这一趟由 `GameGlobalOutline` 用 `CommandBuffer.DrawRenderer(..., 本材质, 0, 0)` 逐个角色重画，
// 所以**不用**动模型（URP 那个视频靠"给 mesh 加一套 UV"区分物体，Built-in 下按 renderer 画就行）。
//
// ⚠️ CGPROGRAM 里的**标识符必须全 ASCII**：HLSL/Cg 不认中文标识符，
//    写成 `struct 输出` / `float 边权()` 会报 "Unexpected directive" 这种看不懂的错（踩过）。
//    中文只能出现在注释里。
Shader "Cultivation/OutlineMask"
{
    Properties
    {
        _ID ("对象组 ID（0=背景 1=玩家 2=其他角色）", Range(0, 1)) = 0
        _CamFar ("相机远裁面（把线性深度归一化用）", Float) = 100
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            float _ID;
            float _CamFar;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 nrm : TEXCOORD0;   // 世界空间法线
                float  dep : TEXCOORD1;   // 到相机的线性距离（正数）
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.dep = length(wp - _WorldSpaceCameraPos);
                return o;
            }

            struct MaskOut
            {
                half4 rt0 : SV_Target0;   // 法线 + 深度
                half4 rt1 : SV_Target1;   // 组 ID
            };

            MaskOut frag(v2f i)
            {
                MaskOut o;
                // 用**相机空间**法线：物体绕自身旋转时轮廓仍然算边缘（世界法线会把"转身"当成边缘）
                float3 nv = mul((float3x3)UNITY_MATRIX_V, normalize(i.nrm));
                o.rt0 = half4(nv * 0.5 + 0.5, saturate(i.dep / max(0.0001, _CamFar)));
                o.rt1 = half4(_ID, 0, 0, 1);
                return o;
            }
            ENDCG
        }
    }

    Fallback Off
}
