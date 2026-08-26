using System.Collections;
using UnityEngine;

/// <summary>
/// Blanco físico y feedback del capullo. La autoridad de vida permanece en
/// ArenaPowerUpManager, pero el capullo ya no es una decoración sin collider.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArenaBudTarget : MonoBehaviour
{
    private Renderer targetRenderer;
    private Coroutine flashRoutine;

    public void Configure(ArenaPowerUpManager owner, int index, Renderer renderer)
    {
        targetRenderer = renderer;
        gameObject.name = $"Blanco de capullo corrupto {index + 1}";
    }

    public void FlashHit()
    {
        if (!isActiveAndEnabled || targetRenderer == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(block);
        Color flash = new Color(1f, 0.78f, 0.46f, 1f);
        block.SetColor("_BaseColor", flash);
        block.SetColor("_Color", flash);
        block.SetColor("_EmissionColor", flash * 1.8f);
        targetRenderer.SetPropertyBlock(block);
        yield return new WaitForSeconds(0.12f);
        if (targetRenderer != null) targetRenderer.SetPropertyBlock(null);
        flashRoutine = null;
    }
}
