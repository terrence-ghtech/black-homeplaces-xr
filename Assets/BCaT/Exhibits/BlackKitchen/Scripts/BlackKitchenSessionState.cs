public static class BlackKitchenSessionState
{
    public static bool ExitReflectionPlayed { get; private set; }

    public static void MarkExitReflectionPlayed()
    {
        ExitReflectionPlayed = true;
    }

    public static void ResetForTesting()
    {
        ExitReflectionPlayed = false;
    }
}
