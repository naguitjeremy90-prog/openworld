Shader "UI/ClarityIrisTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,1)
        _Radius ("Radius", Float) = 1.5
        _Center ("Center", Vector) = (0.5,0.5,0,0)
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
            fixed4 _Color;
            float _Radius;
            float4 _Center;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta = i.uv - _Center.xy;
                delta.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float outsideMask = smoothstep(_Radius - 0.015, _Radius, length(delta));
                return fixed4(_Color.rgb, _Color.a * outsideMask);
            }
            ENDCG
        }
    }
}
