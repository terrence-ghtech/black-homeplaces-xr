using UnityEngine;

namespace BCaT.Production
{
    /// <summary>
    /// Marks a development aid that must never reach a player build — currently
    /// the XR Device Simulator that drives the Quest rig at the desk.
    ///
    /// Marked objects are destroyed at build time by BCaTEditorOnlyStripper, so
    /// they are absent from the player rather than shipped-and-hidden. The
    /// previous approach searched for the literal GameObject name
    /// "XR Device Simulator" at runtime and deactivated it, which shipped the
    /// object and broke silently if it was ever renamed.
    ///
    /// The component itself does nothing at runtime; it exists so the stripper
    /// and the architecture validator (BCAT-L004) can identify dev aids by
    /// identity instead of by name.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EditorOnlyObject : MonoBehaviour
    {
        [Tooltip("Why this object is development-only. Informational.")]
        [SerializeField] private string reason = "Development aid; stripped from player builds.";

        public string Reason => reason;

        void Awake()
        {
#if !UNITY_EDITOR
            // Defence in depth: if a build ever ships one of these (stripper
            // disabled, object added at runtime), it must still not run.
            Debug.LogWarning($"[EditorOnlyObject] '{name}' reached a player build and was disabled. " +
                             "The build-time stripper should have removed it.");
            gameObject.SetActive(false);
#endif
        }
    }
}
