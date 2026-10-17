Shader "Cultivation/WorldAuthoring/CloudSea"
{
    Properties
    {
        _LandMask("Land occupancy",2D)="black"{}
        _Colour("Cloud light",Color)=(.72,.79,.83,.85)
        _Shade("Cloud shade",Color)=(.36,.47,.56,1)
        _MapRect("Map XZ bounds",Vector)=(-32,-32,.015625,.015625)
    }
    SubShader
    {
        Tags {"Queue"="Transparent-20" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _LandMask;
            float4 _Colour,_Shade,_MapRect;
            struct Input{float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Output{float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);Output o;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.world.y+=sin(o.world.x*.12+_Time.y*.14)*.20+cos(o.world.z*.15-_Time.y*.11)*.18;
                o.position=mul(UNITY_MATRIX_VP,float4(o.world,1));o.normal=UnityObjectToWorldNormal(v.normal);return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float2 uv=(i.world.xz-_MapRect.xy)*_MapRect.zw;
                float inBounds=step(0,uv.x)*step(0,uv.y)*step(uv.x,1)*step(uv.y,1);
                float land=tex2D(_LandMask,uv).r*inBounds;
                float voidMask=1-smoothstep(.28,.55,land);
                float2 p=i.world.xz*.10+float2(_Time.y*.016,-_Time.y*.012);
                float body=.5+.25*sin(p.x+sin(p.y*1.7))+ .18*cos(p.y*1.3+sin(p.x*.7));
                float light=saturate(normalize(i.normal).y*.55+body*.50);
                return fixed4(lerp(_Shade.rgb,_Colour.rgb,light),_Colour.a*voidMask*(.50+body*.45));
            }
            ENDCG
        }
    }
    Fallback Off
}
