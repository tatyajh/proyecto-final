public enum OnlineMatchPhase
{
    Offline,
    Connecting,
    WaitingForOpponent,
    Playing,
    Finished,
    OpponentDisconnected,
    ConnectionFailed
}

public static class OnlineMatchState
{
    public static OnlineMatchPhase Phase { get; private set; } = OnlineMatchPhase.Offline;
    public static string Message { get; private set; } = string.Empty;
    public static bool CanPlay => Phase == OnlineMatchPhase.Playing;

    public static void Set(OnlineMatchPhase phase, string message)
    {
        Phase = phase;
        Message = message ?? string.Empty;
    }

    public static void Reset()
    {
        Set(OnlineMatchPhase.Offline, string.Empty);
    }
}
