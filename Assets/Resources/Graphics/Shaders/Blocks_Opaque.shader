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
                float3 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 position : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            float4 _Offset;

            v2f vert(appdata v)
            {
                v2f f;
                f.position = UnityObjectToClipPos(float3(v.position & 63, (v.position >> 6) & 63, (v.position >> 12) & 63));
                f.texcoord = v.texcoord;
                f.normal = float3(((v.position >> 18) & 3) - 1.0, ((v.position >> 20) & 3) - 1.0, ((v.position >> 22) & 3) - 1.0);
                f.color = 1.0;
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
