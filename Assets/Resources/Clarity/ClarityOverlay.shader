Shader "Hidden/ALAALA/ClarityOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _HighlightColor ("Highlight Color", Color) = (1, 0.78, 0.45, 1)
        _Strength ("Strength", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _HighlightColor;
            float _Strength;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed alphaMask = tex2D(_MainTex, input.uv).a;
                fixed4 color = _HighlightColor;

                // The original renderer stays visible underneath this restrained
                // warm additive layer, so its textures and shading remain intact.
                color.a = saturate(_Strength * 0.32) * alphaMask;
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
