public enum PlayMode
{
    LocalStory,
    Training,
    Multiplayer
}

public static class PlayModeContext
{
    public static PlayMode Current { get; private set; } = PlayMode.LocalStory;

    public static void UseLocalStory() => Current = PlayMode.LocalStory;
    public static void UseTraining() => Current = PlayMode.Training;
    public static void UseMultiplayer() => Current = PlayMode.Multiplayer;
}
