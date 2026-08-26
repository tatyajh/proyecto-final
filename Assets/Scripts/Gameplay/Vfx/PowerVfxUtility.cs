using UnityEngine;
using UnityEngine.Rendering;

namespace BlightedBlossoms.Gameplay.Vfx
{
    internal static class PowerVfxUtility
    {
        private const string RuntimeMaterialResource = "Vfx/Powers/RayUltimate";
        public const string ParticleLibraryRoot = "Vfx/ThirdParty/KenneyParticles/";
        private static Material runtimeTemplate;

        public static Material CreateTransparentMaterial(
            Color color,
            string textureResource = null,
            bool additive = false)
        {
            // Un material guardado en Resources fuerza a WebGL a conservar el
            // shader. Shader.Find funcionaba en Editor, pero el stripping del
            // build podía eliminar todos salvo el material del rayo de
            // Quietmor, haciendo invisibles los demás poderes.
            if (runtimeTemplate == null)
                runtimeTemplate = Resources.Load<Material>(RuntimeMaterialResource);

            Material material;
            if (runtimeTemplate != null)
                material = new Material(runtimeTemplate);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                ?? Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Particles/Standard Unlit")
                                ?? Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.LogError($"[PowerVfx] No existe Resources/{RuntimeMaterialResource} ni un shader de respaldo.");
                    return null;
                }
                material = new Material(shader);
            }

            material.name = "Runtime Power VFX Material";
            material.color = color;
            material.renderQueue = (int)RenderQueue.Transparent;

            if (!string.IsNullOrWhiteSpace(textureResource))
            {
                Texture2D texture = Resources.Load<Texture2D>(textureResource);
                if (texture != null)
                {
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                }
                else
                {
                    Debug.LogWarning($"[PowerVfx] No se encontró la textura Resources/{textureResource}.");
                }
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend",
                additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        public static GameObject CreateGroundDisc(Transform parent, string name, Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            disc.transform.localScale = new Vector3(0.15f, 0.018f, 0.15f);
            Object.Destroy(disc.GetComponent<Collider>());
            disc.GetComponent<Renderer>().sharedMaterial = material;
            return disc;
        }

        public static ParticleSystem CreateParticles(
            Transform parent,
            string name,
            Color color,
            Material material,
            float radius,
            int maxParticles,
            bool falling)
        {
            GameObject host = new GameObject(name);
            host.transform.SetParent(parent, false);
            if (falling) host.transform.localPosition = new Vector3(0f, 2.5f, 0f);

            ParticleSystem system = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(falling ? 2.5f : 1.2f, falling ? 5f : 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            main.startColor = color;
            main.maxParticles = Mathf.Max(64, maxParticles);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(falling ? 90f : -90f, 0f, 0f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.92f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.15f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0f)));

            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = falling ? 0.22f : 0.10f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.18f;

            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return system;
        }

        public static void SpawnBurst(Vector3 position, Color color, int count = 28,
            float radius = 0.35f, float scale = 1f,
            string textureResource = null, bool additive = true)
        {
            GameObject root = new GameObject("Botanical magic burst");
            root.transform.position = position;
            root.transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            Material material = CreateTransparentMaterial(color, textureResource, additive);
            ParticleSystem particles = CreateParticles(root.transform, "Sparks and petals", color,
                material, radius, Mathf.Max(64, count), false);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 4.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.24f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.08f, 0.18f);
            particles.Emit(Mathf.Clamp(count, 8, 96));
            Object.Destroy(root, 1.6f);
            if (material != null) Object.Destroy(material, 1.7f);
        }
    }
}
