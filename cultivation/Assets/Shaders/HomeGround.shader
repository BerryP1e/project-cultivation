Shader "Cultivation/HomeGround"
{
    Properties
    {
        _GrassTex("村庄草地",2D)="white"{}
        _SoilTex("村庄土路",2D)="white"{}
        _RockTex("岩壁细节",2D)="white"{}
        _GrassScale("草地铺设尺寸",Float)=7
        _RockScale("岩壁铺设尺寸",Float)=3
        _Tint("草地颜色",Color)=(1,1,1,1)
        _RockTint("岩壁颜色",Color)=(0.55,0.56,0.50,1)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _GrassTex,_SoilTex,_RockTex;
        float _GrassScale,_RockScale;fixed4 _Tint,_RockTint;
        struct Input {float3 worldPos;float3 worldNormal;fixed4 color:COLOR;};
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float2 uv=IN.worldPos.xz/max(_GrassScale,.01);
            // 世界坐标连续铺设，扩建不会改变旧地块的纹理相位。
            fixed3 grass=tex2D(_GrassTex,uv).rgb*_Tint.rgb;
            fixed3 soil=tex2D(_SoilTex,IN.worldPos.xz/6).rgb;
            float variation=sin(IN.worldPos.x*.17+sin(IN.worldPos.z*.13))*sin(IN.worldPos.z*.21);
            grass=lerp(grass,soil,saturate(variation*.18+.03));
            float3 w=pow(abs(IN.worldNormal),4);w/=max(w.x+w.y+w.z,.001);
            float3 p=IN.worldPos/max(_RockScale,.01);
            fixed3 rock=(tex2D(_RockTex,p.zy).rgb*w.x+tex2D(_RockTex,p.xz).rgb*w.y+tex2D(_RockTex,p.xy).rgb*w.z)*_RockTint.rgb;
            o.Albedo=lerp(grass,rock,IN.color.r);o.Metallic=0;o.Smoothness=.05;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
