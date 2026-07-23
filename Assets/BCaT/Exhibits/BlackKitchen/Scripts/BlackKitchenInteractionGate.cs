using UnityEngine;

public static class BlackKitchenInteractionGate
{
    private static int frame = -1;
    private static int selectedInstanceId;
    private static float selectedDistance = float.PositiveInfinity;

    public static void RegisterCandidate(MonoBehaviour candidate, float distance)
    {
        if (candidate == null)
            return;

        if (frame != Time.frameCount)
        {
            frame = Time.frameCount;
            selectedInstanceId = 0;
            selectedDistance = float.PositiveInfinity;
        }

        if (distance < selectedDistance)
        {
            selectedInstanceId = candidate.GetInstanceID();
            selectedDistance = distance;
        }
    }

    public static bool IsSelected(MonoBehaviour candidate)
    {
        return candidate != null && frame == Time.frameCount && selectedInstanceId == candidate.GetInstanceID();
    }
}
