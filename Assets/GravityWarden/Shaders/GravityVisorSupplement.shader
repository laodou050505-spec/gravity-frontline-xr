Shader "GravityWarden/UI/VisorSupplement"
{
    Properties
    {
        [PerRendererData] _MainTex ("Additional glass fractures", 2D) = "white" {}
        _Layer ("Display fault / additional glass", Float) = 0
        _Darkening ("Power loss", Range(0,1)) = 0
        _FaultRatio ("Display fault severity", Range(0,1)) = 0
        _FaultTime ("Unscaled fault time", Float) = 0
        _HitPulse ("Impact interruption", Range(0,1)) = 0
        _CentralCracks ("Additional glass opacity", Range(0,1)) = 0
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
            sampler2D _MainTex;
            float _Layer,_Darkening,_FaultRatio,_FaultTime,_HitPulse,_CentralCracks;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;
            }
            float band(float coordinate,float centre,float width)
            { return 1-smoothstep(width,width+.008,abs(coordinate-centre)); }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                if(_Layer>.5)
                {
                    float core=smoothstep(.032,.055,length((uv-.5)*float2(1.15,1)));
                    float a=tex2D(_MainTex,uv).a*_CentralCracks*core;
                    return float4(float3(.75,.90,1)*i.color.rgb,a*i.color.a);
                }
                float edge=max(abs(uv.x-.5),abs(uv.y-.5))*2;
                // A persistent power sag plus local dark LCD rows. No global flicker:
                // animation only changes the strength of two damaged display regions.
                float slow=.5+.5*sin(_FaultTime*2.4166);
                float leftRegion=1-smoothstep(.42,.60,uv.x);
                float rightRegion=smoothstep(.47,.68,uv.x);
                float rows=band(uv.y,.31,.024)*leftRegion
                    +band(uv.y,.69,.013)*rightRegion
                    +band(uv.y,.84,.035)*(1-smoothstep(.62,.82,uv.x));
                float interrupted=band(uv.y,.31,.008)*leftRegion*slow
                    +band(uv.y,.69,.006)*rightRegion*(1-slow);
                float scan=(1-smoothstep(.08,.2,frac(uv.y*185)))*.055*_FaultRatio;
                float dropout=saturate(_FaultRatio*(rows*.30+interrupted*.25+pow(saturate(edge),3)*.20)
                    +scan+_HitPulse*rows*.16);
                float alpha=1-(1-_Darkening)*(1-dropout);
                // A few dim cyan row faults suggest electronics, subordinate to darkening.
                float trace=(band(uv.y,.342,.0005)*leftRegion+band(uv.y,.711,.0005)*rightRegion)*_FaultRatio;
                return float4(float3(.008,.07,.085)*trace,alpha*i.color.a);
            }
            ENDCG
        }
    }
}
