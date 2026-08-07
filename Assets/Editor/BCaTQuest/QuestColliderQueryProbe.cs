using UnityEngine;
using UnityEditor;

namespace BCaT.EditorTools
{
    /// <summary>
    /// One-off empirical probe: does Collider.excludeLayers (per-collider layer
    /// override, used to stop a collider from physically blocking the player)
    /// also filter scene QUERIES such as Physics.Raycast / SphereCast?
    ///
    /// This decides how Quest XR select colliders are built: if queries still
    /// hit an exclude-everything collider, the XR ray can target it while the
    /// player walks straight through. Run with:
    ///   -executeMethod BCaT.EditorTools.QuestColliderQueryProbe.Run
    /// </summary>
    public static class QuestColliderQueryProbe
    {
        public static void Run()
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine("=== QuestColliderQueryProbe ===");

            var go = new GameObject("ProbeBox");
            go.layer = 0;
            go.transform.position = new Vector3(0f, 0f, 10f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 2f, 2f);
            box.isTrigger = false;

            // Exclude every layer from contact generation.
            box.excludeLayers = ~0;
            box.includeLayers = 0;

            Physics.SyncTransforms();

            var ray = new Ray(Vector3.zero, Vector3.forward);

            bool rayHit = Physics.Raycast(ray, out RaycastHit rayInfo, 50f,
                ~0, QueryTriggerInteraction.Ignore);
            results.AppendLine($"Physics.Raycast hit={rayHit} collider={(rayHit ? rayInfo.collider.name : "none")}");

            bool sphereHit = Physics.SphereCast(ray, 0.1f, out RaycastHit sphereInfo, 50f,
                ~0, QueryTriggerInteraction.Ignore);
            results.AppendLine($"Physics.SphereCast hit={sphereHit} collider={(sphereHit ? sphereInfo.collider.name : "none")}");

            Collider[] overlap = Physics.OverlapSphere(new Vector3(0f, 0f, 10f), 0.5f, ~0,
                QueryTriggerInteraction.Ignore);
            results.AppendLine($"Physics.OverlapSphere count={overlap.Length}");

            // Control: same box with default (no exclusions).
            box.excludeLayers = 0;
            Physics.SyncTransforms();
            bool controlHit = Physics.Raycast(ray, out _, 50f, ~0, QueryTriggerInteraction.Ignore);
            results.AppendLine($"CONTROL (no excludeLayers) Raycast hit={controlHit}");

            results.AppendLine(rayHit
                ? "VERDICT: excludeLayers does NOT filter queries -> safe to use for non-blocking XR select colliders."
                : "VERDICT: excludeLayers DOES filter queries -> must NOT use it; size colliders inside existing geometry instead.");

            Debug.Log(results.ToString());
            Object.DestroyImmediate(go);

            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "..", "Builds", "QuestColliderQueryProbe.txt"),
                results.ToString());

            EditorApplication.Exit(0);
        }
    }
}
