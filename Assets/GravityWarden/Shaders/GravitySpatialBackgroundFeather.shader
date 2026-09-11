Shader "GravityWarden/UI/SpatialBackgroundFeather"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Feather ("Edge feather", Range(0.001,0.3)) = 0.20
        _MinimumAlpha ("Minimum edge alpha", Range(0,1)) = 0
        _UvRect ("Raw image UV rect", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _UvRect;
            half _Feather;
            half _MinimumAlpha;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.texcoord) * input.color;
                float2 uv = saturate((input.texcoord - _UvRect.xy) / max(_UvRect.zw, float2(.0001, .0001)));
                float2 edge = min(uv, 1.0 - uv);
                // Independent smooth fades round the corners without diagonal
                // seams. Use the panel's short side so all four fades are equal.
                float2 t = saturate(edge * float2(1.6, 1.0) / _Feather);
                float2 fade2 = t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
                half fade = fade2.x * fade2.y;
                sample.a *= lerp(_MinimumAlpha, 1.0, fade);
                return sample;
            }
            ENDCG
        }
    }
}
