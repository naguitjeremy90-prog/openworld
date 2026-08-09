Shader "Unlit/GridShader"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (1,1,1,1)
        _GridScale ("Grid Scale", Float) = 1.0
        _LineThickness ("Line Thickness", Float) = 0.01
        _FadeDistance ("Fade Distance", Float) = 50.0
        _FadeRange ("Fade Range", Float) = 10.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
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
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 objectPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            fixed4 _GridColor;
            float _GridScale;
            float _LineThickness;
            float _FadeDistance;
            float _FadeRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objectPos = v.vertex.xyz;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 gridUV = i.objectPos.xz / _GridScale;
                float2 lines = abs(frac(gridUV - 0.5) - 0.5) / fwidth(gridUV);

                float grid = min(lines.x, lines.y);
                float finalGrid = 1.0 - saturate(grid / _LineThickness);
                float dist = length(_WorldSpaceCameraPos - i.worldPos);
                // To only fade on XZ plane regardless of object rotation:
                // float dist = length(_WorldSpaceCameraPos.xz - i.worldPos.xz);

                float alpha = 1.0;
                if (dist > _FadeDistance)
                    alpha = 1.0 - saturate((dist - _FadeDistance) / _FadeRange);

                fixed4 finalColor = _GridColor;
                finalColor.a = finalColor.a * finalGrid * alpha;

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Standard"
}
