Shader "Cultivation/VoxelTerrainSurface"
{
    Properties
    {
        _Control ("Terrain blend",2D)="white" {}
        _Layer0 ("Layer 0",2D)="white" {} _Layer1 ("Layer 1",2D)="white" {}
        _Layer2 ("Layer 2",2D)="white" {} _Layer3 ("Layer 3",2D)="white" {}
        _Terrain ("Terrain origin and size",Vector)=(0,0,200,200)
        _Tile0 ("Tile 0",Vector)=(8,8,0,0) _Tile1 ("Tile 1",Vector)=(8,8,0,0)
        _Tile2 ("Tile 2",Vector)=(8,8,0,0) _Tile3 ("Tile 3",Vector)=(8,8,0,0)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _Control,_Layer0,_Layer1,_Layer2,_Layer3;
        float4 _Terrain,_Tile0,_Tile1,_Tile2,_Tile3;
        struct Input {float3 worldPos;float3 worldNormal;float4 color:COLOR;};
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float2 p=IN.worldPos.xz-_Terrain.xy;
            half4 w=tex2D(_Control,p/_Terrain.zw);w/=max(dot(w,half4(1,1,1,1)),.0001);
            half3 surface=tex2D(_Layer0,(p+_Tile0.zw)/_Tile0.xy).rgb*w.r
                         +tex2D(_Layer1,(p+_Tile1.zw)/_Tile1.xy).rgb*w.g
                         +tex2D(_Layer2,(p+_Tile2.zw)/_Tile2.xy).rgb*w.b
                         +tex2D(_Layer3,(p+_Tile3.zw)/_Tile3.xy).rgb*w.a;
            half3 weights=pow(abs(IN.worldNormal),4);weights/=max(weights.x+weights.y+weights.z,.0001);
            half3 soil=tex2D(_Layer3,IN.worldPos.yz/1.4).rgb*weights.x
                      +tex2D(_Layer3,IN.worldPos.xz/1.4).rgb*weights.y
                      +tex2D(_Layer3,IN.worldPos.xy/1.4).rgb*weights.z;
            soil*=half3(.75,.65,.53);
            o.Albedo=lerp(soil,surface,IN.color.a);o.Metallic=0;o.Smoothness=.02;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
