using System.IO;
using Gameplay.Combat;
using UnityEditor;
using UnityEngine;

/// <summary>Materializa las doce fichas de habilidad y las enlaza a los seis personajes.</summary>
public static class AbilityAssetGenerator
{
    private const string Folder = "Assets/Resources/Abilities";
    private static readonly string[] CharacterKeys =
    {
        "Heliandra", "Lunara", "Solmara", "Quietmor", "Acatheria", "Terramor"
    };

    [MenuItem("Blighted Blossoms/Regenerate Ability Assets")]
    public static void GenerateAll()
    {
        EnsureFolder();
        AbilityDefinition[,] assets = new AbilityDefinition[CharacterKeys.Length, 2];

        for (int character = 0; character < CharacterKeys.Length; character++)
        {
            for (int slotIndex = 0; slotIndex < 2; slotIndex++)
            {
                AbilitySlot slot = (AbilitySlot)slotIndex;
                AbilityDefinition source = AbilityCatalog.GetFallback(character, slot);
                string suffix = slot == AbilitySlot.Basic ? "Basic" : "Ultimate";
                string path = $"{Folder}/{CharacterKeys[character]}_{suffix}.asset";
                AbilityDefinition target = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                if (target == null)
                {
                    target = ScriptableObject.CreateInstance<AbilityDefinition>();
                    AssetDatabase.CreateAsset(target, path);
                }

                Copy(source, target);
                target.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(target);
                assets[character, slotIndex] = target;
            }
        }

        foreach (CharacterDefinition definition in Resources.LoadAll<CharacterDefinition>("Characters/Definitions"))
        {
            int index = Mathf.Clamp(definition.sortOrder, 0, CharacterKeys.Length - 1);
            definition.basicAbility = assets[index, (int)AbilitySlot.Basic];
            definition.ultimateAbility = assets[index, (int)AbilitySlot.Ultimate];
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AbilityAssetGenerator] 12 fichas configurables generadas y enlazadas.");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources", "Abilities");
    }

    private static void Copy(AbilityDefinition source, AbilityDefinition target)
    {
        target.abilityId = source.abilityId;
        target.spanishName = source.spanishName;
        target.englishName = source.englishName;
        target.slot = source.slot;
        target.shape = source.shape;
        target.range = source.range;
        target.radius = source.radius;
        target.coneAngle = source.coneAngle;
        target.castDelay = source.castDelay;
        target.damage = source.damage;
        target.cooldown = source.cooldown;
        target.hostileEffect = source.hostileEffect;
        target.hostileEffectDuration = source.hostileEffectDuration;
        target.hostileEffectStrength = source.hostileEffectStrength;
        target.alliedEffect = source.alliedEffect;
        target.alliedEffectDuration = source.alliedEffectDuration;
        target.alliedEffectStrength = source.alliedEffectStrength;
        target.vfxColor = source.vfxColor;
    }
}
