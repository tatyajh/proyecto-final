using System.Collections;
using UnityEngine;

/// <summary>Objetivo local reutilizable para verificar daño, alcance y VFX sin otro jugador.</summary>
public sealed class DestructiblePracticeTarget : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 60;
    private int health;
    private Renderer targetRenderer;
    private Color restColor;
    private Vector3 restScale;
    private Coroutine feedback;

    public int CurrentHealth => health;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        health = maxHealth;
        targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null) restColor = targetRenderer.sharedMaterial.color;
        restScale = transform.localScale;
    }

    public void Configure(int healthPoints, Color color)
    {
        maxHealth = Mathf.Max(1, healthPoints);
        health = maxHealth;
        restColor = color;
        restScale = transform.localScale;
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null) targetRenderer.material.color = restColor;
    }

    public void ApplyDamage(int damage)
    {
        health = Mathf.Max(0, health - Mathf.Max(0, damage));
        if (feedback != null) StopCoroutine(feedback);
        feedback = StartCoroutine(PlayHitFeedback());
        if (health <= 0)
        {
            StartCoroutine(DestroyAfterFeedback());
            return;
        }
    }

    private IEnumerator PlayHitFeedback()
    {
        if (targetRenderer != null) targetRenderer.material.color = MenuTheme.GiltBright;
        transform.localScale = restScale * 0.88f;
        yield return new WaitForSeconds(0.12f);
        if (targetRenderer != null) targetRenderer.material.color = restColor;
        transform.localScale = restScale;
        feedback = null;
    }

    private IEnumerator DestroyAfterFeedback()
    {
        yield return new WaitForSeconds(0.13f);
        Destroy(gameObject);
    }
}
