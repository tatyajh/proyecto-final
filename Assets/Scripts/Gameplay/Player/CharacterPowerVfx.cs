using System.Collections;
using System.Collections.Generic;
using BlightedBlossoms.Gameplay.Vfx;
using Gameplay.Combat;
using UnityEngine;

/// <summary>
/// Presentación procedural por personaje. La resolución del combate sigue en
/// PlayerController; telegráfico, viaje e impacto parten del mismo CastOrigin.
/// </summary>
public sealed class CharacterPowerVfx : MonoBehaviour
{
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private AbilityDefinition ability;
    private GameObject actor;
    private Vector3 origin;
    private Vector3 direction;
    private Vector3 destination;
    private int characterIndex;

    public static void Play(GameObject actor, AbilityDefinition ability, Vector3 origin,
        Vector3 direction, int characterIndex, float range)
    {
        if (ability == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = actor != null ? actor.transform.forward : Vector3.forward;
        forward.Normalize();

        GameObject host = new GameObject($"{CharacterCatalog.NameOf(characterIndex)} · {ability.DisplayName}");
        CharacterPowerVfx effect = host.AddComponent<CharacterPowerVfx>();
        effect.actor = actor;
        effect.ability = ability;
        effect.origin = origin;
        effect.direction = forward;
        effect.destination = origin + forward * Mathf.Max(1f, range);
        effect.destination.y = actor != null ? actor.transform.position.y + 0.10f : 0.10f;
        effect.characterIndex = CharacterCatalog.Clamp(characterIndex);
        effect.StartCoroutine(effect.Run());
    }

    private IEnumerator Run()
    {
        Color accent = IdentityAccent();
        PowerVfxUtility.SpawnBurst(origin, accent,
            ability.slot == AbilitySlot.Ultimate ? 34 : 20, 0.22f,
            ability.slot == AbilitySlot.Ultimate ? 1.15f : 0.82f,
            IdentityMuzzleTexture());
        GameObject telegraph = CreateTelegraph();
        float warningTime = Mathf.Clamp(ability.castDelay > 0f ? ability.castDelay : 0.22f, 0.16f, 0.8f);
        yield return FadeTelegraph(telegraph, warningTime);

        if (ability.projectilePrefab != null)
        {
            GameObject projectile = Instantiate(ability.projectilePrefab, origin,
                Quaternion.LookRotation(direction), transform);
            yield return MoveProjectile(projectile.transform, origin, destination, 0.22f);
        }
        else
        {
            yield return PlayIdentityTravel();
        }

        if (ability.impactPrefab != null)
            Instantiate(ability.impactPrefab, Ground(destination), Quaternion.identity);
        else
            yield return PlayIdentityImpact();

        if (ability.impactSfx != null) AudioCatalog.PlayOneShot(ability.impactSfx, destination);
        Destroy(gameObject);
    }

    private GameObject CreateTelegraph()
    {
        if (ability.telegraphPrefab != null)
            return Instantiate(ability.telegraphPrefab, Ground(destination), Quaternion.identity, transform);

        GameObject host = new GameObject("Telegráfico");
        host.transform.SetParent(transform, false);
        LineRenderer line = host.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = ability.shape is AbilityShape.Area or AbilityShape.Leap;
        line.widthMultiplier = ability.slot == AbilitySlot.Ultimate ? 0.16f : 0.10f;
        Color color = ability.vfxColor;
        color.a = 0.72f;
        line.startColor = line.endColor = color;
        line.textureMode = LineTextureMode.Tile;
        line.sharedMaterial = NewMaterial(color, IdentityTelegraphTexture(), true);

        if (line.loop)
        {
            const int points = 48;
            line.positionCount = points;
            float effectRadius = Mathf.Max(0.8f, ability.radius);
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points;
                line.SetPosition(i, Ground(destination) +
                    new Vector3(Mathf.Cos(angle) * effectRadius, 0.08f, Mathf.Sin(angle) * effectRadius));
            }
        }
        else if (ability.shape == AbilityShape.Cone)
        {
            Quaternion left = Quaternion.Euler(0f, -ability.coneAngle * 0.5f, 0f);
            Quaternion right = Quaternion.Euler(0f, ability.coneAngle * 0.5f, 0f);
            line.positionCount = 4;
            line.SetPosition(0, Ground(origin));
            line.SetPosition(1, Ground(origin + left * direction * ability.range));
            line.SetPosition(2, Ground(origin + right * direction * ability.range));
            line.SetPosition(3, Ground(origin));
        }
        else if (ability.shape == AbilityShape.Wall)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized * Mathf.Max(1.5f, ability.radius);
            line.positionCount = 2;
            line.SetPosition(0, Ground(destination - side));
            line.SetPosition(1, Ground(destination + side));
        }
        else
        {
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, destination);
        }
        return host;
    }

    private IEnumerator FadeTelegraph(GameObject telegraph, float duration)
    {
        if (telegraph == null) yield break;
        LineRenderer line = telegraph.GetComponentInChildren<LineRenderer>();
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (line != null)
            {
                Color color = ability.vfxColor;
                color.a = Mathf.Lerp(0.32f, 0.86f, Mathf.PingPong(elapsed * 5f, 1f));
                line.startColor = line.endColor = color;
            }
            yield return null;
        }
        Destroy(telegraph);
    }

    private IEnumerator PlayIdentityTravel()
    {
        Color primary = ability.vfxColor;
        Color accent = IdentityAccent();
        int strands;
        switch (characterIndex)
        {
            case 0: strands = 5; break;
            case 1: strands = 3; break;
            case 2: strands = 4; break;
            case 3: strands = 4; break;
            case 4: strands = 3; break;
            default: strands = 7; break;
        }

        GameObject projectile = GameObject.CreatePrimitive(characterIndex == 1
            ? PrimitiveType.Capsule : PrimitiveType.Sphere);
        projectile.name = characterIndex == 5 ? "Rosa marchita" : "Cuerpo mágico";
        projectile.transform.SetParent(transform, true);
        projectile.transform.position = origin;
        projectile.transform.localScale = characterIndex == 5
            ? new Vector3(0.55f, 0.24f, 0.55f)
            : Vector3.one * (ability.slot == AbilitySlot.Ultimate ? 0.48f : 0.28f);
        Destroy(projectile.GetComponent<Collider>());
        projectile.GetComponent<Renderer>().sharedMaterial = NewMaterial(accent, IdentityCoreTexture(), true);

        for (int i = 0; i < strands; i++)
        {
            GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            petal.name = characterIndex == 5 ? "Pétalo de rosa corrupta" : "Pétalo mágico";
            petal.transform.SetParent(projectile.transform, false);
            float angle = i * 360f / strands;
            petal.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.72f;
            petal.transform.localRotation = Quaternion.Euler(35f, angle, 0f);
            petal.transform.localScale = new Vector3(0.38f, 0.16f, 0.74f);
            Destroy(petal.GetComponent<Collider>());
            petal.GetComponent<Renderer>().sharedMaterial = NewMaterial(
                Color.Lerp(primary, accent, 0.45f), IdentityBurstTexture(), true);
        }

        Material trailMaterial = NewMaterial(new Color(accent.r, accent.g, accent.b, 0.78f),
            IdentityTrailTexture(), true);
        ParticleSystem trail = PowerVfxUtility.CreateParticles(projectile.transform,
            IdentityTrailName(), accent, trailMaterial, 0.24f, 72, false);
        ParticleSystem.MainModule trailMain = trail.main;
        trailMain.simulationSpace = ParticleSystemSimulationSpace.World;
        trailMain.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
        trailMain.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.52f);
        trailMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.19f);
        ParticleSystem.EmissionModule trailEmission = trail.emission;
        trailEmission.rateOverTime = ability.slot == AbilitySlot.Ultimate ? 48f : 30f;
        trail.Play();
        yield return MoveProjectile(projectile.transform, origin, destination, 0.24f);
    }

    private IEnumerator MoveProjectile(Transform projectile, Vector3 start, Vector3 end, float duration)
    {
        if (projectile == null) yield break;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            projectile.position = Vector3.Lerp(start, end, t);
            projectile.Rotate(direction, 420f * Time.deltaTime, Space.World);
            yield return null;
        }
        if (projectile != null) Destroy(projectile.gameObject);
    }

    private IEnumerator PlayIdentityImpact()
    {
        GameObject impact = new GameObject("Impacto botánico");
        impact.transform.SetParent(transform, false);
        impact.transform.position = Ground(destination);
        Color color = ability.vfxColor;
        Color accent = IdentityAccent();

        PowerVfxUtility.SpawnBurst(Ground(destination), accent,
            ability.slot == AbilitySlot.Ultimate ? 70 : 38,
            Mathf.Max(0.32f, ability.radius * 0.18f),
            ability.slot == AbilitySlot.Ultimate ? 1.2f : 0.9f,
            IdentityBurstTexture());

        switch (characterIndex)
        {
            case 0:
                for (int root = 0; root < 6; root++)
                    CreateRoot(impact.transform, root * 60f, Color.Lerp(color, accent, 0.62f));
                break;
            case 1:
                for (int ring = 0; ring < 3; ring++)
                    CreateRing(impact.transform, ring * 0.46f, Color.Lerp(color, accent, ring * 0.25f));
                CreateRays(impact.transform, 8, accent, 2.6f, "Agujas arcanas");
                break;
            case 2:
                CreateRays(impact.transform, 10, accent, 3.2f, "Rayos solares marchitos");
                break;
            case 3:
                for (int ring = 0; ring < 4; ring++)
                    CreateRing(impact.transform, ring * 0.48f, Color.Lerp(color, accent, 0.42f));
                break;
            case 4:
                CreateRays(impact.transform, 7, accent, 2.4f, "Garras y espinas");
                for (int root = 0; root < 5; root++) CreateRoot(impact.transform, root * 72f, accent);
                break;
            default:
                for (int root = 0; root < 8; root++)
                    CreateRoot(impact.transform, root * 45f, Color.Lerp(color, accent, 0.35f));
                break;
        }

        AreaExplosionVfx explosion = impact.AddComponent<AreaExplosionVfx>();
        explosion.Configure(Mathf.Max(1.1f, ability.radius), 0.52f,
            ability.slot == AbilitySlot.Ultimate ? 72 : 34, color);
        explosion.Execute();
        if (actor != null && characterIndex == 4)
            CharacterAuraVfx.Play(actor, new Color(0.30f, 0.92f, 0.32f, 0.9f), 1.2f);
        yield return new WaitForSeconds(0.62f);
        Destroy(impact);
    }

    private void CreateRays(Transform parent, int count, Color color, float length, string label)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject host = new GameObject(label);
            host.transform.SetParent(parent, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 3;
            line.widthMultiplier = 0.10f;
            Vector3 ray = Quaternion.Euler(0f, i * 360f / count, 0f) * Vector3.forward;
            line.SetPosition(0, Vector3.up * 0.08f);
            line.SetPosition(1, ray * (length * 0.58f) + Vector3.up * 0.18f);
            line.SetPosition(2, ray * length + Vector3.up * 0.04f);
            line.startColor = new Color(color.r, color.g, color.b, 0.95f);
            line.endColor = new Color(color.r, color.g, color.b, 0.08f);
            line.textureMode = LineTextureMode.Tile;
            line.sharedMaterial = NewMaterial(color, IdentityBurstTexture(), true);
        }
    }

    private void CreateRing(Transform parent, float extraRadius, Color color)
    {
        GameObject host = new GameObject("Onda muda");
        host.transform.SetParent(parent, false);
        LineRenderer line = host.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = 36;
        line.widthMultiplier = 0.10f;
        float radius = 0.9f + extraRadius;
        for (int i = 0; i < 36; i++)
        {
            float a = i * Mathf.PI * 2f / 36f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.08f, Mathf.Sin(a) * radius));
        }
        line.startColor = line.endColor = color;
        line.textureMode = LineTextureMode.Tile;
        line.sharedMaterial = NewMaterial(color, IdentityTelegraphTexture(), true);
    }

    private void CreateRoot(Transform parent, float angle, Color color)
    {
        GameObject root = new GameObject("Raíz con rosa marchita");
        root.transform.SetParent(parent, false);
        LineRenderer line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 4;
        line.widthMultiplier = 0.18f;
        Vector3 rootDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        for (int i = 0; i < 4; i++)
            line.SetPosition(i, rootDirection * (i * 0.72f) + Vector3.up * (Mathf.Sin(i * 1.7f) * 0.12f));
        line.startColor = new Color(0.18f, 0.08f, 0.05f, 0.92f);
        line.endColor = color;
        line.textureMode = LineTextureMode.Tile;
        line.sharedMaterial = NewMaterial(color, IdentityRootTexture(), false);
    }

    private Material NewMaterial(Color color, string textureResource = null, bool additive = false)
    {
        Material material = PowerVfxUtility.CreateTransparentMaterial(color, textureResource, additive);
        runtimeMaterials.Add(material);
        return material;
    }

    private static string ParticleTexture(string name) =>
        PowerVfxUtility.ParticleLibraryRoot + name;

    private string IdentityMuzzleTexture() => characterIndex switch
    {
        0 => ParticleTexture("star_07"),
        1 => ParticleTexture("magic_03"),
        2 => ParticleTexture("light_03"),
        3 => ParticleTexture("circle_05"),
        4 => ParticleTexture("slash_02"),
        _ => ParticleTexture("magic_05")
    };

    private string IdentityCoreTexture() => characterIndex switch
    {
        0 => ParticleTexture("magic_01"),
        1 => ParticleTexture("twirl_02"),
        2 => ParticleTexture("light_03"),
        3 => ParticleTexture("circle_05"),
        4 => ParticleTexture("trace_04"),
        _ => ParticleTexture("scorch_02")
    };

    private string IdentityTrailTexture() => characterIndex switch
    {
        0 => ParticleTexture("star_07"),
        1 => ParticleTexture("magic_03"),
        2 => ParticleTexture("spark_04"),
        3 => ParticleTexture("smoke_05"),
        4 => ParticleTexture("slash_02"),
        _ => ParticleTexture("magic_05")
    };

    private string IdentityBurstTexture() => characterIndex switch
    {
        0 => ParticleTexture("star_07"),
        1 => ParticleTexture("magic_05"),
        2 => ParticleTexture("spark_04"),
        3 => ParticleTexture("circle_05"),
        4 => ParticleTexture("slash_02"),
        _ => ParticleTexture("smoke_05")
    };

    private string IdentityTelegraphTexture() => characterIndex switch
    {
        0 => ParticleTexture("symbol_02"),
        1 => ParticleTexture("magic_03"),
        2 => ParticleTexture("light_03"),
        3 => ParticleTexture("circle_05"),
        4 => ParticleTexture("slash_02"),
        _ => ParticleTexture("scorch_02")
    };

    private string IdentityRootTexture() => characterIndex == 5
        ? ParticleTexture("scorch_02")
        : IdentityTelegraphTexture();

    private Color IdentityAccent()
    {
        return characterIndex switch
        {
            0 => new Color(1f, 0.96f, 0.72f, 0.95f),
            1 => new Color(0.62f, 0.24f, 1f, 0.95f),
            2 => new Color(1f, 0.73f, 0.18f, 0.95f),
            3 => new Color(0.42f, 0.20f, 0.56f, 0.82f),
            4 => new Color(0.45f, 0.95f, 0.32f, 0.92f),
            _ => new Color(0.62f, 0.16f, 0.20f, 0.95f)
        };
    }

    private string IdentityTrailName()
    {
        return characterIndex switch
        {
            0 => "Pétalos del alba",
            1 => "Runas violetas",
            2 => "Polen solar quemado",
            3 => "Eco silenciado",
            4 => "Rastro de veneno",
            _ => "Rosas marchitas"
        };
    }

    private static Vector3 Ground(Vector3 value) => new Vector3(value.x, value.y + 0.08f, value.z);

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
            if (material != null) Destroy(material);
        runtimeMaterials.Clear();
    }
}
