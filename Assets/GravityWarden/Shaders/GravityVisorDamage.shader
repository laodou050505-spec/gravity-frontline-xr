Shader "GravityWarden/UI/VisorDamage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Nano Banana glass fractures", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Damage ("Missing shield ratio", Range(0,1)) = 0
        _EdgeStrength ("Progressive glass visibility", Range(0,1)) = 1
        _HitPulse ("Brief impact", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay+990" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex; fixed4 _Color; half _Damage, _HitPulse, _EdgeStrength;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                float edge=max(abs(uv.x-.5),abs(uv.y-.5))*2;
                float critical=saturate((_Damage-.65)/.35);
                // Reveal outer fractures first, extending inward as shield is depleted.
                float reveal=smoothstep(.04+(1-edge)*.6,.17+(1-edge)*.6,_Damage);
                float centre=smoothstep(.21,.52,length((uv-.5)*float2(1.15,1)));
                float crack=tex2D(_MainTex,uv).a*reveal*centre*(.32+.65*_Damage)*_EdgeStrength;
                float rim=pow(saturate(edge),9)*(.20*_HitPulse+.10*critical*critical);
                // Fine stationary chromatic faults on the very outer glass at critical health.
                float stripe=pow(saturate(sin(uv.x*1650)),28)*step(.86,edge)*critical*.085;
                float3 ice=float3(.75,.90,1);
                float3 warm=float3(1,.14,.12);
                float3 rgb=(ice*crack+warm*rim+float3(.38,.72,1)*stripe)/max(.0001,crack+rim+stripe);
                return float4(rgb, saturate(crack+rim+stripe)*i.color.a)*float4(i.color.rgb,1);
            }
            ENDCG
        }
    }
}
