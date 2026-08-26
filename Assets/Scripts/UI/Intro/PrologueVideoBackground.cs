using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Fondo cinematográfico del prólogo. En WebGL VideoPlayer no admite VideoClip,
/// por eso reproduce por URL desde StreamingAssets y conserva una imagen de
/// respaldo mientras prepara el primer frame o si el navegador falla.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrologueVideoBackground : MonoBehaviour
{
    private const int VideoWidth = 1280;
    private const int VideoHeight = 720;

    private GameObject root;
    private RawImage image;
    private CanvasGroup group;
    private VideoPlayer player;
    private RenderTexture target;
    private Texture2D fallback;
    private Coroutine fadeRoutine;
    private bool prepared;

    public void Build(Transform canvasRoot)
    {
        if (root != null || canvasRoot == null) return;

        root = new GameObject("Prologue Video Background", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvasRoot, false);
        root.transform.SetAsFirstSibling();
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject videoHost = new GameObject("Looping story film", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        videoHost.transform.SetParent(root.transform, false);
        RectTransform videoRect = videoHost.GetComponent<RectTransform>();
        Stretch(videoRect);
        AspectRatioFitter fitter = videoHost.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = VideoWidth / (float)VideoHeight;

        fallback = Resources.Load<Texture2D>("UI/Intro/PrologueVideoFallback");
        image = videoHost.GetComponent<RawImage>();
        image.texture = fallback;
        image.color = Color.white;
        image.raycastTarget = false;

        GameObject veilHost = new GameObject("Story contrast veil", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        veilHost.transform.SetParent(root.transform, false);
        Image veil = veilHost.GetComponent<Image>();
        veil.color = new Color(0.015f, 0.01f, 0.02f, 0.48f);
        veil.raycastTarget = false;
        Stretch(veil.rectTransform);

        target = new RenderTexture(VideoWidth, VideoHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "Prologue menu video",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        target.Create();

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.targetTexture = target;
        player.url = StreamingVideoUrl();
        player.prepareCompleted += HandlePrepared;
        player.errorReceived += HandleError;
        root.SetActive(false);
    }

    public void Play()
    {
        if (root == null || player == null) return;
        EnsureTarget();
        root.SetActive(true);
        root.transform.SetAsFirstSibling();
        image.texture = prepared ? target : fallback;
        FadeTo(1f, 0.7f, false);
        if (prepared) player.Play();
        else player.Prepare();
    }

    public void Hide()
    {
        if (root == null) return;
        player?.Pause();
        FadeTo(0f, 0.45f, true);
    }

    private void HandlePrepared(VideoPlayer source)
    {
        prepared = true;
        if (image != null) image.texture = target;
        if (root != null && root.activeInHierarchy) source.Play();
    }

    private void HandleError(VideoPlayer source, string message)
    {
        prepared = false;
        if (image != null) image.texture = fallback;
        Debug.LogWarning($"[PrologueVideo] No se pudo reproducir el video; se usa la imagen de respaldo. {message}");
    }

    private void FadeTo(float alpha, float duration, bool deactivate)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(alpha, duration, deactivate));
    }

    private IEnumerator Fade(float targetAlpha, float duration, bool deactivate)
    {
        float start = group != null ? group.alpha : 0f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (group != null) group.alpha = Mathf.Lerp(start, targetAlpha, elapsed / duration);
            yield return null;
        }
        if (group != null) group.alpha = targetAlpha;
        if (deactivate && root != null) root.SetActive(false);
        if (deactivate) ReleaseTargetWhileHidden();
        fadeRoutine = null;
    }

    private void EnsureTarget()
    {
        if (target == null) return;
        if (!target.IsCreated()) target.Create();
        if (player != null) player.targetTexture = target;
    }

    private void ReleaseTargetWhileHidden()
    {
        if (player != null)
        {
            player.Stop();
            player.targetTexture = null;
        }
        prepared = false;
        if (image != null) image.texture = fallback;
        if (target != null && target.IsCreated()) target.Release();
    }

    private static string StreamingVideoUrl()
    {
        string basePath = Application.streamingAssetsPath.TrimEnd('/', '\\');
        return $"{basePath}/Trailer/menu.mp4";
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.prepareCompleted -= HandlePrepared;
            player.errorReceived -= HandleError;
            player.Stop();
        }
        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
    }
}
