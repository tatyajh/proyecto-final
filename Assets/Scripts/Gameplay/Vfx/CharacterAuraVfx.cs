using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlightedBlossoms.Gameplay.Vfx
{
    /// <summary>
    /// Aura temporal reutilizable para habilidades especiales. Conserva los
    /// materiales PBR del personaje y añade emisión/tinte sobre copias runtime;
    /// al terminar restaura exactamente los materiales compartidos originales.
    /// </summary>
    public sealed class CharacterAuraVfx : MonoBehaviour
    {
        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] Originals;
            public Material[] RuntimeCopies;
        }

        private readonly List<RendererState> states = new List<RendererState>();
        private Coroutine activeRoutine;

        public static void Play(GameObject target, Color color, float duration)
        {
            if (target == null) return;
            CharacterAuraVfx aura = target.GetComponent<CharacterAuraVfx>() ??
                                     target.AddComponent<CharacterAuraVfx>();
            aura.Execute(color, duration);
        }

        public void Execute(Color color, float duration)
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            Restore();
            CaptureAndApply(color);
            activeRoutine = StartCoroutine(RestoreAfter(Mathf.Max(0.1f, duration)));
        }

        private void CaptureAndApply(Color auraColor)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                    continue;

                Material[] originals = renderer.sharedMaterials;
                if (originals == null || originals.Length == 0) continue;

                Material[] copies = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    Material source = originals[i];
                    if (source == null) continue;

                    Material copy = new Material(source)
                    {
                        name = source.name + " (Aura Runtime)",
                        globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive
                    };

                    Color emission = new Color(auraColor.r * 1.8f, auraColor.g * 1.8f,
                        auraColor.b * 1.8f, 1f);
                    if (copy.HasProperty("_EmissionColor"))
                    {
                        copy.SetColor("_EmissionColor", emission);
                        copy.EnableKeyword("_EMISSION");
                    }
                    else if (copy.HasProperty("_BaseColor"))
                    {
                        copy.SetColor("_BaseColor", Color.Lerp(copy.GetColor("_BaseColor"), auraColor, 0.16f));
                    }
                    else if (copy.HasProperty("_Color"))
                    {
                        copy.SetColor("_Color", Color.Lerp(copy.GetColor("_Color"), auraColor, 0.16f));
                    }

                    copies[i] = copy;
                }

                states.Add(new RendererState
                {
                    Renderer = renderer,
                    Originals = originals,
                    RuntimeCopies = copies
                });
                renderer.sharedMaterials = copies;
            }
        }

        private IEnumerator RestoreAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            activeRoutine = null;
            Restore();
            Destroy(this);
        }

        private void Restore()
        {
            foreach (RendererState state in states)
            {
                if (state.Renderer != null) state.Renderer.sharedMaterials = state.Originals;
                if (state.RuntimeCopies == null) continue;
                foreach (Material material in state.RuntimeCopies)
                    if (material != null) Destroy(material);
            }
            states.Clear();
        }

        private void OnDestroy() => Restore();
    }
}
