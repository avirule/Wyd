Shader "Wyd/Standard Terrain"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2DArray) = "white" {}
        _Offset ("_CoordinateOffset", Vector) = (0.0, 0.0, 0.0, 0.0)
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
                float4 color : COLOR;
            };

            struct v2f {
                float4 position : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                float4 color: COLOR;
            };

            float4 _Offset;

            v2f vert(appdata v)
            {
                v2f f;
                f.position = UnityObjectToClipPos(_Offset.xyz + float3(v.position & 31, (v.position >> 5) & 31, (v.position >> 10) & 31));
                f.texcoord = v.texcoord;
                f.color = v.color;
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
