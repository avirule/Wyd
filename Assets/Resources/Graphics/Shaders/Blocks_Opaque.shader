Shader "Wyd/Blocks - Opaque"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2DArray) = "white" {}
        _Offset ("_Offset", Vector) = (0.0, 0.0, 0.0, 0.0)
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
                float3 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 position : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            float4 _Offset;

            v2f vert(appdata v)
            {
                v2f f;
                f.position = UnityObjectToClipPos(float3(v.position & 63, (v.position >> 6) & 63, (v.position >> 12) & 63));
                f.texcoord = v.texcoord;
                return f;
            }

            fixed4 frag(v2f f) : SV_TARGET
            {
                return UNITY_SAMPLE_TEX2DARRAY(_MainTex, f.texcoord);
            }

            ENDCG
        }
    }

}
