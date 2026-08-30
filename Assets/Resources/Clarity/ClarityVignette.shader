Shader "Hidden/ALAALA/ClarityVignette"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Softness ("Softness", Range(0.1, 1)) = 0.65
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;
            float _Softness;

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv);

                float2 centeredUv = input.uv * 2.0 - 1.0;
                float edgeDistance = length(centeredUv) / 1.41421356;
                float innerEdge = 1.0 - _Softness;
                float vignette = smoothstep(innerEdge, 1.0, edgeDistance);

                color.rgb *= 1.0 - vignette * _Intensity;
                return color;
            }
            ENDCG
        }
    }
}
