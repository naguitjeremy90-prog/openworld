Shader "UI/JournalAttentionAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sparkle Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            float4 frag(v2f i) : SV_Target { return float4(tex2D(_MainTex, i.uv).rgb * i.color.rgb * i.color.a, 0); }
            ENDCG
        }
    }
}
