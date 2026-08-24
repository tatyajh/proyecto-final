Shader "Custom/Environment_CameraFade"
{
    Properties
    {
        _MainTex ("Base Color (Albedo)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _RoughnessMap ("Roughness Map", 2D) = "white" {}
        
        [Header(Configuracion de Distancia Camera)]
        _StartFadeDist ("Distancia Inicio Desvanecer", Float) = 2.0
        _EndFadeDist ("Distancia Totalmente Invisible", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _RoughnessMap;

        float _StartFadeDist;
        float _EndFadeDist;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_RoughnessMap;
            float3 worldPos;
            float4 screenPos;
        };

        // Matriz Dither 4x4 para hacer la transparencia por puntos
        static const float4x4 ditherMatrix = float4x4(
            0.0,  0.5,  0.125,0.625,
            0.75, 0.25, 0.875,0.375,
            0.1875,0.6875,0.0625,0.5625,
            0.9375,0.4375,0.8125,0.3125
        );

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Calcular distancia de la superficie a la Camara
            float dist = distance(_WorldSpaceCameraPos, IN.worldPos);

            // 2. Mapear la distancia a un rango de 0 a 1
            float fadeFactor = saturate((dist - _EndFadeDist) / (_StartFadeDist - _EndFadeDist));

            // 3. Obtener coordenadas de pantalla para el patron de puntos (Dither)
            float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
            float2 ditherUV = fmod(screenUV * _ScreenParams.xy, 4.0);
            float threshold = ditherMatrix[ditherUV.x][ditherUV.y];

            // 4. Descartar pixeles segun la cercania
            clip(fadeFactor - threshold);

            // 5. Configurar Texturas PBR
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = albedo.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            
            fixed roughness = tex2D(_RoughnessMap, IN.uv_RoughnessMap).r;
            o.Smoothness = 1.0 - roughness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}