using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates the five separated Black Kitchen audio stations, the interaction manager,
// and validates that no two station trigger bounds intersect. Runable headless:
//   Unity -batchmode -quit -executeMethod BlackKitchenAudioStationBuilder.RebuildStations
public static class BlackKitchenAudioStationBuilder
{
    private const string ScenePath = "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";
    private const string AudioPath = "Assets/BCaT/Exhibits/BlackKitchen/Audio/";
    private const string ExperienceRootName = "BlackKitchenExperience_ROOT";
    private const string StationsRootName = "BlackKitchenAudioStations";
    private const float OverlapTolerance = 0.01f;

    private static readonly Vector3 TriggerSize = new Vector3(0.9f, 2f, 0.9f);
    private static readonly Vector3 TriggerCenter = new Vector3(0f, 1f, 0f);

    private struct StationDefinition
    {
        public string ObjectName;
        public string NarrativeId;
        public string DisplayName;
        public string ClipFile;
        public Vector3 Position;
        public float Volume;
    }

    private static readonly StationDefinition[] Stations =
    {
        new() { ObjectName = "CulturalBackgroundInteraction", NarrativeId = "cultural_background", DisplayName = "Cultural Background", ClipFile = "cultural_background.mp3", Position = new Vector3(-1.2f, 0f, -3.2f), Volume = 0.6f },
        new() { ObjectName = "KitchenConversationInteraction", NarrativeId = "kitchen_conversation", DisplayName = "Kitchen Conversation", ClipFile = "kitchen_conversation.mp3", Position = new Vector3(1.0f, 0f, 0.5f), Volume = 0.7f },
        new() { ObjectName = "RiceAndBeansInteraction", NarrativeId = "rice_and_bean_pot", DisplayName = "Rice and Beans Story", ClipFile = "rice_and_bean_pot.mp3", Position = new Vector3(-1.54f, 0f, 2.23f), Volume = 0.9f },
        new() { ObjectName = "BirthdayCakeInteraction", NarrativeId = "birthday_cake", DisplayName = "Birthday Cake Story", ClipFile = "birthday_cake.mp3", Position = new Vector3(0f, 0f, 3.3f), Volume = 0.85f },
        new() { ObjectName = "NieceCakeInteraction", NarrativeId = "niece_cake", DisplayName = "Niece Cake Story", ClipFile = "niece_cake.mp3", Position = new Vector3(1.9f, 0f, 3.3f), Volume = 0.65f },
    };

    private static readonly string[] LegacyObjectNames =
    {
        "RiceBeansPotInteraction", "OvenInteraction", "AmbientConversationZone", "CulturalBackgroundAudio",
        StationsRootName, "BlackKitchenInteractionManager"
    };

    [MenuItem("BCaT/Black Kitchen/Rebuild Audio Stations")]
    public static void RebuildStations()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find(ExperienceRootName);
        if (root == null)
            throw new System.InvalidOperationException($"'{ExperienceRootName}' not found in {ScenePath}.");

        BlackKitchenAudioCoordinator coordinator = Object.FindAnyObjectByType<BlackKitchenAudioCoordinator>();
        BlackKitchenExperienceController controller = Object.FindAnyObjectByType<BlackKitchenExperienceController>();
        if (coordinator == null || controller == null)
            throw new System.InvalidOperationException("Coordinator or experience controller missing from the Black Kitchen scene.");

        foreach (string legacyName in LegacyObjectNames)
        {
            Transform legacy = root.transform.Find(legacyName);
            if (legacy != null)
            {
                Debug.Log($"[BlackKitchenAudioStationBuilder] Removing legacy object '{legacyName}'.");
                Object.DestroyImmediate(legacy.gameObject);
            }
        }

        // Legacy serialized references would otherwise point at deleted objects.
        SetPrivate(coordinator, "kitchenConversationSource", null);
        SetPrivate(coordinator, "culturalBackgroundSource", null);

        CreateStationsAndManager(root.transform, coordinator, controller);

        if (!ValidateSeparationInternal(out string report))
            throw new System.InvalidOperationException("Black Kitchen audio station triggers overlap:\n" + report);

