Shader "Custom/Character PBR"
{
    Properties
    {
        _MainTex ("Base Color (Albedo)", 2D) = "white" {}
        _HeightMap ("Height Map (Parallax)", 2D) = "black" {}
        _Parallax ("Altura / Intensidad Height", Range (0.005, 0.08)) = 0.02
        _MetallicMap ("Metallic Map", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _RoughnessMap ("Roughness Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        // Renderiza ambas caras de la malla
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _HeightMap;
        sampler2D _MetallicMap;
        sampler2D _BumpMap;
        sampler2D _RoughnessMap;
        float _Parallax;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_HeightMap;
            float2 uv_MetallicMap;
            float2 uv_BumpMap;
            float2 uv_RoughnessMap;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Height Map (Parallax Mapping) para dar profundidad extra al relieve
            float height = tex2D(_HeightMap, IN.uv_HeightMap).r;
            float2 offset = ParallaxOffset(height, _Parallax, IN.viewDir);
            float2 finalUV = IN.uv_MainTex + offset;

            // 2. Base Color (Albedo)
            fixed4 albedo = tex2D(_MainTex, finalUV);
            o.Albedo = albedo.rgb;

            // 3. Normal Map
            o.Normal = UnpackNormal(tex2D(_BumpMap, finalUV));

            // 4. Metallic Map
            fixed metallic = tex2D(_MetallicMap, finalUV).r;
            o.Metallic = metallic;

            // 5. Roughness Map (Invertido a Smoothness para Unity Standard)
            fixed roughness = tex2D(_RoughnessMap, finalUV).r;
            o.Smoothness = 1.0 - roughness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}