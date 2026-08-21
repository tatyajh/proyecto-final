Shader "Custom/AuraRoja"
{
    Properties
    {
        _ColorAura ("Color del Aura", Color) = (1, 0, 0, 1)
        _Grosor ("Grosor (Metros)", Range(0.001, 0.05)) = 0.002
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            fixed4 _ColorAura;
            float _Grosor;

            v2f vert (appdata v)
            {
                v2f o;
                float3 n = normalize(v.normal);
                v.vertex.xyz += n * _Grosor;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _ColorAura;
            }
            ENDCG
        }
    }
}