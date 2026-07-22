using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class MeshellInteractionDiagnostic
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string NotePadsPath = "_SceneContent/ImplementedContributorInstallations/Meshell_Sturgis/NotePads";
    private const string OutputPath = "Logs/meshell_diag.txt";

    private static readonly StringBuilder Log = new StringBuilder();

    [MenuItem("BCaT/Meshell/Run Interaction Diagnostic")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Log.AppendLine("==== MESHELL INTERACTION DIAGNOSTIC ====");

        GameObject notePads = GameObject.Find(NotePadsPath);
        if (notePads == null)
        {
            // GameObject.Find skips inactive objects; fall back to a transform walk.
            notePads = FindInactiveByPath(NotePadsPath);
            Log.AppendLine(notePads == null
                ? "NotePads NOT FOUND by path (active or inactive)."
                : "NotePads found but INACTIVE in hierarchy.");
        }

        if (notePads != null)
            DumpExhibit("MESHELL NotePads", notePads);

        GameObject album = GameObject.Find("PhotoAlbum_Preview");
        if (album == null)
            album = FindInactiveByName("PhotoAlbum_Preview");
        if (album != null)
            DumpExhibit("LINDA PhotoAlbum_Preview", album);
        else
            Log.AppendLine("PhotoAlbum_Preview not found by name.");

        DumpCameras();
        DumpInteractors();

        if (notePads != null)
            PhysicsProbe(notePads, album);

        System.IO.File.WriteAllText(OutputPath, Log.ToString());
        Debug.Log(Log.ToString());
    }

    private static void DumpExhibit(string label, GameObject go)
    {
        Log.AppendLine($"\n---- {label} ----");
        Log.AppendLine($"Path: {GetPath(go.transform)}");
        Log.AppendLine($"activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} layer={go.layer}({LayerMask.LayerToName(go.layer)}) tag={go.tag}");
        Log.AppendLine($"World pos={go.transform.position} rot={go.transform.rotation.eulerAngles} lossyScale={go.transform.lossyScale}");

        Transform ancestor = go.transform;
        while (ancestor != null)
        {
            Log.AppendLine($"  ancestor '{ancestor.name}' localPos={ancestor.localPosition} localScale={ancestor.localScale} active={ancestor.gameObject.activeSelf}");
            ancestor = ancestor.parent;
        }

        Log.AppendLine("Components:");
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c == null) { Log.AppendLine("  <MISSING SCRIPT>"); continue; }
            Log.AppendLine($"  {c.GetType().FullName}");
            DumpComponentDetail(c);
        }

        Log.AppendLine("Children (recursive), colliders and renderers:");
        Bounds combined = default;
        bool hasBounds = false;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            string indent = "  ";
            Log.AppendLine($"{indent}[{(t.gameObject.activeInHierarchy ? "A" : "inactive")}] {GetPath(t)} lossyScale={t.lossyScale}");
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) { Log.AppendLine($"{indent}  <MISSING SCRIPT>"); continue; }
                if (c is Collider col)
                {
                    string meshInfo = "";
                    if (col is MeshCollider mc)
                        meshInfo = $" sharedMesh={(mc.sharedMesh == null ? "NULL" : mc.sharedMesh.name)} convex={mc.convex}";
                    if (col is BoxCollider bc)
                        meshInfo = $" center={bc.center} size={bc.size}";
                    Log.AppendLine($"{indent}  COLLIDER {c.GetType().Name} enabled={col.enabled} isTrigger={col.isTrigger}{meshInfo} worldBounds(center={col.bounds.center}, size={col.bounds.size})");
                }
                else if (c is Renderer r && !(t == go.transform))
                {
                    Log.AppendLine($"{indent}  RENDERER {c.GetType().Name} enabled={r.enabled} worldBounds(center={r.bounds.center}, size={r.bounds.size})");
                    if (r is MeshRenderer && t.name.StartsWith("Notepad"))
                    {
                        if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                        else combined.Encapsulate(r.bounds);
                    }
                }
            }
        }
        if (hasBounds)
            Log.AppendLine($"COMBINED Notepad* renderer bounds: center={combined.center} size={combined.size} min={combined.min} max={combined.max}");
    }

    private static void DumpComponentDetail(Component c)
    {
        string typeName = c.GetType().Name;
        if (typeName == "LindaLeaksPanelOpener" || typeName == "MeshellArticleNotebookOpener" || typeName == "MeshellArticleReaderController")
        {
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.GetIterator();
            p.NextVisible(true);
            while (p.NextVisible(false))
            {
                string v = p.propertyType switch
                {
                    SerializedPropertyType.ObjectReference => p.objectReferenceValue == null ? "NULL" : $"{p.objectReferenceValue.GetType().Name}:'{p.objectReferenceValue.name}'",
                    SerializedPropertyType.Enum => $"{p.enumValueIndex}",
                    SerializedPropertyType.Float => p.floatValue.ToString(),
                    SerializedPropertyType.Integer => p.intValue.ToString(),
                    SerializedPropertyType.Boolean => p.boolValue.ToString(),
                    _ => p.propertyType.ToString()
                };
                Log.AppendLine($"      .{p.propertyPath} = {v}");
            }
        }
        else if (c is XRSimpleInteractable xr)
        {
            SerializedObject so = new SerializedObject(xr);
            SerializedProperty cols = so.FindProperty("m_Colliders");
            Log.AppendLine($"      m_Colliders size={cols.arraySize}");
            for (int i = 0; i < cols.arraySize; i++)
            {
                Object o = cols.GetArrayElementAtIndex(i).objectReferenceValue;
                Log.AppendLine($"        [{i}] {(o == null ? "NULL/MISSING" : $"{o.GetType().Name} on '{((Collider)o).gameObject.name}'")}");
            }
            Log.AppendLine($"      m_InteractionLayers={so.FindProperty("m_InteractionLayers").FindPropertyRelative("m_Bits").intValue}");
            SerializedProperty calls = so.FindProperty("m_SelectEntered").FindPropertyRelative("m_PersistentCalls").FindPropertyRelative("m_Calls");
            Log.AppendLine($"      SelectEntered persistent calls={calls.arraySize}");
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                Object target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                Log.AppendLine($"        [{i}] target={(target == null ? "NULL" : target.GetType().Name)} method={call.FindPropertyRelative("m_MethodName").stringValue} mode={call.FindPropertyRelative("m_Mode").intValue} state={call.FindPropertyRelative("m_CallState").intValue}");
            }
        }
        else if (c is Rigidbody rb)
        {
            Log.AppendLine($"      isKinematic={rb.isKinematic} useGravity={rb.useGravity}");
        }
    }

    private static void DumpCameras()
    {
        Log.AppendLine("\n---- CAMERAS ----");
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Log.AppendLine($"  '{GetPath(cam.transform)}' tag={cam.tag} enabled={cam.enabled} activeInHierarchy={cam.gameObject.activeInHierarchy} worldPos={cam.transform.position}");
        Camera main = Camera.main;
        Log.AppendLine($"  Camera.main resolves to: {(main == null ? "NULL" : GetPath(main.transform))}");
    }

    private static void DumpInteractors()
    {
        Log.AppendLine("\n---- XR INTERACTORS ----");
        foreach (XRBaseInteractor it in Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Log.AppendLine($"  {it.GetType().Name} '{GetPath(it.transform)}' active={it.gameObject.activeInHierarchy} interactionLayers={it.interactionLayers.value}");
        foreach (NearFarInteractor nf in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Log.AppendLine($"  NearFarInteractor '{GetPath(nf.transform)}' active={nf.gameObject.activeInHierarchy} layers={nf.interactionLayers.value}");
    }

    private static void PhysicsProbe(GameObject notePads, GameObject album)
    {
        Log.AppendLine("\n---- PHYSICS PROBE ----");
        Physics.SyncTransforms();

        Bounds target = ComputeRendererBounds(notePads, "Notepad");
        Log.AppendLine($"Probe target bounds: center={target.center} size={target.size}");

        Collider[] near = Physics.OverlapBox(target.center, target.extents + new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        Log.AppendLine($"Colliders within bounds+0.5m: {near.Length}");
        foreach (Collider c in near)
            Log.AppendLine($"  {GetPath(c.transform)} type={c.GetType().Name} trigger={c.isTrigger} bounds(center={c.bounds.center}, size={c.bounds.size})");

        foreach (float dist in new[] { 1.5f, 3f })
        {
            foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 eye = target.center - dir * dist + Vector3.up * 0.4f;
                Vector3 rayDir = (target.center - eye).normalized;
                RaycastHit[] hits = Physics.RaycastAll(eye, rayDir, 8f, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                Log.AppendLine($"Ray from {eye} toward center ({dist}m {DirName(dir)}): {hits.Length} hits");
                for (int i = 0; i < Mathf.Min(hits.Length, 4); i++)
                    Log.AppendLine($"    hit[{i}] {GetPath(hits[i].collider.transform)} d={hits[i].distance:0.###} trigger={hits[i].collider.isTrigger}");
            }
        }

        if (album != null)
        {
            Bounds albumBounds = ComputeRendererBounds(album, null);
            Log.AppendLine($"\nLinda album renderer bounds: center={albumBounds.center} size={albumBounds.size}");
            Vector3 eye = albumBounds.center + Vector3.forward * -2f + Vector3.up * 0.3f;
            RaycastHit[] hits = Physics.RaycastAll(eye, (albumBounds.center - eye).normalized, 8f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            Log.AppendLine($"Linda probe ray: {hits.Length} hits");
            for (int i = 0; i < Mathf.Min(hits.Length, 4); i++)
                Log.AppendLine($"    hit[{i}] {GetPath(hits[i].collider.transform)} d={hits[i].distance:0.###} trigger={hits[i].collider.isTrigger}");
        }
    }

    private static Bounds ComputeRendererBounds(GameObject root, string childPrefix)
    {
        Bounds b = default;
        bool has = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (childPrefix != null && !r.gameObject.name.StartsWith(childPrefix))
                continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static string DirName(Vector3 v)
    {
        if (v == Vector3.forward) return "from-south";
        if (v == Vector3.back) return "from-north";
        if (v == Vector3.left) return "from-east";
        return "from-west";
    }

    private static GameObject FindInactiveByPath(string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0])
                continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++)
                t = t.Find(parts[i]);
            if (t != null)
                return t.gameObject;
        }
        return null;
    }

    private static GameObject FindInactiveByName(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name)
                return t.gameObject;
        return null;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = $"{t.name}/{path}";
        }
        return path;
    }
}
