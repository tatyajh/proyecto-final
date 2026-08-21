using System.Collections;
using BlightedBlossoms.Gameplay.Vfx;
using UnityEngine;

/// <summary>
/// Selects a reusable power presentation without coupling combat to the old
/// Character tests scene. Damage and targeting remain authoritative elsewhere.
/// </summary>
public sealed class CharacterPowerVfx : MonoBehaviour
{
    private const string QuietmorName = "Quietmor";
    private const string AcatheriaName = "Acatheria";
    private const string RayPrefabPath = "Vfx/Powers/VisualRayAttack";

    public static void Play(
        Vector3 origin,
        Vector3 direction,
        bool ultimate,
        int characterIndex,
        float range)
    {
#if UNITY_EDITOR
        Debug.Log($"[CharacterPowerVfx] {(ultimate ? "Definitiva" : "Ataque")} " +
                  $"del personaje {characterIndex} ejecutado.");
#endif
        Vector3 normalizedDirection = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector3.forward;
        GameObject host = new GameObject(ultimate ? "Ultimate Power VFX" : "Basic Power VFX");
        host.transform.position = origin + normalizedDirection * Mathf.Max(1f, range);
        CharacterPowerVfx effect = host.AddComponent<CharacterPowerVfx>();
        effect.StartCoroutine(effect.Run(ultimate, characterIndex));
    }

    private IEnumerator Run(bool ultimate, int characterIndex)
    {
        string characterName = CharacterCatalog.NameOf(characterIndex);
        if (ultimate && characterName == QuietmorName)
            yield return PlayRayStrike();
        else if (characterName == AcatheriaName)
            yield return PlayPoisonZone(ultimate);
        else
            yield return PlayAreaExplosion(ultimate, characterIndex);

        Destroy(gameObject);
    }

    private IEnumerator PlayRayStrike()
    {
        GameObject template = Resources.Load<GameObject>(RayPrefabPath);
        GameObject visual = template != null
            ? Instantiate(template, transform.position, Quaternion.identity, transform)
            : new GameObject("Ray Strike Visual");

        if (template == null)
        {
            visual.transform.SetParent(transform, false);
            Debug.LogWarning($"[CharacterPowerVfx] No se encontró Resources/{RayPrefabPath}; " +
                             "el combate continúa sin el arte del rayo.");
        }

        RayStrikeVfx ray = visual.GetComponent<RayStrikeVfx>() ?? visual.AddComponent<RayStrikeVfx>();
        // La última iteración del equipo hace que el impacto nazca concentrado
        // y se expanda, en lugar de encogerse. Conservamos ese comportamiento
        // dentro del componente URP reutilizable, sin traer el script de prueba.
        ray.Configure(0.25f, 0.1f, 2.5f, new Color(1f, 0.74f, 0.47f, 0.9f));
        ray.Execute();
        yield return new WaitForSeconds(ray.Duration + 0.1f);
    }

    private IEnumerator PlayPoisonZone(bool ultimate)
    {
        PoisonZoneVfx poison = gameObject.AddComponent<PoisonZoneVfx>();
        poison.Configure(
            ultimate ? 3.2f : 1.35f,
            ultimate ? 1.5f : 0.65f,
            new Color(0.38f, 0.72f, 0.22f, 0.72f));
        poison.Execute();
        yield return new WaitForSeconds(poison.Duration + 0.1f);
    }

    private IEnumerator PlayAreaExplosion(bool ultimate, int characterIndex)
    {
        Color color = ultimate
            ? new Color(0.78f, 0.48f, 0.12f, 0.78f)
            : CharacterCatalog.TintOf(characterIndex);
        color.a = ultimate ? 0.78f : 0.68f;

        AreaExplosionVfx explosion = gameObject.AddComponent<AreaExplosionVfx>();
        explosion.Configure(
            ultimate ? 2.8f : 1.25f,
            ultimate ? 0.65f : 0.35f,
            ultimate ? 60 : 24,
            color);
        explosion.Execute();
        yield return new WaitForSeconds(explosion.Duration + 0.1f);
    }
}
