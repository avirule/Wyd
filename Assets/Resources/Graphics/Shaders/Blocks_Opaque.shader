Shader "Wyd/Blocks - Opaque"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2DArray) = "white" {}
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

            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            struct appdata
            {
                int position : POSITION;
                int texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 position : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                half3 normal : NORMAL;
                float4 color : COLOR;
            };

            float4 _Offset;

            v2f vert(appdata v)
            {
                float3 uncompressedVertex = float3(v.position & 63, (v.position >> 6) & 63, (v.position >> 12) & 63);
                half3 uncompressedNormal = half3(((v.position >> 18) & 3) - 1.0, ((v.position >> 20) & 3) - 1.0, ((v.position >> 22) & 3) - 1.0);
                float3 uncompressedUv = float3(v.texcoord & 63, (v.texcoord >> 6) & 63, (v.texcoord >> 12) & 0x7FFFFFFF);

                v2f f;
                f.position = UnityObjectToClipPos(uncompressedVertex);
                f.normal = UnityObjectToClipPos(uncompressedNormal);
                f.texcoord = uncompressedUv;
                f.color = float4((uncompressedNormal + 1.0) / 2.0, 1.0);
                return f;
            }

            fixed4 frag(v2f f) : SV_TARGET
            {
                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, f.texcoord) * f.color;
            }

            ENDCG
        }
    }

}
