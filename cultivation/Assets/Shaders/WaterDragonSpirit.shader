Shader "Cultivation/WaterDragonSpirit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Spirit tint", Color) = (.7,.91,1,.88)
        _RimColor ("Rim", Color) = (.34,.86,1,1)
        _RimStrength ("Rim strength", Float) = 1.2
        _RimPower ("Rim power", Float) = 2.8
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _Visibility ("Visibility", Range(0,1)) = 1
        _NoiseScale ("Dissolve scale", Float) = 5
        _EdgeWidth ("Dissolve edge", Float) = .065
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert alpha:fade
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _RimColor;
        float _RimStrength, _RimPower, _Dissolve, _Visibility, _NoiseScale, _EdgeWidth;
        struct Input { float2 uv_MainTex; float3 worldPos; float3 viewDir; };
        float hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))) * 43758.5453); }
        float noise(float3 p)
        {
            float3 i = floor(p), f = frac(p); f = f*f*(3-2*f);
            return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                        lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);
        }
        void surf(Input IN, inout SurfaceOutput o)
        {
            float3 local = mul(unity_WorldToObject, float4(IN.worldPos,1)).xyz;
            float field = lerp(.015,.985,noise(local*_NoiseScale));
            clip(field-_Dissolve);
            fixed4 tex = tex2D(_MainTex,IN.uv_MainTex);
            float rim = pow(1-saturate(dot(normalize(IN.viewDir),o.Normal)),_RimPower);
            float edge = (1-smoothstep(0,_EdgeWidth,field-_Dissolve))*step(.001,_Dissolve);
            o.Albedo = tex.rgb*_Color.rgb;
            o.Emission = _RimColor.rgb*(rim*_RimStrength+edge*2.2);
            o.Alpha = tex.a*_Color.a*(.92+.08*rim)*_Visibility;
        }
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}
