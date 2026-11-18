Shader "Custom/BuzzyVehicle"
{
    Properties
    {
        _BaseTex ("Base Texture", 2D) = "white" {}
        _Color ("Body Color", Color) = (1,0.5,0,1)
        _UseSubColor ("Use Sub Color", Float) = 0
        _SubColor ("Sub Color", Color) = (1,1,1,1)
        _OpenDoor ("Open Door", Range(0, 1)) = 0
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

            sampler2D _BaseTex;
            float4 _Color;
            float _UseSubColor;
            float4 _SubColor;
            float _OpenDoor;

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
                float objZ : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 pos = v.vertex;
                // Bus door vertices have uv.x > 1.0 in Bus__5717648863676121777.obj
                if (v.uv.x > 1.0)
                {
                    float dx = saturate(_OpenDoor * 5.0) * 0.05;
                    float dz = saturate(_OpenDoor * 1.25 - 0.25) * 0.70;
                    pos.x += dx;
                    pos.z += dz;
                }
                o.pos = UnityObjectToClipPos(pos);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(pos));
                o.objZ = pos.z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_BaseTex, i.uv);
                float3 bodyBase = _Color.rgb;
                if (_UseSubColor > 0.5)
                {
                    float t = saturate((i.objZ + 0.05) * 20.0);
                    t = t * t * (3.0 - 2.0 * t);
                    bodyBase = lerp(_Color.rgb, _SubColor.rgb, t);
                }
                bodyBase = saturate(bodyBase);

                float3 N = normalize(i.worldNormal);
                float3 V = normalize(i.viewDir);
                float3 L = normalize(float3(-0.11605177, 0.98644006, -0.11605177));

                float ndl = saturate(dot(N, L));
                float ramp = ndl * ndl * (3.0 - 2.0 * ndl);

                float3 R = reflect(-L, N);
                float spec = pow(saturate(dot(R, V)), 10.0) * 0.25;

                float3 cDark = pow(max(bodyBase, 0.001), 1.2) * 0.9;
                float3 cLight = pow(max(bodyBase, 0.001), 0.8) * 1.1;
                float aoFactor = ramp - (1.0 - tex.r) * 2.0;
                float3 bodyLit = lerp(cDark, cLight, saturate(aoFactor));
                float3 hiTarget = saturate(bodyLit * 2.0 + 0.8);
                bodyLit = lerp(bodyLit, hiTarget, spec);

                float texLight = min(ramp + 0.5, 1.0);
                float3 detailLit = tex.rgb * texLight;

                // tex.a > 0.5 -> paintable hull; tex.a < 0.5 -> windows, wheels, lights, bumper
                float paintMask = smoothstep(0.3, 0.7, tex.a);
                float3 finalCol = lerp(detailLit, bodyLit, paintMask);
                if (_Color.a < 0.5)
                {
                    // Surprise / Mystery Bus tint
                    finalCol = lerp(float3(0.36, 0.42, 0.54) * texLight, finalCol, paintMask < 0.5 ? 0.65 : 0.15);
                }
                return fixed4(finalCol, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
