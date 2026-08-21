using System.Collections;
using UnityEngine;

namespace BlightedBlossoms.Gameplay.Vfx
{
    /// <summary>Runtime controller for the Visual Ray Attack prefab delivered by the team.</summary>
    public sealed class RayStrikeVfx : MonoBehaviour
    {
        private Renderer[] renderers;
        private ParticleSystem[] particleSystems;
        private Transform cylinder;
        private Material runtimeMaterial;
        private Coroutine execution;
        private float duration = 0.25f;
        private float radiusInitial = 0.1f;
        private float radiusMax = 2.5f;
        private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float Duration => duration;

        public void Configure(float totalDuration, float initialRadius, float maximumRadius, Color color)
        {
            duration = Mathf.Max(0.05f, totalDuration);
            radiusInitial = Mathf.Max(0.05f, initialRadius);
            radiusMax = Mathf.Max(radiusInitial, maximumRadius);
            Prepare(color);
        }

        public void MoveTo(Vector3 worldPosition) => transform.position = worldPosition;

        public void ShowAim(bool visible)
        {
            Prepare(new Color(1f, 0.74f, 0.47f, 0.9f));
            SetVisible(visible);
        }

        public void Execute()
        {
            Prepare(new Color(1f, 0.74f, 0.47f, 0.9f));
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

            if (particleSystems != null)
                foreach (ParticleSystem particles in particleSystems)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetVisible(false);
        }

        private void Prepare(Color color)
        {
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
            if (particleSystems == null) particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            if (cylinder == null)
            {
                foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
                {
                    if (!candidate.name.Equals("Cylinder", System.StringComparison.OrdinalIgnoreCase)) continue;
                    cylinder = candidate;
                    break;
                }
            }

            if (runtimeMaterial == null)
            {
                runtimeMaterial = PowerVfxUtility.CreateTransparentMaterial(color);
                foreach (Renderer item in renderers) item.sharedMaterial = runtimeMaterial;
            }
        }

        private IEnumerator Animate()
        {
            SetVisible(true);
            foreach (ParticleSystem particles in particleSystems) particles.Play();
            Vector3 originalScale = cylinder != null ? cylinder.localScale : Vector3.one;

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(radiusInitial, radiusMax, scaleCurve.Evaluate(t));
                if (cylinder != null)
                    cylinder.localScale = new Vector3(radius, originalScale.y, radius);
                yield return null;
            }

            if (cylinder != null) cylinder.localScale = originalScale;
            foreach (ParticleSystem particles in particleSystems)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            SetVisible(false);
            execution = null;
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null) return;
            foreach (Renderer item in renderers) item.enabled = visible;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
