using System.Collections;
using UnityEngine;

namespace BlightedBlossoms.Gameplay.Vfx
{
    /// <summary>Reusable persistent zone adapted from the teammate poison prototype.</summary>
    public sealed class PoisonZoneVfx : MonoBehaviour
    {
        private GameObject indicator;
        private ParticleSystem fallingParticles;
        private ParticleSystem risingParticles;
        private Material material;
        private Coroutine execution;
        private float radius = 2.8f;
        private float duration = 1.15f;
        private Color color = new Color(0.38f, 0.72f, 0.22f, 0.72f);

        public float Duration => duration;

        public void Configure(float effectRadius, float effectDuration, Color effectColor)
        {
            radius = Mathf.Max(0.1f, effectRadius);
            duration = Mathf.Max(0.05f, effectDuration);
            color = effectColor;
        }

        public void MoveTo(Vector3 worldPosition) => transform.position = worldPosition;

        public void ShowAim(bool visible)
        {
            Prepare();
            indicator.SetActive(visible);
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
            if (fallingParticles != null) fallingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (risingParticles != null) risingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Prepare()
        {
            if (material == null) material = PowerVfxUtility.CreateTransparentMaterial(color);
            if (indicator == null)
                indicator = PowerVfxUtility.CreateGroundDisc(transform, "Poison Zone", material);
            if (fallingParticles == null)
                fallingParticles = PowerVfxUtility.CreateParticles(
                    transform, "Poison Fall", color, material, radius, 80, true);
            if (risingParticles == null)
                risingParticles = PowerVfxUtility.CreateParticles(
                    transform, "Poison Rise", color, material, radius, 80, false);
        }

        private IEnumerator Animate()
        {
            ShowAim(true);
            fallingParticles.Play();
            risingParticles.Play();

            ParticleSystem.EmissionModule fallingEmission = fallingParticles.emission;
            fallingEmission.rateOverTime = 30f;
            ParticleSystem.EmissionModule risingEmission = risingParticles.emission;
            risingEmission.rateOverTime = 20f;

            yield return new WaitForSeconds(duration);
            fallingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            risingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            indicator.SetActive(false);
            execution = null;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
