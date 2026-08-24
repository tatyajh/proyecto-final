using UnityEngine;

/// <summary>
/// Esporas bioluminiscentes flotando sobre el vacío, con una viñeta que cierra
/// los bordes. Vive por encima de todas las fases: es lo único que no cambia
/// entre el prólogo y la selección de personaje, y es lo que da continuidad.
///
/// Se construye por código para que la escena no dependa de un prefab que
/// alguien pueda romper sin darse cuenta.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class SporeAtmosphere : MonoBehaviour
{
    [Header("Densidad")]
    [SerializeField, Min(0.1f)] private float sporesPerSecond = 0.65f;
    [SerializeField, Min(1f)] private float lifetime = 18f;
    [SerializeField, Min(0.1f)] private float leavesPerSecond = 0.55f;
    [SerializeField, Min(1f)] private float leafLifetime = 13f;

    [Header("Deriva")]
    [Tooltip("Caída muy lenta: las esporas deben flotar, no llover.")]
    [SerializeField] private float fallSpeed = 0.35f;
    [SerializeField] private float horizontalDrift = 0.25f;

    private ParticleSystem sporeSystem;
    private ParticleSystem leafSystem;

    /// <summary>Crea el sistema completo colgado de la cámara indicada.</summary>
    public static SporeAtmosphere Create(Transform parent, Camera viewCamera)
    {
        GameObject host = new GameObject("Spore Atmosphere");
        host.transform.SetParent(parent, false);

        // Delante de la cámara, en el plano donde se ven sin perspectiva rara.
        host.transform.position = viewCamera != null
            ? viewCamera.transform.position + viewCamera.transform.forward * 12f + Vector3.up * 7f
            : new Vector3(0f, 7f, 12f);

        SporeAtmosphere atmosphere = host.AddComponent<SporeAtmosphere>();
        atmosphere.Build(viewCamera);
        return atmosphere;
    }

    private void Build(Camera viewCamera)
    {
        sporeSystem = GetComponent<ParticleSystem>();
        sporeSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = sporeSystem.main;
        main.loop = true;
        main.startLifetime = lifetime;
        main.startSpeed = fallSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Sin escalar por Time.timeScale: el menú debe seguir vivo en pausa.
        main.useUnscaledTime = true;
        main.maxParticles = Mathf.CeilToInt(sporesPerSecond * lifetime) + 32;
        main.startColor = new ParticleSystem.MinMaxGradient(
            MenuTheme.WithAlpha(MenuTheme.OroMarchito, 0.32f),
            MenuTheme.WithAlpha(MenuTheme.MarfilEnvejecido, 0.24f));

        ParticleSystem.EmissionModule emission = sporeSystem.emission;
        emission.rateOverTime = sporesPerSecond;

        // Caja ancha por encima del encuadre: las esporas entran ya en marcha.
        ParticleSystem.ShapeModule shape = sporeSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(26f, 0.1f, 6f);

        // Rotan la deriva horizontal para que no caigan en línea recta.
        ParticleSystem.VelocityOverLifetimeModule velocity = sporeSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // Las tres curvas deben compartir modo (aquí, dos constantes) o Unity
        // rechaza el módulo entero; z quedaba en su modo por defecto y no
        // encajaba con x/y, lo que disparaba "curves must all be in the same
        // mode" en cada frame.
        velocity.x = new ParticleSystem.MinMaxCurve(-horizontalDrift, horizontalDrift);
        velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed, -fallSpeed * 0.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Aparecen y se apagan solas: nunca deben "cortarse" en pantalla.
        ParticleSystem.ColorOverLifetimeModule fade = sporeSystem.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.3f, 0.25f),
                new GradientAlphaKey(0.24f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        // Latido lento de tamaño: sugiere que están vivas.
        ParticleSystem.SizeOverLifetimeModule size = sporeSystem.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.7f)));

        ConfigureRenderer(viewCamera);
        BuildLeaves(viewCamera);
        sporeSystem.Play();
    }

    private void BuildLeaves(Camera viewCamera)
    {
        GameObject leafHost = new GameObject("Falling Leaves", typeof(ParticleSystem));
        leafHost.transform.SetParent(transform, false);
        leafSystem = leafHost.GetComponent<ParticleSystem>();
        leafSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = leafSystem.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(leafLifetime * 0.8f, leafLifetime * 1.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = Mathf.CeilToInt(leavesPerSecond * leafLifetime) + 12;
        main.startColor = new ParticleSystem.MinMaxGradient(
            MenuTheme.WithAlpha(MenuTheme.OroMarchito, 0.25f),
            MenuTheme.WithAlpha(MenuTheme.RojoLatente, 0.20f));

        ParticleSystem.EmissionModule emission = leafSystem.emission;
        emission.rateOverTime = leavesPerSecond;

        ParticleSystem.ShapeModule shape = leafSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(25f, 0.1f, 5f);

        ParticleSystem.VelocityOverLifetimeModule velocity = leafSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.32f, 0.32f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.62f, -0.34f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

        ParticleSystem.RotationOverLifetimeModule rotation = leafSystem.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.85f, 0.85f);

        ParticleSystem.ColorOverLifetimeModule fade = leafSystem.colorOverLifetime;
        fade.enabled = true;
        Gradient leafFade = new Gradient();
        leafFade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.16f),
                new GradientAlphaKey(0.72f, 0.78f),
                new GradientAlphaKey(0f, 1f)
            });
        fade.color = new ParticleSystem.MinMaxGradient(leafFade);

        ParticleSystemRenderer renderer = leafSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = -11;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                        ?? Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material material = new Material(shader) { name = "Botanical Leaf Particles (Runtime)" };
            Texture2D leaf = CreateLeafTexture();
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", leaf);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", leaf);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);
            renderer.material = material;
        }

        if (viewCamera != null) renderer.sortingLayerName = "Default";
        leafSystem.Play();
    }

    private static Texture2D CreateLeafTexture()
    {
        const int width = 64;
        const int height = 40;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Botanical Leaf (Runtime)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = (x + 0.5f) / width * 2f - 1f;
            float ny = (y + 0.5f) / height * 2f - 1f;
            float halfWidth = Mathf.Sin((ny + 1f) * Mathf.PI * 0.5f) * 0.82f;
            float edge = halfWidth - Mathf.Abs(nx);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 12f));
            pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void ConfigureRenderer(Camera viewCamera)
    {
        ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;

        // Mezcla suave: la textura y las ramas son protagonistas; las esporas
        // solo añaden profundidad y no deben leerse como confeti luminoso.
        Shader shader = Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Legacy Shaders/Particles/Additive")
                        ?? Shader.Find("Sprites/Default");
        if (shader == null) return;

        Material material = new Material(shader) { color = Color.white };
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);
        renderer.material = material;
        renderer.sortingOrder = -10;

        if (viewCamera != null)
            renderer.sortingLayerName = "Default";
    }

    /// <summary>Sube o baja la densidad según la fase: el prólogo pide menos ruido.</summary>
    public void SetIntensity(float multiplier)
    {
        if (sporeSystem == null) return;
        ParticleSystem.EmissionModule emission = sporeSystem.emission;
        emission.rateOverTime = sporesPerSecond * Mathf.Max(0f, multiplier);
        if (leafSystem != null)
        {
            ParticleSystem.EmissionModule leaves = leafSystem.emission;
            leaves.rateOverTime = leavesPerSecond * Mathf.Max(0f, multiplier);
        }
    }
}
