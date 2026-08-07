using System;
using System.IO;
using System.Text;
using BCaT.Production.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the Rhythm and Rope jump rope exhibit prefab and stages it in the
/// front yard for review.
///
/// No new interaction or link-launching code exists for this exhibit: the
/// prefab uses the project's existing <see cref="InteractableLinkLauncher"/>,
/// which already implements IInteractionTarget, registers with the
/// InteractionRouter, formats prompts through SharedInteractionPrompt, and owns
/// the single Application.OpenURL call. Desktop input arrives from the router's
/// DesktopInteractionInputProvider and Quest select arrives through
/// XRSimpleInteractable.selectEntered -> OpenLink -> RequestXRSelect, so the
/// exhibit script itself never reads the keyboard.
///
/// Prefab and staging group are outputs: edit this builder and re-run the menu
/// items rather than hand-editing them.
/// </summary>
public static class RhythmAndRopeExhibitBuilder
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string ExhibitRoot = "Assets/BCaT/Exhibits/RhythmAndRope";
    private const string PrefabRoot = ExhibitRoot + "/Prefabs";
    private const string PrefabPath = PrefabRoot + "/RhythmAndRope_JumpRope.prefab";

    // Actual asset path/spelling on disk ("rhythm_n_rope"), not the folder name
    // used in the brief. Imported assets are deliberately not renamed.
    private const string ModelPath = "Assets/BCaT_assets/rhythm_n_rope/jump_rope.glb";

    private const string Creator = "Diamond Beverly-Porter";
    private const string ProjectTitle = "Rhythm and Rope";
    private const string TargetUrl = "https://diamondebp.itch.io/rhythm-and-rope";
    private const string DesktopPrompt = "Press E to Explore Rhythm and Rope";
    private const string XrPrompt = "Interact to Explore Rhythm and Rope";

    private const string StagingRootName = "TEST_RhythmAndRope_FrontYard";

    /// <summary>Largest model dimension in metres after normalization.</summary>
    private const float ModelTargetSize = 0.6f;

    private const float MinColliderSize = 0.4f;
    private const float InteractDistance = 3.5f;

    // Front yard, clear of the staged Adinkra row (that row is at z 134,
    // x 159.9 - 175.9). This sits ~2.2 m south of the row and ~2.9 m from the
    // nearest symbol so neither exhibit overlaps or competes for router focus.
    private static readonly Vector3 StagePosition = new Vector3(162f, 0f, 131.8f);
    private const float PlinthHeight = 1.0f;
    private static readonly Vector3 PlinthFootprint = new Vector3(0.5f, PlinthHeight, 0.5f);

    [MenuItem("BCaT/Rhythm and Rope/Build Jump Rope Prefab")]
    public static void BuildPrefab()
    {
        EnsureFolders();
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

        var log = new StringBuilder("[RhythmAndRope] Prefab build\n");

        GameObject root = new GameObject("RhythmAndRope_JumpRope");

        // The jump rope itself is the interaction target.
        GameObject model = AddNormalizedModel(root.transform, ModelPath, out Bounds scaledBounds,
            out Vector3 nativeSize);
        log.AppendLine($"  model '{Path.GetFileName(ModelPath)}' native {nativeSize.x:F3} x {nativeSize.y:F3} x " +
                       $"{nativeSize.z:F3} m -> {scaledBounds.size.x:F3} x {scaledBounds.size.y:F3} x " +
                       $"{scaledBounds.size.z:F3} m");

        // Non-trigger collider: XRI ray interactors in this project use
        // m_RaycastTriggerInteraction = Ignore, so Quest select needs solid geometry.
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.center = scaledBounds.center;
        collider.size = new Vector3(
            Mathf.Max(scaledBounds.size.x, MinColliderSize),
            Mathf.Max(scaledBounds.size.y, MinColliderSize),
            Mathf.Max(scaledBounds.size.z, MinColliderSize));

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        InteractableLinkLauncher launcher = root.AddComponent<InteractableLinkLauncher>();
        ConfigureLauncher(launcher);

        AddXrSelect(root, launcher);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine($"  saved {PrefabPath}");
        log.AppendLine($"  url={TargetUrl}");
        log.AppendLine($"  desktopPrompt='{DesktopPrompt}' xrPrompt='{XrPrompt}'");
        Debug.Log(log.ToString());
    }

    [MenuItem("BCaT/Rhythm and Rope/Stage Jump Rope In Front Yard")]
    public static void StageFrontYard()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find(StagingRootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new FileNotFoundException(
                "Missing RhythmAndRope_JumpRope.prefab — run BCaT/Rhythm and Rope/Build Jump Rope Prefab first.");

        GameObject stagingRoot = new GameObject(StagingRootName);
        stagingRoot.transform.position = Vector3.zero;

        float groundY = SampleGroundY(StagePosition.x, StagePosition.z, 4.86f);

        // Display furniture lives in the staging group, never in the prefab, so
        // the jump rope can be dropped anywhere in the house later.
        GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plinth.name = "TEMP_ReviewPlinth";
        plinth.transform.SetParent(stagingRoot.transform, false);
        plinth.transform.localScale = PlinthFootprint;
        plinth.transform.position = new Vector3(StagePosition.x, groundY + PlinthHeight * 0.5f, StagePosition.z);
        Renderer plinthRenderer = plinth.GetComponent<Renderer>();
        plinthRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material shared = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/BCaT/Exhibits/Adinkra/Materials/Adinkra_Plinth.mat");
        if (shared != null)
            plinthRenderer.sharedMaterial = shared;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "RhythmAndRope_JumpRope";
        instance.transform.SetParent(stagingRoot.transform, false);
        instance.transform.position = new Vector3(StagePosition.x, groundY + PlinthHeight, StagePosition.z);
        // Facing/orientation is intentionally left at identity for manual art direction.
        instance.transform.rotation = Quaternion.identity;

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);

        Debug.Log($"[RhythmAndRope] Front yard staging ({StagingRootName}) " +
                  $"x={StagePosition.x:F2} z={StagePosition.z:F2} ground={groundY:F2} " +
                  $"ropeY={groundY + PlinthHeight:F2} sceneSaved={saved}");
    }

    [MenuItem("BCaT/Rhythm and Rope/Build And Stage Everything")]
    public static void BuildAndStage()
    {
        BuildPrefab();
        StageFrontYard();
    }

    // ---- Configuration ---------------------------------------------------

    /// <summary>
    /// Writes every serialized field explicitly. Fields absent from serialized
    /// data deserialize to default(T) rather than their C# initializer, so
    /// allowDesktop/allowQuest must be written or the platform gate in
    /// InteractableLinkLauncher.IsAvailable would read false on both platforms.
    /// </summary>
    private static void ConfigureLauncher(InteractableLinkLauncher launcher)
    {
        SerializedObject so = new SerializedObject(launcher);

        so.FindProperty("displayName").stringValue = Creator;
        so.FindProperty("projectName").stringValue = ProjectTitle;

        so.FindProperty("targetUrl").stringValue = TargetUrl;
        so.FindProperty("openBehavior").enumValueIndex =
            (int)InteractableLinkLauncher.OpenBehavior.ExternalUrl;

        so.FindProperty("allowDesktop").boolValue = true;
        so.FindProperty("allowQuest").boolValue = true;

        SerializedProperty prompt = so.FindProperty("prompt");
        prompt.FindPropertyRelative("desktopPrompt").stringValue = DesktopPrompt;
        prompt.FindPropertyRelative("xrPrompt").stringValue = XrPrompt;
        prompt.FindPropertyRelative("verb").enumValueIndex = (int)SharedInteractionVerb.Open;
        // objectName is set so the shared fallback stays correct ("...to open
        // Rhythm and Rope") if the explicit prompts are ever cleared, instead of
        // falling through to displayName and naming the creator.
        prompt.FindPropertyRelative("objectName").stringValue = ProjectTitle;

        so.FindProperty("playerCamera").objectReferenceValue = null;
        so.FindProperty("interactDistance").floatValue = InteractDistance;
        // No floating world prompt: the router's shared prompt UI shows the text
        // (same convention as the Linda Leaks housing map link exhibit).
        so.FindProperty("promptText").objectReferenceValue = null;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- Model -----------------------------------------------------------

    /// <summary>
    /// Instantiates the GLB and normalizes its largest dimension to
    /// <see cref="ModelTargetSize"/> with its base at the parent origin.
    /// Rotation is left at identity — artistic orientation is set manually.
    /// </summary>
    private static GameObject AddNormalizedModel(Transform parent, string assetPath,
        out Bounds scaledBounds, out Vector3 nativeSize)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            throw new FileNotFoundException("Missing jump rope model: " + assetPath);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = "JumpRope_Model";
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        if (!TryGetRendererBounds(instance, out Bounds native))
        {
            Debug.LogWarning($"[RhythmAndRope] '{assetPath}' has no renderers; model left unscaled.");
            nativeSize = Vector3.zero;
            scaledBounds = new Bounds(Vector3.zero, Vector3.one * MinColliderSize);
            return instance;
        }

        nativeSize = native.size;
        float largest = Mathf.Max(native.size.x, Mathf.Max(native.size.y, native.size.z));
        float scale = largest > 0.0001f ? ModelTargetSize / largest : 1f;

        instance.transform.localScale = Vector3.one * scale;
        TryGetRendererBounds(instance, out Bounds scaled);
        instance.transform.localPosition = new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);

        TryGetRendererBounds(instance, out scaledBounds);
        scaledBounds = new Bounds(parent.InverseTransformPoint(scaledBounds.center), scaledBounds.size);
        return instance;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    // ---- XR wiring -------------------------------------------------------

    /// <summary>
    /// Wires XRSimpleInteractable.selectEntered to the launcher's existing
    /// OpenLink entry point, which routes through InteractionRouter.RequestXRSelect.
    /// </summary>
    private static void AddXrSelect(GameObject target, InteractableLinkLauncher launcher)
    {
        Type type = Type.GetType(
                        "UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit")
                    ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
        if (type == null)
        {
            Debug.LogWarning("[RhythmAndRope] XRSimpleInteractable type unavailable; Quest select not wired.");
            return;
        }

        Component interactable = target.GetComponent(type) ?? target.AddComponent(type);
        object selectEntered = type.GetProperty("selectEntered")?.GetValue(interactable)
                               ?? type.GetField("selectEntered")?.GetValue(interactable)
                               ?? type.GetField("m_SelectEntered",
                                       System.Reflection.BindingFlags.Instance |
                                       System.Reflection.BindingFlags.NonPublic)
                                   ?.GetValue(interactable);

        if (selectEntered is UnityEventBase unityEvent)
        {
            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(unityEvent, i);
            UnityEventTools.AddVoidPersistentListener(unityEvent, launcher.OpenLink);
            EditorUtility.SetDirty(interactable);
        }
        else
        {
            Debug.LogWarning("[RhythmAndRope] Could not resolve XRSimpleInteractable.selectEntered; Quest select not wired.");
        }
    }

    // ---- Helpers ---------------------------------------------------------

    private static void EnsureFolders()
    {
        foreach (string folder in new[] { ExhibitRoot, ExhibitRoot + "/Editor", PrefabRoot })
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
        }
    }

    /// <summary>
    /// Ground height for the staging slot. Starts below Boundary_Top (y 13.28)
    /// and skips the invisible boundary shells and any previous staging pass.
    /// </summary>
    private static float SampleGroundY(float x, float z, float fallback)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Ray(new Vector3(x, 12f, z), Vector3.down), 40f, ~0,
            QueryTriggerInteraction.Ignore);

        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null ||
                hit.collider.name.StartsWith("Boundary", StringComparison.Ordinal) ||
                IsOwnStagingCollider(hit.collider))
                continue;
            if (hit.point.y > best)
                best = hit.point.y;
        }

        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    private static bool IsOwnStagingCollider(Collider collider)
    {
        for (Transform t = collider.transform; t != null; t = t.parent)
        {
            if (t.name == StagingRootName)
                return true;
        }

        return false;
    }
}
