using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CorruptedFoliageInstaller
{
    private static readonly Vector3[] GrassPositions =
    {
        new Vector3(6.5f, 0f, 8.5f),
        new Vector3(11f, 0f, 7f),
        new Vector3(17.5f, 0f, 9.5f),
        new Vector3(6.5f, 0f, 17.5f),
        new Vector3(18.5f, 0f, 19f),
        new Vector3(12f, 0f, 22f),
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
        Material grassMaterial = CreateMaterial("Corrupted Grass Material", grassTexture);
        Material vineMaterial = CreateMaterial("Corrupted Vines Material", vineTexture);

        for (int i = 0; i < GrassPositions.Length; i++)
        {
            float scale = 1.65f + (i % 3) * 0.22f;
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
