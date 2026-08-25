using System;

public enum LocalCombatantRole
{
    Human,
    TrainingBot
}

/// <summary>
/// Identidad local equivalente a los datos que Fusion replica en una partida.
/// Permite que entrenamiento use el mismo PlayerController sin fingir que el
/// bot tiene autoridad de input ni leer la selección global de PlayerPrefs.
/// </summary>
[Serializable]
public struct LocalCombatantConfig
{
    public int CharacterIndex;
    public int TeamId;
    public int CombatantId;
    public string DisplayName;
    public LocalCombatantRole Role;

    public static LocalCombatantConfig Human(int characterIndex, string displayName)
    {
        return new LocalCombatantConfig
        {
            CharacterIndex = CharacterCatalog.Clamp(characterIndex),
            TeamId = MatchTeams.Bloom,
            CombatantId = 0,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName,
            Role = LocalCombatantRole.Human
        };
    }

    public static LocalCombatantConfig Bot(int characterIndex)
    {
        int safeIndex = CharacterCatalog.Clamp(characterIndex);
        return new LocalCombatantConfig
        {
            CharacterIndex = safeIndex,
            TeamId = MatchTeams.Blight,
            CombatantId = 1,
            DisplayName = CharacterCatalog.NameOf(safeIndex),
            Role = LocalCombatantRole.TrainingBot
        };
    }
}
