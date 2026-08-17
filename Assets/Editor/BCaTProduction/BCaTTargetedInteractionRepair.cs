using System.Text;
using BCaT.Production.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Two targeted scene repairs, both authored against patterns that already
    /// ship in this scene rather than new mechanisms.
    ///
    /// 1. Nine Night ("Nine Night and Good Mourning", Christin Washington)
    ///    shipped as a bare looping AudioSource with m_PlayOnAwake enabled and a
    ///    4 m custom rolloff: the soundscape started at scene load and simply
    ///    faded up as the visitor walked past the drum. There was no collider,
    ///    no IInteractionTarget and no XR interactable, so it could not be
    ///    selected at all.
    ///
    ///    This gives the drum the exact component set Rianna Walcott's "Duppy
    ///    Know Who Fi Frighten" dominoes use (RI/domino/DominoSpatialAudio):
    ///    trigger BoxCollider + SpatialAudioToggle + XRSimpleInteractable whose
    ///    selectEntered calls SpatialAudioToggle.OnXRSelect + XrSelectSurface.
    ///    Selection, blocking and cooldown therefore run through the shared
    ///    InteractionRouter on both platforms, and one select toggles playback.
    ///
    ///    The AudioSource itself is left where it was authored (its own child
    ///    object, 1.5 m from the drum) so the spatialization is unchanged; only
    ///    playOnAwake is cleared. SpatialAudioToggle references it through its
    ///    serialized audioSource field, and is configured with the AudioSource's
    ///    own authored rolloff values so Start() re-applies identical settings.
    ///
    /// 2. Linda Leaks "Cooperative Hall of Fame" is a
    ///    MediaVideoController in ProximityTrigger mode, where the trigger
    ///    volume is the entire desktop availability gate (MaxDistance is 999 and
    ///    MaxViewAngle is 0 in that mode, so nothing else constrains it). Its
    ///    scene override sized that trigger 180 x 80 x 140 local units on an
    ///    object scaled 0.05 — a 9.0 x 4.0 x 7.0 m box straddling the dining
    ///    room's west wall, reaching about 4.3 m into the hallway and swallowing
    ///    the Nine Night drum. This retightens it to a dining-room-side pocket
    ///    in front of the camera and brings the unused raycast fallback distance
    ///    into line with it.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BCaTTargetedInteractionRepair.Repair
    /// </summary>
    public static class BCaTTargetedInteractionRepair
    {
        const string ScenePath = "Assets/BH_XR_MainScene.unity";

        const string DrumPath =
            "_SceneContent/ImplementedContributorInstallations/9Night/drum";
        const string NineNightAudioPath =
            "_SceneContent/ImplementedContributorInstallations/9Night/Audio Source";
        const string LindaArtifactPath =
            "_SceneContent/ImplementedContributorInstallations/LindaLeaks_Exhibit/" +
            "VintageCamera_Preview/Artifact_VintageCamera";

        /// <summary>Aim/line-of-sight volume around the drum, in metres.</summary>
        const float DrumInteractionBoxMetres = 1.6f;

        /// <summary>Matches the drum AudioSource's authored 4 m rolloff.</summary>
        const float DrumInteractionDistance = 4f;

        // Linda Leaks trigger, expressed in the artifact's own local units
        // (the object is scaled 0.05, so 20 local units = 1 m).
        static readonly Vector3 LindaTriggerSize = new Vector3(64f, 80f, 88f);
        static readonly Vector3 LindaTriggerCenter = new Vector3(6.8f, 10f, -6.6f);
        const float LindaInteractionDistance = 4f;

        const string LindaQuestSelectName = "Artifact_VintageCamera_QuestXRSelect";

        // Quest aim collider, in the same local units. Only the hallway-facing
        // axis moves: the box is pulled fully onto the dining-room side of the
        // wall plane so the wall occludes the controller ray from the hallway.
        // Height and depth stay exactly as authored, so the target the visitor
        // points at from the dining room is unchanged.
        static readonly Vector3 LindaQuestSelectCenter = new Vector3(0.99f, 0f, 0f);
        const float LindaQuestSelectSizeX = 5.2f;

        /// <summary>Dining room / hallway wall plane, world X.</summary>
        const float DiningRoomWallPlaneX = 170.02f;

        static readonly StringBuilder Report = new StringBuilder();
        static bool failed;

        /// <summary>
        /// Read-only measurement pass: prints the geometry the Quest aim
        /// surface has to be sized against, and raycasts the exhibit from the
        /// five validation positions so the XR ray's first hit is on record.
        /// </summary>
        [MenuItem("BCaT/Interaction/Probe Linda Leaks Reach")]
        public static void Probe()
        {
            Report.Clear();
            failed = false;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject artifact = Find(scene, LindaArtifactPath);
            if (artifact == null)
            {
                Debug.Log("[TargetedInteractionRepair]\n" + Report);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Report.AppendLine("--- visible camera geometry (renderers under the artifact) ---");
            Bounds? visible = null;
            foreach (Renderer r in artifact.GetComponentsInChildren<Renderer>(true))
            {
                // The popup panel is authored inactive and is not part of the object.
                if (r.GetComponentInParent<Canvas>() != null)
                    continue;
                Report.AppendLine($"  {Path(r.transform)}: {FmtBounds(r.bounds)}");
                if (visible == null) visible = r.bounds;
                else { Bounds b = visible.Value; b.Encapsulate(r.bounds); visible = b; }
            }
            if (visible != null)
                Report.AppendLine($"  COMBINED visible bounds: {FmtBounds(visible.Value)}");

            Report.AppendLine("--- colliders under the artifact ---");
            foreach (Collider c in artifact.GetComponentsInChildren<Collider>(true))
                Report.AppendLine($"  {Path(c.transform)}: trigger={c.isTrigger} " +
                                  $"activeSelf={c.gameObject.activeSelf} {FmtBounds(c.bounds)}");

            Report.AppendLine("--- XR ray probe: first physics hit from each validation position ---");
            var questSelect = artifact.transform.Find("Artifact_VintageCamera_QuestXRSelect");
            Vector3 aim = questSelect != null ? questSelect.position : artifact.transform.position;
            foreach (var probe in Probes)
            {
                Vector3 eye = probe.eye;
                Vector3 dir = aim - eye;
                float distance = dir.magnitude;
                var hits = Physics.RaycastAll(eye, dir.normalized, distance + 0.5f,
                    ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                string first = hits.Length > 0
                    ? $"{Path(hits[0].collider.transform)} @ {hits[0].distance:F2} m"
                    : "<nothing>";
                Report.AppendLine($"  {probe.label,-22} eye={Fmt(eye)} dist={distance:F2} first hit: {first}");
            }

            Debug.Log("[TargetedInteractionRepair]\n" + Report);
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        /// <summary>The five validation positions, at standing eye height.</summary>
        internal static readonly (string label, Vector3 eye)[] Probes =
        {
            ("dining centre", new Vector3(172.30f, 7.40f, 164.60f)),
            ("dining edge", new Vector3(173.30f, 7.40f, 165.60f)),
            ("doorway", new Vector3(170.02f, 7.40f, 163.20f)),
            ("hallway outside", new Vector3(168.60f, 7.40f, 163.40f)),
            ("hallway far", new Vector3(166.60f, 7.40f, 159.50f)),
        };

        [MenuItem("BCaT/Interaction/Repair Nine Night + Linda Leaks Reach")]
        public static void Repair()
        {
            Report.Clear();
            failed = false;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RepairNineNight(scene);
            RepairLindaLeaksReach(scene);

            if (!failed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Report.AppendLine($"Saved '{ScenePath}'.");
            }
            else
            {
                Report.AppendLine("FAILED — scene NOT saved.");
            }

            Debug.Log("[TargetedInteractionRepair]\n" + Report);

            if (Application.isBatchMode)
                EditorApplication.Exit(failed ? 1 : 0);
        }

        // ---- Goal 1: Nine Night ------------------------------------------

        static void RepairNineNight(Scene scene)
        {
            Report.AppendLine("--- Nine Night ---");

            GameObject drum = Find(scene, DrumPath);
            GameObject audioHost = Find(scene, NineNightAudioPath);
            if (drum == null || audioHost == null)
                return;

            var audio = audioHost.GetComponent<AudioSource>();
            if (audio == null)
            {
                Fail($"'{NineNightAudioPath}' has no AudioSource.");
                return;
            }

            // No autoplay: the soundscape now starts only on an explicit select.
            Report.AppendLine($"  AudioSource.playOnAwake {audio.playOnAwake} -> false " +
                              $"(clip='{(audio.clip != null ? audio.clip.name : "<resource>")}', " +
                              $"loop={audio.loop}, min={audio.minDistance:F3}, max={audio.maxDistance:F3}, " +
                              $"rolloff={audio.rolloffMode}, spatialBlend={audio.spatialBlend})");
            audio.playOnAwake = false;
            EditorUtility.SetDirty(audio);

            // Aim / line-of-sight / XR-ray volume, sized in world metres.
            var box = drum.GetComponent<BoxCollider>();
            if (box == null)
                box = drum.AddComponent<BoxCollider>();
            Vector3 lossy = drum.transform.lossyScale;
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(
                DrumInteractionBoxMetres / Mathf.Abs(lossy.x),
                DrumInteractionBoxMetres / Mathf.Abs(lossy.y),
                DrumInteractionBoxMetres / Mathf.Abs(lossy.z));
            EditorUtility.SetDirty(box);
            Report.AppendLine($"  BoxCollider trigger local size={Fmt(box.size)} " +
                              $"(world {Fmt(box.bounds.size)}) at {Fmt(box.bounds.center)}");

            // The shared select-to-activate handler.
            var toggle = drum.GetComponent<SpatialAudioToggle>();
            if (toggle == null)
                toggle = drum.AddComponent<SpatialAudioToggle>();

            var so = new SerializedObject(toggle);
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("interactionDistance").floatValue = DrumInteractionDistance;
            so.FindProperty("displayName").stringValue = "Nine Night Soundscape";
            so.FindProperty("prompt.desktopPrompt").stringValue = string.Empty;
            so.FindProperty("prompt.xrPrompt").stringValue = "Listen — Nine Night Soundscape";
            so.FindProperty("prompt.verb").enumValueIndex = (int)SharedInteractionVerb.Listen;
            so.FindProperty("prompt.objectName").stringValue = "Nine Night Soundscape";
            // Re-apply exactly the authored spatialization, nothing else.
            so.FindProperty("configureSpatialAudio").boolValue = true;
            so.FindProperty("spatialBlend").floatValue = audio.spatialBlend;
            so.FindProperty("rolloffMode").enumValueIndex = (int)audio.rolloffMode;
            so.FindProperty("minDistance").floatValue = audio.minDistance;
            so.FindProperty("maxDistance").floatValue = audio.maxDistance;
            so.FindProperty("dopplerLevel").floatValue = audio.dopplerLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
            Report.AppendLine($"  SpatialAudioToggle -> audioSource='{Path(audio.transform)}', " +
                              $"interactionDistance={DrumInteractionDistance}");

            // Quest select relay, wired exactly like the dominoes.
            var interactable = drum.GetComponent<XRSimpleInteractable>();
            if (interactable == null)
                interactable = drum.AddComponent<XRSimpleInteractable>();

            var iso = new SerializedObject(interactable);
            SerializedProperty colliders = iso.FindProperty("m_Colliders");
            colliders.ClearArray();
            colliders.InsertArrayElementAtIndex(0);
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = box;
            iso.ApplyModifiedPropertiesWithoutUndo();

            // Never leave a second dispatch path behind.
            while (interactable.selectEntered.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(interactable.selectEntered, 0);
            UnityEventTools.AddVoidPersistentListener(interactable.selectEntered, toggle.OnXRSelect);
            EditorUtility.SetDirty(interactable);
            Report.AppendLine($"  XRSimpleInteractable colliders=1, selectEntered listeners=" +
                              $"{interactable.selectEntered.GetPersistentEventCount()} " +
                              $"-> SpatialAudioToggle.OnXRSelect");

            // The XRI casters ignore triggers, so the trigger box above needs a
            // Quest-only non-trigger twin; XrSelectSurface builds it at runtime.
            var surface = drum.GetComponent<XrSelectSurface>();
            if (surface == null)
                surface = drum.AddComponent<XrSelectSurface>();
            var sso = new SerializedObject(surface);
            sso.FindProperty("forwardsTo").stringValue = "SpatialAudioToggle";
            sso.ApplyModifiedPropertiesWithoutUndo();
            Report.AppendLine("  XrSelectSurface added (forwardsTo=SpatialAudioToggle)");
        }

        // ---- Goal 2: Linda Leaks Cooperative Hall of Fame ----------------

        static void RepairLindaLeaksReach(Scene scene)
        {
            Report.AppendLine("--- Linda Leaks / Cooperative Hall of Fame ---");

            GameObject artifact = Find(scene, LindaArtifactPath);
            if (artifact == null)
                return;

            var box = artifact.GetComponent<BoxCollider>();
            var controller = artifact.GetComponent<MediaVideoController>();
            if (box == null || controller == null)
            {
                Fail($"'{LindaArtifactPath}' is missing its BoxCollider or MediaVideoController.");
                return;
            }

            Report.AppendLine($"  before: local size={Fmt(box.size)} center={Fmt(box.center)} " +
                              $"trigger={box.isTrigger}");
            Report.AppendLine($"  before: world bounds {FmtBounds(box.bounds)}");

            box.isTrigger = true;
            box.size = LindaTriggerSize;
            box.center = LindaTriggerCenter;
            EditorUtility.SetDirty(box);

            Report.AppendLine($"  after:  local size={Fmt(box.size)} center={Fmt(box.center)} " +
                              $"trigger={box.isTrigger}");
            Report.AppendLine($"  after:  world bounds {FmtBounds(box.bounds)}");

            // The raycast fallback distance is unused in ProximityTrigger mode,
            // but must not describe a reach the trigger no longer has.
            var co = new SerializedObject(controller);
            SerializedProperty distance = co.FindProperty("interactionDistance");
            Report.AppendLine($"  MediaVideoController.interactionDistance " +
                              $"{distance.floatValue} -> {LindaInteractionDistance} " +
                              $"(desktopActivation={(MediaVideoController.DesktopActivation)co.FindProperty("desktopActivation").enumValueIndex})");
            distance.floatValue = LindaInteractionDistance;
            co.ApplyModifiedPropertiesWithoutUndo();

            RepairLindaLeaksQuestSurface(artifact);
        }

        /// <summary>
        /// The Quest reach fix.
        ///
        /// The hand-authored twin is NOT oversized: its 7 x 7.456 x 9.659 figure
        /// is in local units under a 0.05-scaled parent, i.e. 0.35 x 0.37 x
        /// 0.48 m in world space — a box on the camera itself. What is wrong is
        /// where it sits: its west face lands 15 mm past the dining room's wall
        /// plane, beside the doorway, so nothing occludes the controller ray
        /// from the hallway side.
        ///
        /// XrSelectSurface is deliberately NOT used here. It mirrors the
        /// interactable's own colliders, which after the desktop repair above is
        /// the 3.2 x 4.0 x 4.4 m proximity trigger; that would replace a 0.35 m
        /// ray target with a 3.2 m one reaching into the doorway — the opposite
        /// of the requirement. The authored twin is kept and pulled clear of the
        /// wall instead.
        /// </summary>
        static void RepairLindaLeaksQuestSurface(GameObject artifact)
        {
            Transform questSelect = artifact.transform.Find(LindaQuestSelectName);
            var questBox = questSelect != null ? questSelect.GetComponent<BoxCollider>() : null;
            if (questBox == null)
            {
                Fail($"'{LindaQuestSelectName}' or its BoxCollider is missing.");
                return;
            }

            Report.AppendLine($"  Quest aim before: local size={Fmt(questBox.size)} " +
                              $"center={Fmt(questBox.center)}");
            Report.AppendLine($"  Quest aim before: world bounds {FmtBounds(questBox.bounds)} " +
                              $"(wall plane X={DiningRoomWallPlaneX:F2}, " +
                              $"clearance {questBox.bounds.min.x - DiningRoomWallPlaneX:F3} m)");

            Vector3 size = questBox.size;
            size.x = LindaQuestSelectSizeX;
            questBox.size = size;
            questBox.center = LindaQuestSelectCenter;
            EditorUtility.SetDirty(questBox);

            Report.AppendLine($"  Quest aim after:  local size={Fmt(questBox.size)} " +
                              $"center={Fmt(questBox.center)}");
            Report.AppendLine($"  Quest aim after:  world bounds {FmtBounds(questBox.bounds)} " +
                              $"(clearance {questBox.bounds.min.x - DiningRoomWallPlaneX:F3} m)");

            if (questBox.bounds.min.x <= DiningRoomWallPlaneX)
                Fail("Quest aim collider still reaches the hallway side of the wall plane.");
        }

        // ---- helpers ------------------------------------------------------

        static GameObject Find(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            Transform current = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == parts[0]) { current = root.transform; break; }
            }

            for (int i = 1; current != null && i < parts.Length; i++)
                current = current.Find(parts[i]);

            if (current == null)
                Fail($"scene object '{path}' not found.");

            return current != null ? current.gameObject : null;
        }

        static void Fail(string message)
        {
            failed = true;
            Report.AppendLine("  FAIL: " + message);
            Debug.LogError("[TargetedInteractionRepair] " + message);
        }

        static string Fmt(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

        static string FmtBounds(Bounds b) =>
            $"X[{b.min.x:F2},{b.max.x:F2}] Y[{b.min.y:F2},{b.max.y:F2}] Z[{b.min.z:F2},{b.max.z:F2}] " +
            $"size={Fmt(b.size)}";

        static string Path(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }
    }
}
