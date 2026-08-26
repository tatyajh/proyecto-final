using UnityEngine;

namespace Gameplay.Combat
{
    public enum AbilitySlot
    {
        Basic = 0,
        Ultimate = 1
    }

    public enum AbilityShape
    {
        Line,
        Cone,
        Area,
        Leap,
        Dash,
        Wall
    }

    public enum CombatEffectKind
    {
        None,
        Root,
        Reveal,
        Slow,
        Silence,
        Haste,
        Knockback,
        Blind,
        Shield,
        Stun
    }

    /// <summary>
    /// Datos configurables de una habilidad. La resolución de red usa estos
    /// valores tanto en entrenamiento como en Fusion, evitando dos versiones
    /// distintas de cada ataque.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "Blighted Blossoms/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [Header("Identidad")]
        public string abilityId;
        public string spanishName;
        public string englishName;
        public AbilitySlot slot;

        [Header("Apuntado")]
        public AbilityShape shape = AbilityShape.Line;
        [Min(0.1f)] public float range = 5f;
        [Min(0.1f)] public float radius = 0.75f;
        [Range(5f, 180f)] public float coneAngle = 55f;
        [Min(0f)] public float castDelay;

        [Header("Combate")]
        [Min(0)] public int damage = 20;
        [Min(0.05f)] public float cooldown = 1f;
        public CombatEffectKind hostileEffect;
        [Min(0f)] public float hostileEffectDuration;
        [Min(0f)] public float hostileEffectStrength;
        public CombatEffectKind alliedEffect;
        [Min(0f)] public float alliedEffectDuration;
        [Min(0f)] public float alliedEffectStrength;

        [Header("Presentación")]
        public Color vfxColor = new Color(0.83f, 0.64f, 0.20f, 0.9f);
        [Tooltip("Textura del icono dentro de Resources, sin extensión. Se usa en HUD de PC y móvil.")]
        public string iconResourcePath;
        public AudioClip castSfx;
        [Tooltip("Telegráfico opcional. Si está vacío se genera uno procedural según la forma.")]
        public GameObject telegraphPrefab;
        [Tooltip("Proyectil o cuerpo principal opcional del poder.")]
        public GameObject projectilePrefab;
        [Tooltip("Impacto opcional. Si está vacío se usa la identidad procedural del personaje.")]
        public GameObject impactPrefab;
        [Tooltip("Trigger de Animator. Vacío usa attack/ultimate según el slot.")]
        public string castAnimationTrigger;
        public AudioClip impactSfx;

        public string DisplayName => GameLocalization.Choose(spanishName, englishName);

        public Texture2D LoadIcon() => string.IsNullOrWhiteSpace(iconResourcePath)
            ? null
            : Resources.Load<Texture2D>(iconResourcePath);
    }
}
