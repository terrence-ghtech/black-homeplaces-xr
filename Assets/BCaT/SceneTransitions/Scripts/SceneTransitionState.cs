public static class SceneTransitionState
{
    public const string MainHouseSceneName = "BH_XR_MainScene";
    public const string BlackKitchenSceneName = "BlackKitchen_MemoryScene";
    public const string LoadingSceneName = "LoadingScene";
    public const string BlackKitchenEntrySpawnId = "BlackKitchenEntry";
    public const string MainHouseKitchenReturnSpawnId = "MainHouseKitchenReturn";

    public static string DestinationSceneName { get; private set; }
    public static string DestinationSpawnId { get; private set; }
    public static string SourceSceneName { get; private set; }
    public static bool IsTransitionInProgress { get; private set; }
    public static string LastError { get; private set; }

    public static bool RequestTransition(string destinationScene, string destinationSpawnId, string sourceSceneName = "")
    {
        if (string.IsNullOrWhiteSpace(destinationScene))
        {
            LastError = "A scene transition was requested without a destination scene.";
            return false;
        }

        if (IsTransitionInProgress)
        {
            LastError = $"A scene transition to '{DestinationSceneName}' is already in progress.";
            return false;
        }

        DestinationSceneName = destinationScene.Trim();
        DestinationSpawnId = string.IsNullOrWhiteSpace(destinationSpawnId) ? string.Empty : destinationSpawnId.Trim();
        SourceSceneName = string.IsNullOrWhiteSpace(sourceSceneName) ? string.Empty : sourceSceneName.Trim();
        LastError = string.Empty;
        IsTransitionInProgress = true;
        return true;
    }

    public static void ClearRequest()
    {
        DestinationSceneName = string.Empty;
        DestinationSpawnId = string.Empty;
        SourceSceneName = string.Empty;
        LastError = string.Empty;
        IsTransitionInProgress = false;
    }

    public static void CancelTransition(string error = "")
    {
        LastError = string.IsNullOrWhiteSpace(error) ? string.Empty : error;
        DestinationSceneName = string.Empty;
        DestinationSpawnId = string.Empty;
        SourceSceneName = string.Empty;
        IsTransitionInProgress = false;
    }
}
