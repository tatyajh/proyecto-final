using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// Catálogo de respaldo para las doce habilidades iniciales. Las fichas de
    /// personaje pueden reemplazar cualquier entrada con un asset editable sin
    /// cambiar el código de red.
    /// </summary>
    public static class AbilityCatalog
    {
        private static AbilityDefinition[,] fallback;

        public static AbilityDefinition GetFallback(int characterIndex, AbilitySlot slot)
        {
            if (fallback == null) fallback = BuildFallback();
            int character = Mathf.Clamp(characterIndex, 0, fallback.GetLength(0) - 1);
            return fallback[character, (int)slot];
        }

        private static AbilityDefinition[,] BuildFallback()
        {
            AbilityDefinition[,] result = new AbilityDefinition[6, 2];

            result[0, 0] = Make("heliandra_root", "Raíz de Alba", "Dawn Root", AbilitySlot.Basic,
                AbilityShape.Line, 8, 3f, 6f, 0.65f, CombatEffectKind.Root, 1.25f, 0f,
                CombatEffectKind.None, 0f, 0f, new Color(0.93f, 0.88f, 0.62f));
            result[0, 1] = Make("heliandra_last_dawn", "Flor del Último Alba", "Last Dawn Blossom", AbilitySlot.Ultimate,
                AbilityShape.Area, 0, 15f, 7f, 3.2f, CombatEffectKind.Blind, 2f, 3.5f,
                CombatEffectKind.Shield, 4f, 35f, new Color(0.95f, 0.82f, 0.42f));

            result[1, 0] = Make("lunara_needle", "Aguja Arcana", "Arcane Needle", AbilitySlot.Basic,
                AbilityShape.Line, 8, 3f, 6.5f, 0.55f, CombatEffectKind.Reveal, 3f, 0f,
                CombatEffectKind.None, 0f, 0f, new Color(0.48f, 0.25f, 1f));
            result[1, 1] = Make("lunara_oracle", "Oráculo de Medianoche", "Midnight Oracle", AbilitySlot.Ultimate,
                AbilityShape.Area, 20, 15f, 7f, 3f, CombatEffectKind.Slow, 3f, 0.45f,
                CombatEffectKind.None, 0f, 0f, new Color(0.34f, 0.12f, 0.82f));

            result[2, 0] = Make("solmara_ray", "Rayo Solar Marchito", "Withered Sun Ray", AbilitySlot.Basic,
                AbilityShape.Line, 8, 3f, 7f, 0.55f, CombatEffectKind.None, 0f, 0f,
                CombatEffectKind.None, 0f, 0f, new Color(1f, 0.62f, 0.12f));
            result[2, 1] = Make("solmara_eclipse", "Eclipse de Polen", "Pollen Eclipse", AbilitySlot.Ultimate,
                AbilityShape.Area, 20, 15f, 7f, 3.1f, CombatEffectKind.None, 0f, 0f,
                CombatEffectKind.None, 0f, 0f, new Color(0.95f, 0.42f, 0.12f), 0.85f);

            result[3, 0] = Make("quietmor_chime", "Tañido Mudo", "Mute Chime", AbilitySlot.Basic,
                AbilityShape.Cone, 8, 3f, 5.5f, 0.8f, CombatEffectKind.Silence, 1.5f, 0f,
                CombatEffectKind.None, 0f, 0f, new Color(0.58f, 0.30f, 0.76f));
            result[3, 1] = Make("quietmor_requiem", "Réquiem sin Eco", "Echo-less Requiem", AbilitySlot.Ultimate,
                AbilityShape.Area, 20, 15f, 7f, 3.2f, CombatEffectKind.Silence, 3f, 0f,
                CombatEffectKind.None, 0f, 0f, new Color(0.34f, 0.12f, 0.48f));

            result[4, 0] = Make("acatheria_claw", "Garra Espinosa", "Thorn Claw", AbilitySlot.Basic,
                AbilityShape.Cone, 8, 3f, 4.5f, 0.75f, CombatEffectKind.None, 0f, 0f,
                CombatEffectKind.Haste, 2f, 0.3f, new Color(0.42f, 0.82f, 0.36f));
            result[4, 1] = Make("acatheria_leap", "Salto Depredador", "Predator Leap", AbilitySlot.Ultimate,
                AbilityShape.Leap, 20, 15f, 7f, 2.2f, CombatEffectKind.Knockback, 0f, 3.5f,
                CombatEffectKind.None, 0f, 0f, new Color(0.85f, 0.12f, 0.34f));

            result[5, 0] = Make("terramor_charge", "Embate de Raíz", "Root Charge", AbilitySlot.Basic,
                AbilityShape.Dash, 8, 3f, 5.5f, 1.1f, CombatEffectKind.Stun, 1f, 3f,
                CombatEffectKind.None, 0f, 0f, new Color(0.62f, 0.38f, 0.20f));
            result[5, 1] = Make("terramor_bastion", "Bastión de la Fosa", "Pit Bastion", AbilitySlot.Ultimate,
                AbilityShape.Wall, 0, 15f, 6f, 3.5f, CombatEffectKind.Knockback, 0f, 2.5f,
                CombatEffectKind.None, 0f, 0f, new Color(0.48f, 0.31f, 0.18f));

            return result;
        }

        private static AbilityDefinition Make(string id, string spanish, string english, AbilitySlot slot,
            AbilityShape shape, int damage, float cooldown, float range, float radius,
            CombatEffectKind hostileEffect, float hostileDuration, float hostileStrength,
            CombatEffectKind alliedEffect, float alliedDuration, float alliedStrength, Color color,
            float castDelay = 0f, AudioClip castSfx = null)
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.hideFlags = HideFlags.HideAndDontSave;
            ability.abilityId = id;
            ability.spanishName = spanish;
            ability.englishName = english;
            ability.slot = slot;
            ability.shape = shape;
            ability.damage = damage;
            ability.cooldown = cooldown;
            ability.range = range;
            ability.radius = radius;
            ability.castDelay = castDelay;
            ability.hostileEffect = hostileEffect;
            ability.hostileEffectDuration = hostileDuration;
            ability.hostileEffectStrength = hostileStrength;
            ability.alliedEffect = alliedEffect;
            ability.alliedEffectDuration = alliedDuration;
            ability.alliedEffectStrength = alliedStrength;
            ability.vfxColor = color;
            ability.castSfx = castSfx;
            return ability;
        }
    }
}
