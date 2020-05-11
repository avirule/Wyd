Shader "Wyd/Blocks - Opaque"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2DArray) = "white" {}
        _Color ("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }

    SubShader
    {
        Tags {  "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma require 2darray

            struct appdata
            {
                int position : POSITION;
                int texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 position : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                float4 color : COLOR;
                half3 normal : NORMAL;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            float4 _Color;

            v2f vert(appdata v)
            {
                float3 uncompressedVertex = float3(v.position & 63, (v.position >> 6) & 63, (v.position >> 12) & 63);
                float3 uncompressedUv = float3(v.texcoord & 63, (v.texcoord >> 6) & 63, (v.texcoord >> 12) & 0x7FFFFFFF);
                half3 uncompressedNormal = half3(((v.position >> 18) & 3) - 1.0, ((v.position >> 20) & 3) - 1.0, ((v.position >> 22) & 3) - 1.0);
                float3 normalLerpedColor = float3(smoothstep(-1.5, 4.25, uncompressedNormal.x) * 1.3, smoothstep(-1.5, 4.25, uncompressedNormal.y) * 1.15, smoothstep(-1.0, 4.25, uncompressedNormal.z));
                float normalColor = normalLerpedColor.x + normalLerpedColor.y + normalLerpedColor.z;

                v2f f;
                f.position = UnityObjectToClipPos(uncompressedVertex);
                f.normal = UnityObjectToClipPos(uncompressedNormal);
                f.texcoord = uncompressedUv;
                f.color = float4(normalColor, normalColor, normalColor, 1.0);
                return f;
            }

            fixed4 frag(v2f f) : SV_TARGET
            {
                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, f.texcoord) * f.color * _Color;
            }

            ENDCG
        }
    }

}
