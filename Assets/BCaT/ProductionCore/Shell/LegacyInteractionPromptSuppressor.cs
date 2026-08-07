using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Hides obsolete object-attached activation prompts so the shared
    /// bottom-of-view prompt is the only activation prompt surface.
    ///
    /// Identification is EXPLICIT: a text is suppressed only when it is the
    /// output target of a <see cref="PlatformInteractionPrompt"/> component —
    /// the legacy component whose entire purpose was writing
    /// "&lt;verb&gt;&lt;suffix&gt;" activation instructions onto exhibit canvases.
    ///
    /// Deliberately NOT used as suppression signals:
    ///   * GameObject or ancestor names ("Prompt", "Tooltip", "HoverPrompt", …).
    ///     An earlier revision walked ancestor names and disabled any TMP text
    ///     underneath a match. That hid real curatorial content whose objects
    ///     merely happened to be named "…Prompt" — the Sewing Room exhibit
    ///     panel ("In My Sister's Room" / Maurika Smutherman / Sound
    ///     Installation) and the "Nine Night and Good Mourning" panel became
    ///     blank purple canvases that way.
    ///   * Text content sniffing. Curatorial copy may legitimately contain any
    ///     wording; only the owning component decides what is a prompt.
    ///
    /// Also never deactivates GameObjects: only the prompt text component is
    /// disabled, so sibling artwork, backgrounds, and content on the same
    /// object survive.
    ///
    /// The two sanctioned world-space prompt systems (Front Home Privacy Zones
    /// hologram, Black Kitchen entrance) register themselves with
    /// <see cref="WorldInteractionPromptVisual"/> and are skipped by identity.
    /// </summary>
    public sealed class LegacyInteractionPromptSuppressor : MonoBehaviour
    {
        readonly List<TMP_Text> suppressed = new List<TMP_Text>();

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(SuppressAfterSceneLoad());
        }

        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(SuppressAfterSceneLoad());

        IEnumerator SuppressAfterSceneLoad()
        {
            // One frame for scene objects to run OnEnable (PlatformInteractionPrompt
            // resolves its target text there), then a second pass a moment later
            // because that component re-applies itself while XR initializes.
            yield return null;
            SuppressLegacyPrompts();
            yield return new WaitForSeconds(2f);
            SuppressLegacyPrompts();
        }

        void SuppressLegacyPrompts()
        {
            suppressed.Clear();

            foreach (var prompt in FindObjectsByType<PlatformInteractionPrompt>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prompt == null)
                    continue;

                TMP_Text text = prompt.ResolveTargetText();
                if (text == null)
                    continue;

                // Never touch a sanctioned world-space prompt.
                if (WorldInteractionPromptVisual.IsSanctioned(text) ||
                    WorldInteractionPromptVisual.IsSanctioned(text.gameObject))
                    continue;

                if (text.enabled)
                {
                    text.enabled = false;
                    suppressed.Add(text);
                }
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (suppressed.Count > 0)
            {
                Debug.Log($"[LegacyInteractionPromptSuppressor] Scene '{SceneManager.GetActiveScene().name}': " +
                          $"disabled {suppressed.Count} legacy activation prompt text(s) identified by " +
                          $"PlatformInteractionPrompt ownership.");
                foreach (var text in suppressed)
                    Debug.Log($"[LegacyInteractionPromptSuppressor]   - {Path(text.transform)}");
            }
#endif
        }

        static string Path(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
