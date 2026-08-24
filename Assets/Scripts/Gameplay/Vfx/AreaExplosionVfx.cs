using System.Collections;
using UnityEngine;

namespace BlightedBlossoms.Gameplay.Vfx
{
    /// <summary>Reusable aim, execute and cancel lifecycle for circular impact powers.</summary>
    public sealed class AreaExplosionVfx : MonoBehaviour
    {
        private GameObject indicator;
        private ParticleSystem particles;
        private Material material;
        private Coroutine execution;
        private float radius = 2.8f;
        private float duration = 0.5f;
        private int particleCount = 60;
        private Color color = new Color(0.78f, 0.48f, 0.12f, 0.78f);

        public float Duration => duration;

        public void Configure(float effectRadius, float effectDuration, int particlesToEmit, Color effectColor)
        {
            radius = Mathf.Max(0.1f, effectRadius);
            duration = Mathf.Max(0.05f, effectDuration);
            particleCount = Mathf.Max(1, particlesToEmit);
            color = effectColor;
        }

        public void MoveTo(Vector3 worldPosition) => transform.position = worldPosition;

        public void ShowAim(bool visible)
        {
            Prepare();
            indicator.SetActive(visible);
            if (visible)
                indicator.transform.localScale = new Vector3(radius, 0.018f, radius);
        }

        public void Execute()
        {
            Prepare();
            Cancel();
            execution = StartCoroutine(Animate());
        }

        public void Cancel()
        {
            if (execution != null)
            {
                StopCoroutine(execution);
                execution = null;
            }

            if (indicator != null) indicator.SetActive(false);
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Prepare()
        {
            if (material == null) material = PowerVfxUtility.CreateTransparentMaterial(color);
            if (indicator == null)
                indicator = PowerVfxUtility.CreateGroundDisc(transform, "Impact Area", material);
            if (particles == null)
                particles = PowerVfxUtility.CreateParticles(
                    transform, "Explosion Particles", color, material, radius * 0.55f, particleCount, false);
        }

        private IEnumerator Animate()
        {
            ShowAim(true);
            particles.Emit(particleCount);
            Color initialColor = color;

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.15f, radius, Mathf.SmoothStep(0f, 1f, t));
                float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
                indicator.transform.localScale = new Vector3(scale * pulse, 0.018f, scale * pulse);

                Color faded = initialColor;
                faded.a *= 1f - t;
                material.color = faded;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", faded);
                yield return null;
            }

            indicator.SetActive(false);
            execution = null;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
