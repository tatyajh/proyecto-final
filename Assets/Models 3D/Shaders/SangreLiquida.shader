Shader "Custom/SangreCorazonDeforme"
{
    Properties
    {
        _MainTex ("Textura de Ruido/Voronoi", 2D) = "white" {}
        _BaseColor ("Color de Fondo (Oscuro)", Color) = (0.2, 0, 0.05, 1)
        _GlowColor ("Color de Venas (Brillante)", Color) = (0.9, 0.1, 0.1, 1)
        _FresnelColor ("Color del Borde (Rim)", Color) = (1, 0.2, 0.3, 1)
        
        _SpeedX ("Velocidad X", Float) = 0.05
        _SpeedY ("Velocidad Y", Float) = 0.08
        _Tiling ("Escala de Textura", Float) = 1.5
        _Steps ("Niveles Toon", Range(2, 8)) = 4
        _FresnelPower ("Fuerza de Borde", Range(0.5, 5)) = 2.5

        [Header(Deformacion y Latido Organico)]
        _PulseSpeed ("Velocidad de Latido", Float) = 3.5
        _PulseAmount ("Intensidad de Latido", Range(0, 1)) = 0.2
        _DeformFrequency ("Frecuencia de Bultos", Float) = 3.0
        _DeformAmount ("Deformacion Asimetrica", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
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
                float3 objPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _BaseColor;
            fixed4 _GlowColor;
            fixed4 _FresnelColor;
            float _SpeedX;
            float _SpeedY;
            float _Tiling;
            float _Steps;
            float _FresnelPower;

            float _PulseSpeed;
            float _PulseAmount;
            float _DeformFrequency;
            float _DeformAmount;

            v2f vert (appdata v)
            {
                v2f o;

                // 1. Detectar la escala global para anular el 0.007 del Transform
                float objectScale = length(mul((float3x3)unity_ObjectToWorld, float3(1,0,0)));
                float scaleFactor = (objectScale > 0.00001) ? (1.0 / objectScale) : 1.0;

                // 2. Normalizar posición para cálculo uniforme de ruido
                float3 normPos = normalize(v.vertex.xyz);

                // 3. Tiempo del latido
                float time = _Time.y * _PulseSpeed;

                // 4. Latido Asimétrico + Bultos orgánicos
                float spatialOffset = normPos.x * 1.5 + normPos.y * 2.0 + normPos.z * 1.0;
                float heartbeat = pow(sin(time + spatialOffset) * 0.5 + 0.5, 3.0);

                float3 p = normPos * _DeformFrequency;
                float noiseWave = sin(p.x + time * 1.5) * cos(p.y - time * 0.8) + sin(p.z + time * 1.2);

                // 5. Aplicar desplazamiento amplificado según el factor de escala
                float totalDisplacement = ((heartbeat * _PulseAmount) + (noiseWave * _DeformAmount)) * scaleFactor;
                v.vertex.xyz += v.normal * totalDisplacement;

                // Coordinar vértices a GPU
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.objPos = v.vertex.xyz * _Tiling;
                
                o.normal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Mapeo Triplanar
                float2 offset = float2(_SpeedX, _SpeedY) * _Time.y;
                float2 uvX = i.objPos.yz + offset;
                float2 uvY = i.objPos.xz + offset;
                float2 uvZ = i.objPos.xy + offset;

                float noiseVal = (tex2D(_MainTex, uvX).r + tex2D(_MainTex, uvY).r + tex2D(_MainTex, uvZ).r) / 3.0;

                // Estilo Toon
                noiseVal = floor(noiseVal * _Steps) / _Steps;
                fixed4 finalColor = lerp(_BaseColor, _GlowColor, noiseVal);

                // Borde Fresnel
                float NdotV = 1.0 - saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                float rim = pow(NdotV, _FresnelPower);
                rim = floor(rim * 3.0) / 3.0;

                finalColor += rim * _FresnelColor;

                return finalColor;
            }
            ENDCG
        }
    }
}