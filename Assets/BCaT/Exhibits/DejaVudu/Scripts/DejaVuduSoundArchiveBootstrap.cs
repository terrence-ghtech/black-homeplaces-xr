using BCaT.Production.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Exhibits.DejaVudu
{
    /// <summary>
    /// Legacy runtime installer retained only as documentation for the original
    /// generated hierarchy. The exhibit is now serialized directly into
    /// BH_XR_MainScene, so this path must not create a runtime duplicate.
    /// </summary>
    public static class DejaVuduSoundArchiveBootstrap
    {
        const bool RuntimeInstallEnabled = false;
        const string MainSceneName = "BH_XR_MainScene";
        const string RootName = "DejaVuduSoundArchive";
        const string VisualName = "TEMP_RadioVisual_REPLACE_ME";
        const string VisualResource = "DejaVudu/TEMP_Stereo_MainUnit";
        const string ContentResource = "DejaVudu/DejaVuduSoundArchiveContent";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallForInitialScene()
        {
            if (!RuntimeInstallEnabled)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

        static void TryInstall(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MainSceneName)
                return;

            if (GameObject.Find(RootName) != null)
                return;

            TextAsset content = Resources.Load<TextAsset>(ContentResource);

            GameObject root = new GameObject(RootName);
            root.transform.SetPositionAndRotation(ResolveFrontYardPosition(), ResolveFrontYardRotation());

            GameObject visualHolder = new GameObject(VisualName);
            visualHolder.transform.SetParent(root.transform, false);
            visualHolder.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            visualHolder.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            visualHolder.transform.localScale = Vector3.one * 0.85f;

            GameObject visualPrefab = Resources.Load<GameObject>(VisualResource);
            if (visualPrefab != null)
            {
                GameObject visual = Object.Instantiate(visualPrefab, visualHolder.transform);
                visual.name = "Stereo_MainUnit_LowPolyLivingRoom";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
            }
            else
            {
                Debug.LogWarning($"[DejaVuduSoundArchive] Missing visual resource '{VisualResource}'.");
            }

            GameObject interactionTarget = new GameObject("InteractionTarget");
            interactionTarget.transform.SetParent(root.transform, false);
            interactionTarget.transform.localPosition = Vector3.zero;
            BoxCollider trigger = interactionTarget.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.76f, 0f);
            trigger.size = new Vector3(1.15f, 1.35f, 0.95f);
            Rigidbody body = interactionTarget.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            GameObject focus = new GameObject("FocusTarget");
            focus.transform.SetParent(root.transform, false);
            focus.transform.localPosition = new Vector3(0f, 0.82f, 0f);

            GameObject audio = new GameObject("Audio");
            audio.transform.SetParent(root.transform, false);
            audio.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            AudioSource source = audio.AddComponent<AudioSource>();

            DejaVuduSoundArchiveExhibit exhibit = root.AddComponent<DejaVuduSoundArchiveExhibit>();
            exhibit.Configure(content, source, trigger, focus.transform);
            interactionTarget.AddComponent<XrSelectSurface>();

            Debug.Log("[DejaVuduSoundArchive] Runtime exhibit installed in the Main House front yard. " +
                      $"Visual replacement child: {RootName}/{VisualName}.");
        }

        static Vector3 ResolveFrontYardPosition()
        {
            Transform spawn = FindByName("MainEntranceSpawn");
            if (spawn != null)
            {
                Vector3 position = spawn.position - spawn.right * 2.6f + spawn.forward * 2.8f;
                position.y = Mathf.Max(0.08f, spawn.position.y);
                return position;
            }

            return new Vector3(69.25f, 0.18f, 40.8f);
        }

        static Quaternion ResolveFrontYardRotation()
        {
            Transform spawn = FindByName("MainEntranceSpawn");
            if (spawn == null)
                return Quaternion.Euler(0f, 180f, 0f);

            Vector3 flatForward = spawn.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = Vector3.back;

            return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        static Transform FindByName(string objectName)
        {
            foreach (Transform transform in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform != null && transform.name == objectName)
                    return transform;
            }

            return null;
        }
    }
}
