using System;
using UnityEngine;

public enum GameLanguage { Spanish = 0, English = 1 }

/// <summary>Single language preference used by menus, matchmaking and HUD.</summary>
public static class GameLocalization
{
    private const string PreferenceKey = "GameLanguage";
    public static event Action LanguageChanged;

    public static GameLanguage Current =>
        (GameLanguage)PlayerPrefs.GetInt(PreferenceKey, (int)GameLanguage.Spanish);

    public static bool IsSpanish => Current == GameLanguage.Spanish;
    public static string Choose(string spanish, string english) => IsSpanish ? spanish : english;

    public static void Set(GameLanguage language)
    {
        if (Current == language && PlayerPrefs.HasKey(PreferenceKey)) return;
        PlayerPrefs.SetInt(PreferenceKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }
}
