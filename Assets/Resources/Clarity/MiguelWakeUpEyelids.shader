Shader "UI/MiguelWakeUpEyelids"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Open ("Eye opening", Range(0,1)) = 0
        _Feather ("Feather", Range(0.001,0.08)) = 0.022
        _Curvature ("Eyelid curvature", Range(0,0.8)) = 0.32
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Open;
            float _Feather;
            float _Curvature;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_Open <= 0.001) return fixed4(0, 0, 0, 1);
                if (_Open >= 0.999) return fixed4(0, 0, 0, 0);

                float side = pow(saturate(abs(i.uv.x - 0.5) * 2.0), 1.6);
                float halfHeight = 0.5 * _Open * (1.0 - _Curvature * side);
                float distanceFromCenter = abs(i.uv.y - 0.5);
                float covered = smoothstep(halfHeight - _Feather,
                    halfHeight + _Feather, distanceFromCenter);
                float finalUncover = 1.0 - smoothstep(0.82, 1.0, _Open);
                return fixed4(0, 0, 0, covered * finalUncover);
            }
            ENDCG
        }
    }
}
