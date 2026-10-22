Shader "GravityWarden/StarTwinkle"
{
    Properties
    {
        _MainTex ("Star Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TwinkleStrength ("Twinkle Strength", Range(0, 0.35)) = 0.18
        _TwinkleSpeed ("Twinkle Speed", Range(0.1, 1.5)) = 0.62
    }
    SubShader
    {
        Tags { "Queue"="Transparent+18" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _TwinkleStrength;
            float _TwinkleSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float phase : TEXCOORD1;
                float frequency : TEXCOORD2;
            };

            float Hash31(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                float seed = Hash31(float3(v.color.rg * 31.0, v.color.b * 47.0));
                o.phase = seed * 6.2831853;
                o.frequency = lerp(0.58, 1.0, Hash31(float3(v.color.br * 53.0, v.color.g * 37.0) + 19.17));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 star = tex2D(_MainTex, i.uv) * _Color * i.color;
                float wave = 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed * i.frequency + i.phase);
                float brightness = lerp(1.0 - _TwinkleStrength * 0.55, 1.0 + _TwinkleStrength * 0.45, wave);
                star.rgb *= brightness;
                star.a *= brightness;
                return star;
            }
            ENDCG
        }
    }
}
