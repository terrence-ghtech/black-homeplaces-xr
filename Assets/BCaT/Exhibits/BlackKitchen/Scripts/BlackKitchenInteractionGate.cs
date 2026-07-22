using UnityEngine;

public static class BlackKitchenInteractionGate
{
    private static int frame = -1;
    private static MonoBehaviour selected;
    private static float selectedDistance = float.PositiveInfinity;

    public static void RegisterCandidate(MonoBehaviour candidate, float distance)
    {
        if (candidate == null)
            return;

        if (frame != Time.frameCount)
        {
            frame = Time.frameCount;
            selected = null;
            selectedDistance = float.PositiveInfinity;
        }

        if (distance < selectedDistance)
        {
            selected = candidate;
            selectedDistance = distance;
        }
    }

    public static bool IsSelected(MonoBehaviour candidate)
    {
        return candidate != null && frame == Time.frameCount && selected == candidate;
    }
}
