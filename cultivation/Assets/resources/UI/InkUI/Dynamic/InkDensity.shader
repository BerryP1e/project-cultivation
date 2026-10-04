Shader "Hidden/Cultivation/InkDensity"
{
    Properties
    {
        _MainTex ("Previous Density Velocity", 2D) = "black" {}
        _BaseTex ("Ink Shape", 2D) = "white" {}
        _Step ("Step", Float) = 0.033333
        _Clock ("Unscaled Clock", Float) = 0
        _Seed ("Seed", Float) = 0
        _Reset ("Reset", Float) = 0
        _Pointer ("Pointer UV Strength", Vector) = (-10,-10,0,0)
        _Drag ("Pointer Velocity", Vector) = (0,0,0,0)
        _Click ("Click UV Age Strength", Vector) = (0.5,0.5,1,0)
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
            sampler2D _MainTex,_BaseTex;
            float4 _MainTex_TexelSize,_Pointer,_Drag,_Click;
            float _Step,_Clock,_Seed,_Reset;
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 a=floor(p),b=frac(p); b=b*b*(3-2*b);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);
            }
            float field(float2 p) { return noise(p*3+_Seed+float2(_Clock*.18,_Clock*.13)); }
            float baseInk(float2 uv) {
                float4 c=tex2D(_BaseTex,uv);
                return c.a*(1-dot(c.rgb,float3(.2126,.7152,.0722))*.72);
            }
            float4 frag(v2f_img i):SV_Target {
                float2 uv=i.uv,texel=_MainTex_TexelSize.xy;
                float base=baseInk(uv);
                if(_Reset>.5) return float4(base,0,0,1);
                float4 old=tex2D(_MainTex,uv);
                float2 gradient=float2(field(uv+float2(.01,0))-field(uv-float2(.01,0)),field(uv+float2(0,.01))-field(uv-float2(0,.01)))/.02;
                float2 curl=float2(gradient.y,-gradient.x)*.018;
                float2 velocity=lerp(old.gb,curl,_Step*.8);
                float2 delta=uv-_Pointer.xy;
                float pointer=exp(-dot(delta,delta)/.009)*_Pointer.z;
                velocity+=clamp(_Drag.xy,-.8,.8)*pointer*_Step*2.5;
                float2 clickDelta=uv-_Click.xy;
                float radius=.025+_Click.z*.46;
                float front=exp(-pow((length(clickDelta)-radius)/.045,2))*(1-saturate(_Click.z))*_Click.w;
                velocity+=normalize(clickDelta+float2(.0001,.0001))*front*_Step*.2;
                velocity=clamp(velocity,-.09,.09);
                float2 sampleUV=clamp(uv-velocity*_Step,texel,1-texel);
                float density=tex2D(_MainTex,sampleUV).r;
                float neighbours=(tex2D(_MainTex,sampleUV+float2(texel.x,0)).r+tex2D(_MainTex,sampleUV-float2(texel.x,0)).r+tex2D(_MainTex,sampleUV+float2(0,texel.y)).r+tex2D(_MainTex,sampleUV-float2(0,texel.y)).r)*.25;
                density=lerp(density,neighbours,_Step*.12);
                density=lerp(density,base,_Step*.18);
                density+=pointer*min(length(_Drag.xy),1)*_Step*.32;
                density+=front*_Step*.6;
                float boundary=smoothstep(0,.035,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                return float4(saturate(density)*boundary,velocity,1);
            }
            ENDCG
        }
    }
}
