Shader "GravityWarden/UI/GravityVisorOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Nano Banana visor overlay", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyThreshold ("Dark glass key", Range(0,1)) = .14
        _KeySoftness ("Key softness", Range(1,40)) = 14
        _ScanlineStrength ("Scanline modulation", Range(0,1)) = .08
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
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex; fixed4 _Color; half _KeyThreshold; half _KeySoftness; half _ScanlineStrength;
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 s=tex2D(_MainTex,i.uv);
                half lum=max(s.r,max(s.g,s.b));
                half alpha=saturate((lum-_KeyThreshold)*_KeySoftness);
                half scan=1.0-_ScanlineStrength*(.5+.5*sin((i.uv.y*720.0)+_Time.y*2.4));
                s.a*=alpha*scan;
                s*=i.color;
                return s;
            }
            ENDCG
        }
    }
}
