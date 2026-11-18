Shader "Custom/BuzzyPassenger"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Pass
        {
            Name "ForwardLit"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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
                float3 viewDir : TEXCOORD2;
                float objY : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.objY = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.worldNormal);
                float3 V = normalize(i.viewDir);
                float3 L = normalize(float3(-0.11026357, 0.82697678, -0.55131787));

                float hFactor = saturate((i.objY + 0.1) * 1.42857134);
                hFactor = hFactor * hFactor * (3.0 - 2.0 * hFactor);

                float ndl = saturate(dot(N, L));
                ndl = ndl * ndl * (3.0 - 2.0 * ndl);
                float shade = hFactor * ndl;

                float3 R = reflect(-L, N);
                float spec = pow(saturate(dot(R, V)), 10.0) * 0.3;

                // Mystery / Hidden Passenger shading (_Color.a < 0.5)
                float rimM = 1.0 - smoothstep(0.0, 1.0, saturate((dot(V, N) * shade - 0.3) * 3.333333));
                float3 mysteryCol = shade * float3(0.32, 0.39, 0.49) + rimM * 0.6 + spec;

                // Normal Passenger body shading
                float3 c = saturate(_Color.rgb);
                float3 cDark = (c * c) * 0.75;
                float3 bodyCol = lerp(cDark, c, shade);
                float3 hiTarget = saturate(bodyCol * 2.0 + 0.8);
                bodyCol = lerp(bodyCol, hiTarget, spec);

                // Eyes encoded in UV.y (from PixelBus/3D/Passengers GLSL)
                if (i.uv.y > 0.9)
                {
                    bodyCol = saturate(c + 0.5) * 1.2;
                }
                else if (i.uv.y < 0.1)
                {
                    bodyCol = c * 0.4;
                }

                float3 finalCol = (_Color.a < 0.99) ? lerp(mysteryCol, bodyCol, saturate(_Color.a)) : bodyCol;
                return fixed4(finalCol, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
