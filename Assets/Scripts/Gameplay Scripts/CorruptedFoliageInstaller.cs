using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CorruptedFoliageInstaller
{
    private static readonly Vector3[] GrassPositions =
    {
        // Zona frontal visible desde la cámara de juego.
        new Vector3(2.5f, 0.12f, 3.5f),
        new Vector3(5.5f, 0.12f, 2.5f),
        new Vector3(10.5f, 0.12f, 3.5f),
        new Vector3(14.5f, 0.12f, 5.5f),
        new Vector3(3.5f, 0.12f, 8.5f),
        new Vector3(11.5f, 0.12f, 8f),

        // Laterales y fondo, sin cubrir los puntos de aparición.
        new Vector3(18f, 0.12f, 9.5f),
        new Vector3(5f, 0.12f, 14.5f),
        new Vector3(19f, 0.12f, 15.5f),
        new Vector3(7.5f, 0.12f, 20f),
        new Vector3(16f, 0.12f, 21f),
        new Vector3(12f, 0.12f, 24f),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install(scene);
    }

    private static void Install(Scene scene)
    {
        if (scene.name != "Movement" || GameObject.Find("Generated Corrupted Foliage") != null)
            return;

        Texture2D grassTexture = Resources.Load<Texture2D>("Foliage/grass_corrupted");
        Texture2D vineTexture = Resources.Load<Texture2D>("Foliage/vines_corrupted");
        if (grassTexture == null || vineTexture == null)
        {
            Debug.LogWarning("No se pudieron cargar las texturas de vegetación corrompida.");
            return;
        }

        Transform root = new GameObject("Generated Corrupted Foliage").transform;
        CreateWorldBackdrop(root);
        Material grassMaterial = CreateMaterial("Corrupted Grass Material", grassTexture);
        Material vineMaterial = CreateMaterial("Corrupted Vines Material", vineTexture);

        for (int i = 0; i < GrassPositions.Length; i++)
        {
            float scale = 1.25f + (i % 3) * 0.2f;
            CreateCrossedCluster(
                root,
                $"Corrupted Grass {i + 1}",
                GrassPositions[i],
                new Vector2(scale * 2.1f, scale * 2.4f),
                grassMaterial,
                i * 17f);
        }

        CreateCrossedCluster(root, "Sanctuary Vines A", new Vector3(10.3f, 3.2f, 14.2f), new Vector2(5.2f, 7.2f), vineMaterial, 18f);
        CreateCrossedCluster(root, "Sanctuary Vines B", new Vector3(15.2f, 3.0f, 15.7f), new Vector2(4.6f, 6.5f), vineMaterial, -24f);
    }

    private static void CreateWorldBackdrop(Transform parent)
    {
        // Extensión puramente visual: oculta el vacío sin ampliar el NavMesh jugable.
        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Plane);
        backdrop.name = "Distant Corrupted Ground";
        backdrop.transform.SetParent(parent, false);
        backdrop.transform.position = new Vector3(12f, -1.3f, 14f);
        backdrop.transform.localScale = new Vector3(24f, 1f, 24f);

        Collider collider = backdrop.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        Shader shader = Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "Distant Corrupted Ground Material",
            color = new Color(0.055f, 0.085f, 0.075f, 1f)
        };
        material.SetFloat("_Glossiness", 0.05f);
        backdrop.GetComponent<MeshRenderer>().sharedMaterial = material;

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.032f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.055f, 0.075f, 0.068f, 1f);
        RenderSettings.fogStartDistance = 65f;
        RenderSettings.fogEndDistance = 145f;
    }

    private static Material CreateMaterial(string name, Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader)
        {
            name = name,
            mainTexture = texture,
            color = Color.white,
            renderQueue = 3000
        };
        return material;
    }

    private static void CreateCrossedCluster(
        Transform parent,
        string name,
        Vector3 position,
        Vector2 size,
        Material material,
        float rotationOffset)
    {
        Transform cluster = new GameObject(name).transform;
        cluster.SetParent(parent, false);
        cluster.position = position;

        for (int i = 0; i < 3; i++)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = $"Plane {i + 1}";
            plane.transform.SetParent(cluster, false);
            plane.transform.localPosition = Vector3.up * (size.y * 0.5f);
            plane.transform.localRotation = Quaternion.Euler(0f, rotationOffset + i * 60f, 0f);
            plane.transform.localScale = new Vector3(size.x, size.y, 1f);

            Collider collider = plane.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
