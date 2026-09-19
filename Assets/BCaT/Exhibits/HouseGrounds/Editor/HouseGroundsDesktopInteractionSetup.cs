using System;
using BCaT.Production.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates the Desktop-only well target as a scene sibling of the frozen Quest prefab.</summary>
public static class HouseGroundsDesktopInteractionSetup
{
    const string ScenePath = "Assets/BH_XR_MainScene.unity";
    const string QuestInstanceName = "HouseGroundsExhibit";
    const string DesktopInstanceName = "HouseGroundsExhibitDesktop";
    const string PrefabPath = "Assets/BCaT/Exhibits/HouseGrounds/Prefabs/HouseGroundsExhibit.prefab";

    [MenuItem("BCaT/House Grounds/Install Desktop Well Interaction")]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform well = FindBackyardWell(scene);
        Transform questTransform = well != null ? well.Find(QuestInstanceName) : null;
        HouseGroundsExhibitController questController = questTransform != null
            ? questTransform.GetComponent<HouseGroundsExhibitController>() : null;
        BoxCollider questCollider = questTransform != null ? questTransform.GetComponent<BoxCollider>() : null;

        if (well == null || questTransform == null || questController == null || questCollider == null)
            throw new InvalidOperationException("The existing Quest House Grounds object and collider must exist beneath the backyard well.");

        Transform existing = well.Find(DesktopInstanceName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        GameObject desktopObject = new(DesktopInstanceName);
        desktopObject.transform.SetParent(well, false);
        desktopObject.transform.localPosition = questTransform.localPosition;
        desktopObject.transform.localRotation = questTransform.localRotation;
        desktopObject.transform.localScale = questTransform.localScale;

        BoxCollider desktopCollider = desktopObject.AddComponent<BoxCollider>();
        desktopCollider.center = questCollider.center;
        desktopCollider.size = questCollider.size;
        desktopCollider.isTrigger = true;

        HouseGroundsDesktopInteraction desktopInteraction = desktopObject.AddComponent<HouseGroundsDesktopInteraction>();
        desktopInteraction.Configure(questController, desktopCollider, well);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[HouseGroundsDesktopSetup] Installed Desktop-only target beneath the backyard well.");
    }

    [MenuItem("BCaT/House Grounds/Validate Desktop Well Interaction")]
    public static void Validate()
    {
        GameObject questPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (questPrefab == null || questPrefab.GetComponent<HouseGroundsExhibitController>() == null)
            throw new InvalidOperationException("House Grounds Quest prefab is missing its controller.");
        if (questPrefab.GetComponentInChildren<HouseGroundsDesktopInteraction>(true) != null)
            throw new InvalidOperationException("The Quest House Grounds prefab must not contain the Desktop interaction target.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform well = FindBackyardWell(scene);
        Transform questTransform = well != null ? well.Find(QuestInstanceName) : null;
        Transform desktopTransform = well != null ? well.Find(DesktopInstanceName) : null;
        if (questTransform == null || desktopTransform == null || desktopTransform.parent != well)
            throw new InvalidOperationException("Quest and Desktop House Grounds objects must be separate direct children of the backyard well.");

        HouseGroundsExhibitController questController = questTransform.GetComponent<HouseGroundsExhibitController>();
        BoxCollider desktopCollider = desktopTransform.GetComponent<BoxCollider>();
        HouseGroundsDesktopInteraction desktopInteraction = desktopTransform.GetComponent<HouseGroundsDesktopInteraction>();
        if (questController == null || desktopCollider == null || !desktopCollider.enabled || !desktopCollider.isTrigger ||
            desktopInteraction == null || desktopInteraction.Priority != 2 ||
            desktopInteraction.GetPrompt(false) != "Press E to view House and Grounds")
            throw new InvalidOperationException("Desktop House Grounds target is not configured for universal router interaction.");

        SerializedObject data = new(desktopInteraction);
        if (data.FindProperty("houseGrounds").objectReferenceValue != questController ||
            data.FindProperty("interactionCollider").objectReferenceValue != desktopCollider ||
            data.FindProperty("wellColliderRoot").objectReferenceValue != well)
            throw new InvalidOperationException("Desktop House Grounds target is not wired to the existing Quest popup controller and well hierarchy.");

        Debug.Log("[HouseGroundsDesktopValidation] PASS — separate Desktop target, trigger, router prompt, and Quest popup reference are wired.");
    }

    static Transform FindBackyardWell(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "well" && candidate.Find("Env_Well_01") != null)
                    return candidate;
        return null;
    }
}