        Debug.Log("[BlackKitchenAudioStationBuilder] Station report:\n" + report);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BlackKitchenAudioStationBuilder] Rebuild complete.");
    }

    [MenuItem("BCaT/Black Kitchen/Validate Audio Station Separation")]
    public static void ValidateStationSeparation()
    {
        if (!ValidateSeparationInternal(out string report))
            throw new System.InvalidOperationException("Black Kitchen audio station triggers overlap:\n" + report);

        Debug.Log("[BlackKitchenAudioStationBuilder] Separation valid.\n" + report);
    }

    public static void CreateStationsAndManager(Transform experienceRoot, BlackKitchenAudioCoordinator coordinator, BlackKitchenExperienceController controller)
    {
        GameObject stationsRoot = new GameObject(StationsRootName);
        stationsRoot.transform.SetParent(experienceRoot, false);

        foreach (StationDefinition definition in Stations)
        {
            GameObject stationObject = new GameObject(definition.ObjectName);
            stationObject.transform.SetParent(stationsRoot.transform, false);
            stationObject.transform.localPosition = definition.Position;
            stationObject.transform.localRotation = Quaternion.identity;
            stationObject.transform.localScale = Vector3.one;

            BoxCollider trigger = stationObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = TriggerCenter;
            trigger.size = TriggerSize;
            stationObject.AddComponent<Rigidbody>().isKinematic = true;

            GameObject focus = new GameObject("FocusTarget");
            focus.transform.SetParent(stationObject.transform, false);
            focus.transform.localPosition = new Vector3(0f, 1f, 0f);

            AudioSource source = stationObject.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + definition.ClipFile);
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0f;
            source.spatialBlend = 0.9f;
            source.minDistance = 0.9f;
            source.maxDistance = 7f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            if (source.clip == null)
                Debug.LogWarning($"[BlackKitchenAudioStationBuilder] Clip '{definition.ClipFile}' was not found for '{definition.ObjectName}'.");

            BlackKitchenAudioInteractable interactable = stationObject.AddComponent<BlackKitchenAudioInteractable>();
            interactable.Configure(definition.NarrativeId, definition.DisplayName, source.clip, source, trigger, focus.transform, coordinator, definition.Volume);

            WireXrSelect(stationObject, interactable);
        }

        GameObject managerObject = new GameObject("BlackKitchenInteractionManager");
        managerObject.transform.SetParent(experienceRoot, false);
        BlackKitchenInteractionManager manager = managerObject.AddComponent<BlackKitchenInteractionManager>();
        SetPrivate(manager, "experienceController", controller);
    }

    private static bool ValidateSeparationInternal(out string report)
    {
        BlackKitchenAudioInteractable[] interactables = Object.FindObjectsByType<BlackKitchenAudioInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<(string name, Bounds bounds)> entries = new();
        StringBuilder text = new StringBuilder();
        bool valid = true;

        foreach (BlackKitchenAudioInteractable interactable in interactables)
        {
            Collider trigger = interactable.InteractionTrigger;
            if (trigger is not BoxCollider box)
            {
                text.AppendLine($"{interactable.name}: missing or non-box trigger collider.");
                valid = false;
                continue;
            }

            Transform t = box.transform;
            if (t.lossyScale != Vector3.one || t.rotation != Quaternion.identity)
            {
                text.AppendLine($"{interactable.name}: trigger transform has scale {t.lossyScale} / rotation {t.rotation.eulerAngles}; expected identity so bounds are not distorted.");
                valid = false;
            }

            Bounds bounds = new Bounds(t.position + box.center, box.size);
            entries.Add((interactable.name, bounds));
            text.AppendLine(interactable.name);
            text.AppendLine($"Position: {t.position}");
            text.AppendLine($"Collider center: {bounds.center}");
            text.AppendLine($"Collider size: {box.size}");
        }

        for (int i = 0; i < entries.Count; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                Bounds a = entries[i].bounds;
                a.Expand(-OverlapTolerance);
                if (a.Intersects(entries[j].bounds))
                {
                    text.AppendLine($"OVERLAP: '{entries[i].name}' intersects '{entries[j].name}'.");
                    valid = false;
                }
            }
        }

        if (valid)
            text.AppendLine("No interaction trigger bounds overlap.");

        report = text.ToString();
        return valid;
    }

    private static void SetPrivate(Object target, string fieldName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void WireXrSelect(GameObject target, BlackKitchenAudioInteractable receiver)
    {
        System.Type interactableType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
        if (interactableType == null)
        {
            Debug.LogWarning("[BlackKitchenAudioStationBuilder] XRSimpleInteractable type unavailable; XR selection not wired.");
            return;
        }

        Component xrInteractable = target.AddComponent(interactableType);
        SerializedObject serialized = new SerializedObject(xrInteractable);
        SerializedProperty colliders = serialized.FindProperty("m_Colliders");
        Collider collider = target.GetComponent<Collider>();
        if (colliders != null && collider != null)
        {
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        BlackKitchenXrSelectRelay relay = target.AddComponent<BlackKitchenXrSelectRelay>();
        relay.Configure(receiver, nameof(BlackKitchenAudioInteractable.OnXRSelect));
    }
}
