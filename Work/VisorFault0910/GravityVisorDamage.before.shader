Shader "GravityWarden/UI/VisorDamage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Nano Banana glass fractures", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Damage ("Missing shield ratio", Range(0,1)) = 0
        _HitPulse ("Brief impact", Range(0,1)) = 0
        _Darkening ("Full-view dimming", Range(0,.46)) = 0
        _EdgeDarkening ("Peripheral dimming", Range(0,.12)) = 0
        _CentralCracks ("Sparse central hairlines", Range(0,.32)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay+990" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex; fixed4 _Color;
            half _Damage, _HitPulse, _Darkening, _EdgeDarkening, _CentralCracks;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            float SegmentDistance(float2 p,float2 a,float2 b)
            {
                float2 ab=b-a;
                return length(p-a-ab*saturate(dot(p-a,ab)/dot(ab,ab)));
            }
            float CentralHairlines(float2 p)
            {
                // Two restrained asymmetric branches, with an open aiming core.
                float d=SegmentDistance(p,float2(-.235,.22),float2(-.19,.14));
                d=min(d,SegmentDistance(p,float2(-.19,.14),float2(-.155,.09)));
                d=min(d,SegmentDistance(p,float2(-.155,.09),float2(-.135,.015)));
                d=min(d,SegmentDistance(p,float2(-.135,.015),float2(-.085,-.018)));
                d=min(d,SegmentDistance(p,float2(-.155,.09),float2(-.205,.045)));
                d=min(d,SegmentDistance(p,float2(.195,-.215),float2(.145,-.16)));
                d=min(d,SegmentDistance(p,float2(.145,-.16),float2(.157,-.092)));
                d=min(d,SegmentDistance(p,float2(.157,-.092),float2(.09,-.06)));
                d=min(d,SegmentDistance(p,float2(.157,-.092),float2(.212,-.075)));
                float antialias=max(fwidth(d),.0003);
                return (1-smoothstep(.00035,.00035+antialias,d))*smoothstep(.038,.07,length(p));
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                float edge=max(abs(uv.x-.5),abs(uv.y-.5))*2;
                float critical=saturate((_Damage-.65)/.35);
                // Reveal outer fractures first, extending inward as shield is depleted.
                float reveal=smoothstep(.04+(1-edge)*.6,.17+(1-edge)*.6,_Damage);
                float2 optical=(uv-.5)*float2(1.15,1);
                float centre=smoothstep(.055,.23,length(optical));
                float crack=tex2D(_MainTex,uv).a*reveal*centre*(.32+.65*_Damage);
                crack=max(crack,CentralHairlines(optical)*_CentralCracks);
                float rim=pow(saturate(edge),9)*(.20*_HitPulse+.10*critical*critical);
                // Fine stationary chromatic faults on the very outer glass at critical health.
                float stripe=pow(saturate(sin(uv.x*1650)),28)*step(.86,edge)*critical*.085;
                float3 ice=float3(.75,.90,1);
                float3 warm=float3(1,.14,.12);
                float features=saturate(crack+rim+stripe);
                float3 featureColor=(ice*crack+warm*rim+float3(.38,.72,1)*stripe)/max(.0001,crack+rim+stripe);
                // Cool smoke tint is continuous across the full eye, including its centre.
                // Composite glass over the tint so narrow fractures stay legible at low health.
                float dim=saturate(_Darkening+_EdgeDarkening*pow(saturate(edge),2.5));
                float alpha=features+dim*(1-features);
                float3 rgb=(featureColor*features+float3(.002,.003,.006)*dim*(1-features))/max(.0001,alpha);
                return float4(rgb*i.color.rgb,alpha*i.color.a);
            }
            ENDCG
        }
    }
}
