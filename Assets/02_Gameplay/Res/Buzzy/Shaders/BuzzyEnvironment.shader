Shader "Custom/BuzzyEnvironment"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _SrcBlend ("SrcBlend", Float) = 1
        _DstBlend ("DstBlend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Pass
        {
            Name "ForwardEnv"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                float3 N = normalize(i.worldNormal);
                float3 L = normalize(float3(-0.15, 0.92, -0.35));
                float ndl = saturate(dot(N, L)) * 0.22 + 0.86;
                return fixed4(tex.rgb * ndl, tex.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
