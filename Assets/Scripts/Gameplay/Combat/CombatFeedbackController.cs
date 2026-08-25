using System.Collections;
using TMPro;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// Presentación de impactos sin tocar Time.timeScale ni la simulación de
    /// Fusion. Cada cliente reproduce el mismo pulso; la cámara solo se mueve
    /// para el jugador local golpeado.
    /// </summary>
    public static class CombatFeedbackController
    {
        private static AudioClip impactA;
        private static AudioClip impactB;

        public static void PresentHit(PlayerController target, int damage, Vector3 direction)
        {
            if (target == null) return;
            FeedbackRunner runner = target.GetComponent<FeedbackRunner>();
            if (runner == null) runner = target.gameObject.AddComponent<FeedbackRunner>();
            runner.Play(damage, direction);

            impactA ??= Resources.Load<AudioClip>("Audio/SFX/combat_impact_01");
            impactB ??= Resources.Load<AudioClip>("Audio/SFX/combat_impact_02");
            AudioClip clip = Random.value < 0.5f ? impactA : impactB;
            if (clip != null) AudioCatalog.PlayOneShot(clip, target.transform.position);
        }

        private sealed class FeedbackRunner : MonoBehaviour
        {
            private PlayerController target;

            public void Play(int damage, Vector3 direction)
            {
                target ??= GetComponent<PlayerController>();
                StartCoroutine(Pulse(damage, direction));
            }

            private IEnumerator Pulse(int damage, Vector3 direction)
            {
                Bounds bounds = CalculateBounds();
                if (damage > 0) CreateDamageNumber(bounds.center, damage);

                GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flash.name = "Impact flash";
                Collider collider = flash.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                flash.transform.position = bounds.center;
                flash.transform.localScale = bounds.size * 0.68f;

                Renderer renderer = flash.GetComponent<Renderer>();
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                Material material = new Material(shader)
                {
                    color = new Color(1f, 0.78f, 0.45f, 0.42f)
                };
                renderer.sharedMaterial = material;

                target?.PauseCharacterPresentation(0.07f);
                if (target != null && target.HasLocalControl)
                {
                    MobaCamera camera = FindFirstObjectByType<MobaCamera>();
                    Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                    if (camera != null)
                        camera.AddImpulse((-planar + Vector3.up * 0.25f) * 0.28f);
                }

                float elapsed = 0f;
                const float duration = 0.16f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float ratio = Mathf.Clamp01(elapsed / duration);
                    Color color = material.color;
                    color.a = 0.42f * (1f - ratio);
                    material.color = color;
                    flash.transform.localScale = bounds.size * Mathf.Lerp(0.68f, 0.9f, ratio);
                    yield return null;
                }
                Destroy(flash);
                Destroy(material);
            }

            private void CreateDamageNumber(Vector3 position, int damage)
            {
                GameObject root = new GameObject("Damage number", typeof(TextMeshPro));
                root.transform.position = position + Vector3.up * 0.8f;
                TextMeshPro text = root.GetComponent<TextMeshPro>();
                text.text = $"-{damage}";
                text.fontSize = 4.5f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(1f, 0.82f, 0.45f, 1f);
                text.outlineWidth = 0.22f;
                text.outlineColor = new Color32(30, 10, 18, 255);
                root.AddComponent<FloatingDamageText>();
            }

            private Bounds CalculateBounds()
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
                bool found = false;
                Bounds bounds = new Bounds(transform.position + Vector3.up * 2f, Vector3.one * 3f);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                return bounds;
            }
        }

        private sealed class FloatingDamageText : MonoBehaviour
        {
            private float elapsed;
            private Color initial;
            private TextMeshPro label;

            private void Awake()
            {
                label = GetComponent<TextMeshPro>();
                initial = label.color;
            }

            private void LateUpdate()
            {
                elapsed += Time.unscaledDeltaTime;
                transform.position += Vector3.up * (1.8f * Time.unscaledDeltaTime);
                Camera camera = Camera.main;
                if (camera != null) transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);
                Color color = initial;
                color.a = 1f - Mathf.Clamp01(elapsed / 0.85f);
                label.color = color;
                if (elapsed >= 0.85f) Destroy(gameObject);
            }
        }
    }
}
