using UnityEngine;

public enum MatchModeId
{
    Duel1v1 = 0,
    Duo2v2 = 1,
    Clash3v3 = 2
}

/// <summary>
/// Static description of one PvP format. Everything that changes between
/// 1v1, 2v2 and 3v3 lives here so the rest of the flow stays format agnostic.
/// </summary>
public sealed class MatchModeDefinition
{
    public readonly MatchModeId Id;
    public readonly string Key;
    private readonly string displayNameSpanish;
    private readonly string displayNameEnglish;
    private readonly string taglineSpanish;
    private readonly string taglineEnglish;
    public readonly int TeamSize;

    public string DisplayName => GameLocalization.Choose(displayNameSpanish, displayNameEnglish);
    public string Tagline => GameLocalization.Choose(taglineSpanish, taglineEnglish);

    public MatchModeDefinition(MatchModeId id, string key, string displayNameSpanish, string displayNameEnglish, string taglineSpanish, string taglineEnglish, int teamSize)
    {
        Id = id;
        Key = key;
        this.displayNameSpanish = displayNameSpanish;
        this.displayNameEnglish = displayNameEnglish;
        this.taglineSpanish = taglineSpanish;
        this.taglineEnglish = taglineEnglish;
        TeamSize = teamSize;
    }

    /// <summary>Total players Photon must gather before the combat starts.</summary>
    public int PlayerCount => TeamSize * MatchTeams.TeamCount;
}

public static class MatchModeCatalog
{
    public static readonly MatchModeDefinition Duel = new MatchModeDefinition(
        MatchModeId.Duel1v1, "1v1", "DUELO 1V1", "DUEL 1V1", "Dos jugadores en línea", "Two players online", 1);

    public static readonly MatchModeDefinition Duo = new MatchModeDefinition(
        MatchModeId.Duo2v2, "2v2", "DÚO 2V2", "DUO 2V2", "Cuatro jugadores · dos por equipo", "Four players · two per team", 2);

    public static readonly MatchModeDefinition Clash = new MatchModeDefinition(
        MatchModeId.Clash3v3, "3v3", "CHOQUE 3V3", "CLASH 3V3", "Seis jugadores · tres por equipo", "Six players · three per team", 3);

    public static readonly MatchModeDefinition[] All = { Duel, Duo, Clash };

    public static MatchModeDefinition Default => Duel;

    public static MatchModeDefinition Get(MatchModeId id)
    {
        foreach (MatchModeDefinition mode in All)
            if (mode.Id == id) return mode;

        return Default;
    }

    public static MatchModeDefinition GetByKey(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            string normalized = key.Trim().ToLowerInvariant();
            foreach (MatchModeDefinition mode in All)
                if (mode.Key == normalized) return mode;
        }

        return Default;
    }
}

/// <summary>
/// Mode chosen in the lobby, readable from the arena scene. It is kept both in a
/// static field and in PlayerPrefs so a domain reload between scenes cannot lose it.
/// </summary>
public static class MatchContext
{
    private const string PrefsKey = "SelectedMatchMode";

    private static MatchModeDefinition current;

    public static MatchModeDefinition Mode
    {
        get
        {
            if (current == null)
                current = MatchModeCatalog.Get((MatchModeId)PlayerPrefs.GetInt(PrefsKey, (int)MatchModeId.Duel1v1));

            return current;
        }
    }

    public static int TeamSize => Mode.TeamSize;
    public static int RequiredPlayers => Mode.PlayerCount;

    public static void Select(MatchModeDefinition mode)
    {
        current = mode ?? MatchModeCatalog.Default;
        PlayerPrefs.SetInt(PrefsKey, (int)current.Id);
        PlayerPrefs.Save();
    }

    public static void Reset() => Select(MatchModeCatalog.Default);
}

/// <summary>
/// Team identity shared by the lobby, the spawner and the combat rules.
/// </summary>
public static class MatchTeams
{
    public const int TeamCount = 2;
    public const int Bloom = 0;
    public const int Blight = 1;

    private static readonly Color BloomColor = new Color(0.30f, 0.64f, 0.45f);
    private static readonly Color BlightColor = new Color(0.76f, 0.30f, 0.42f);

    /// <summary>
    /// Provisional team used the moment a player spawns. The master client
    /// rebalances every team once the room is full, so this only has to be
    /// deterministic and roughly even.
    /// </summary>
    public static int TeamForPlayerId(int playerId) => Mathf.Abs(playerId) % TeamCount;

    /// <summary>Position inside the team line, distinct for consecutive ids of the same team.</summary>
    public static int SlotForPlayerId(int playerId, int teamSize)
    {
        if (teamSize <= 1) return 0;
        return (Mathf.Abs(playerId) / TeamCount) % teamSize;
    }

    public static int Opponent(int team) => team == Bloom ? Blight : Bloom;

    public static Color ColorOf(int team) => team == Blight ? BlightColor : BloomColor;

    public static string NameOf(int team) => team == Blight
        ? GameLocalization.Choose("Marchitez", "Blight")
        : GameLocalization.Choose("Floración", "Bloom");

    /// <summary>
    /// Both teams face each other along X. Each slot is offset along Z so a
    /// 2v2 or 3v3 line does not spawn stacked on the same point.
    /// </summary>
    public static Vector3 SpawnOffset(int team, int slot, int teamSize)
    {
        float side = team == Blight ? 1f : -1f;
        float lateral = (slot - (teamSize - 1) * 0.5f) * 3.5f;
        return new Vector3(side * 5f, 0f, lateral);
    }

    public static Quaternion SpawnRotation(int team)
    {
        return Quaternion.LookRotation(team == Blight ? Vector3.left : Vector3.right);
    }
}
