Shader "Custom/BloodTree_PBR_VenasFade"
{
    Properties
    {
        _MainTex ("Base Color (Albedo)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _RoughnessMap ("Roughness Map", 2D) = "white" {}
        
        [Header(Venas Sangrientas Desvanecibles)]
        _VenasTex ("Máscara de Venas (Blanco/Negro)", 2D) = "black" {}
        [HDR] _GlowColor ("Color de Venas", Color) = (1, 0, 0.2, 1)
        _FadeSpeed ("Velocidad de Aparecer/Desaparecer", Float) = 1.5
        _EmissionPower ("Intensidad Máxima del Brillo", Float) = 2.0

        [Header(Ajustes de Superficie)]
        _Metallic ("Metallic", Range(0, 1)) = 0.0
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
        sampler2D _VenasTex;

        fixed4 _GlowColor;
        float _FadeSpeed;
        float _EmissionPower;
        half _Metallic;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_RoughnessMap;
            float2 uv_VenasTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Base Color (Albedo)
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = albedo.rgb;

            // 2. Normal Map
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));

            // 3. Roughness Map (Invertido a Smoothness)
            fixed roughness = tex2D(_RoughnessMap, IN.uv_RoughnessMap).r;
            o.Smoothness = 1.0 - roughness;

            // 4. Metallic
            o.Metallic = _Metallic;

            // 5. Transición suave de aparición/desvanecimiento (Fade In / Fade Out)
            float fadeFactor = sin(_Time.y * _FadeSpeed) * 0.5 + 0.5;

            // 6. Emisión progresiva
            fixed4 venas = tex2D(_VenasTex, IN.uv_VenasTex);
            o.Emission = venas.r * _GlowColor.rgb * (fadeFactor * _EmissionPower);
        }
        ENDCG
    }
    FallBack "Diffuse"
}