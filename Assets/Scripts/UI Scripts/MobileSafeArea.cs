using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MobileSafeArea : MonoBehaviour
{
    private readonly Dictionary<RectTransform, Vector2> basePositions = new();
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private Canvas canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        InstallForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForScene(scene);
    }

    private static void InstallForScene(Scene scene)
    {
        if (scene.name != "Movement") return;
        Canvas target = FindFirstObjectByType<Canvas>();
        if (target != null && target.GetComponent<MobileSafeArea>() == null)
            target.gameObject.AddComponent<MobileSafeArea>();
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        foreach (RectTransform child in transform)
            basePositions[child] = child.anchoredPosition;
        Apply();
    }

    private void Update()
    {
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            Apply();
    }

    private void Apply()
    {
        Rect safe = Screen.safeArea;
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        float left = safe.xMin / scale;
        float right = (Screen.width - safe.xMax) / scale;
        float bottom = safe.yMin / scale;
        float top = (Screen.height - safe.yMax) / scale;

        foreach (KeyValuePair<RectTransform, Vector2> item in basePositions)
        {
            if (item.Key == null) continue;
            Vector2 offset = Vector2.zero;
            if (item.Key.anchorMax.x <= 0.5f) offset.x += left;
            else if (item.Key.anchorMin.x >= 0.5f) offset.x -= right;
            if (item.Key.anchorMax.y <= 0.5f) offset.y += bottom;
            else if (item.Key.anchorMin.y >= 0.5f) offset.y -= top;
            item.Key.anchoredPosition = item.Value + offset;
        }

        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
