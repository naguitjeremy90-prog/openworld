Shader "Custom/PanoramicSkyboxBlend"
{
    Properties
    {
        _TexA ("Sky A", 2D) = "white" {}
        _TexB ("Sky B", 2D) = "white" {}

        _Blend ("Blend", Range(0,1)) = 0

        _Exposure ("Exposure", Range(0,8)) = 1
        _Rotation ("Rotation", Range(0,360)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _TexA;
            sampler2D _TexB;

            float _Blend;
            float _Exposure;
            float _Rotation;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float3 RotateAroundY(float3 direction, float degrees)
            {
                float radians = degrees * UNITY_PI / 180.0;

                float sinValue = sin(radians);
                float cosValue = cos(radians);

                float3 result;

                result.x =
                    cosValue * direction.x -
                    sinValue * direction.z;

                result.y = direction.y;

                result.z =
                    sinValue * direction.x +
                    cosValue * direction.z;

                return result;
            }

            v2f vert(appdata v)
            {
                v2f o;

                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.direction);

                dir = RotateAroundY(dir, _Rotation);

                float longitude =
                    atan2(dir.x, dir.z);

                float latitude =
                    asin(dir.y);

                float2 uv;

                uv.x =
                    longitude / (2.0 * UNITY_PI) + 0.5;

                uv.y =
                    latitude / UNITY_PI + 0.5;

                fixed4 skyA =
                    tex2D(_TexA, uv);

                fixed4 skyB =
                    tex2D(_TexB, uv);

                fixed4 result =
                    lerp(skyA, skyB, _Blend);

                result.rgb *= _Exposure;

                return result;
            }

            ENDCG
        }
    }

    Fallback Off
}
