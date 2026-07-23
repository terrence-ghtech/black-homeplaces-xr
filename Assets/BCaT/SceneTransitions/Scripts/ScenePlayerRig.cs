using UnityEngine;

public sealed class ScenePlayerRig : MonoBehaviour
{
    public enum RigKind
    {
        Desktop,
        XR
    }

    [SerializeField] private RigKind kind;

    public RigKind Kind => kind;
}
