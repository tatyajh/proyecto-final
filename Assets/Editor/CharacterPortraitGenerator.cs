using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera retratos homogéneos directamente desde los seis modelos aprobados.
/// Así la torre nunca mezcla ilustraciones, siluetas y espacios vacíos.
/// </summary>
public static class CharacterPortraitGenerator
{
    private const string OutputFolder = "Assets/Resources/UI/Portraits";
    private const int PortraitLayer = 31;
    private const int Size = 512;

    [MenuItem("Blighted Blossoms/Arte/Regenerar retratos 3D")]
    public static void GenerateFromMenu() => Generate(false);

    public static void GenerateBatch() => Generate(true);

    private static void Generate(bool exitWhenDone)
    {
        int failures = 0;
        Directory.CreateDirectory(OutputFolder);
        for (int i = 0; i < CharacterCatalog.Count; i++)
        {
            try { GeneratePortrait(i); }
            catch (Exception exception)
            {
                failures++;
                Debug.LogError($"[Portraits] {CharacterCatalog.NameOf(i)}: {exception}");
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        for (int i = 0; i < CharacterCatalog.Count; i++) ConfigureImport(i);
        AssignDefinitions();
        AssetDatabase.SaveAssets();
        Debug.Log(failures == 0
            ? "[Portraits] Seis retratos 3D uniformes generados."
            : $"[Portraits] Finalizó con {failures} errores.");
        if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 2);
    }

    private static void GeneratePortrait(int index)
    {
        GameObject prefab = CharacterCatalog.LoadModel(index);
        if (prefab == null) throw new InvalidOperationException("No tiene modelo en Resources.");

        GameObject model = UnityEngine.Object.Instantiate(prefab);
        model.name = $"Portrait {CharacterCatalog.NameOf(index)}";
        model.transform.SetPositionAndRotation(Vector3.zero,
            Quaternion.Euler(0f, CharacterCatalog.GameplayVisualYawOf(index), 0f));
        SetLayer(model.transform, PortraitLayer);
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController == null)
                animator.runtimeAnimatorController = CharacterCatalog.LoadAnimatorController(index);
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0.12f);
            animator.enabled = false;
        }

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.enabled).ToArray();
        if (renderers.Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(model);
            throw new InvalidOperationException("El modelo no contiene renderers visibles.");
        }
        Renderer dominant = renderers.OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude).First();
        Bounds bounds = dominant.bounds;
        float inclusionRadius = Mathf.Max(0.25f, dominant.bounds.extents.magnitude * 1.35f);
        foreach (Renderer renderer in renderers)
        {
            float distance = Vector3.Distance(renderer.bounds.center, dominant.bounds.center);
            if (distance <= inclusionRadius + renderer.bounds.extents.magnitude)
                bounds.Encapsulate(renderer.bounds);
        }

        GameObject cameraObject = new GameObject("Portrait camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.cullingMask = 1 << PortraitLayer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(0.1f, bounds.extents.y * 1.13f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = Mathf.Max(50f, bounds.size.magnitude * 6f);
        Vector3 target = bounds.center + Vector3.up * bounds.size.y * 0.015f;
        camera.transform.position = target + Vector3.forward *
            Mathf.Max(6f, bounds.extents.z + bounds.size.magnitude * 2f);
        camera.transform.LookAt(target);

        Light key = CreateLight("Portrait key", new Vector3(32f, 145f, 0f), 1.15f,
            new Color(1f, 0.86f, 0.68f));
        Light fill = CreateLight("Portrait fill", new Vector3(18f, -35f, 0f), 0.72f,
            new Color(0.58f, 0.48f, 0.82f));

        RenderTexture texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            name = $"{CharacterCatalog.NameOf(index)} portrait render"
        };
        texture.Create();
        camera.targetTexture = texture;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        Texture2D portrait = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
        portrait.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
        portrait.Apply(false, false);
        ReframeFromVisiblePixels(camera, texture, portrait);
        RenderTexture.active = previous;

        string path = $"{OutputFolder}/{CharacterCatalog.NameOf(index)}Portrait.png";
        File.WriteAllBytes(path, portrait.EncodeToPNG());

        camera.targetTexture = null;
        texture.Release();
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(portrait);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(key.gameObject);
        UnityEngine.Object.DestroyImmediate(fill.gameObject);
        UnityEngine.Object.DestroyImmediate(model);
    }

    private static void ReframeFromVisiblePixels(Camera camera, RenderTexture target, Texture2D pixels)
    {
        Color32[] colors = pixels.GetPixels32();
        int minX = Size, minY = Size, maxX = -1, maxY = -1;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            if (colors[y * Size + x].a < 8) continue;
            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }
        if (maxX < minX || maxY < minY) return;

        float pixelCenterX = (minX + maxX + 1) * 0.5f;
        float pixelCenterY = (minY + maxY + 1) * 0.5f;
        float worldPerPixelY = camera.orthographicSize * 2f / Size;
        float worldPerPixelX = worldPerPixelY * camera.aspect;
        Vector3 shift = camera.transform.right * ((pixelCenterX - Size * 0.5f) * worldPerPixelX) +
                        camera.transform.up * ((pixelCenterY - Size * 0.5f) * worldPerPixelY);
        camera.transform.position += shift;

        const float targetCoverage = 0.80f;
        float visiblePixels = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
        camera.orthographicSize *= Mathf.Max(0.12f, visiblePixels / (Size * targetCoverage));
        camera.Render();
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
        pixels.Apply(false, false);
    }

    private static Light CreateLight(string name, Vector3 euler, float intensity, Color color)
    {
        GameObject host = new GameObject(name);
        host.transform.rotation = Quaternion.Euler(euler);
        Light light = host.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.cullingMask = 1 << PortraitLayer;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayer(child, layer);
    }

    private static void ConfigureImport(int index)
    {
        string path = $"{OutputFolder}/{CharacterCatalog.NameOf(index)}Portrait.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = Size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void AssignDefinitions()
    {
        CharacterDefinition[] definitions = Resources.LoadAll<CharacterDefinition>("Characters/Definitions");
        foreach (CharacterDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.characterName)) continue;
            definition.portraitPath = $"UI/Portraits/{definition.characterName}Portrait";
            EditorUtility.SetDirty(definition);
        }
    }
}
