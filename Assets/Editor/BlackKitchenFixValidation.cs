// Temporary batchmode validation for the Black Kitchen interaction fixes.
// Opens the memory scene and verifies, with real physics queries, that the
// oven trigger is reachable from the natural front approach, that the rice
// pot still works, and that the enlarged trigger does not bleed into other
// interaction zones. Run via:
//   Unity -batchmode -nographics -executeMethod BlackKitchenFixValidation.Run
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BlackKitchenFixValidation
{
    private const string ScenePath = "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

    // DesktopRig CharacterController from runtime logs: height 2, radius 0.5,
    // feet at y 0.10, camera at y 1.55.
    private const float CapsuleRadius = 0.5f;
    private const float FeetY = 0.10f;
    private const float CapsuleHeight = 2f;
    private const float EyeY = 1.55f;

    private static readonly StringBuilder Report = new();
    private static bool failed;

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            GameObject oven = GameObject.Find("OvenInteraction");
            GameObject rice = GameObject.Find("RiceBeansPotInteraction");
            GameObject counter = GameObject.Find("Counter_CollisionProxy");
            GameObject exitInterface = GameObject.Find("ExitInterface");
            Check("scene objects resolved", oven != null && rice != null && counter != null && exitInterface != null);
            if (oven == null || rice == null)
            {
                Finish();
                return;
            }

            BoxCollider ovenBox = oven.GetComponent<BoxCollider>();
            Check("oven BoxCollider present and trigger", ovenBox != null && ovenBox.isTrigger);
            if (ovenBox != null)
            {
                Report.AppendLine($"    oven box center {ovenBox.center} size {ovenBox.size} " +
                                  $"world front z {ovenBox.bounds.min.z:F3} back z {ovenBox.bounds.max.z:F3}");
                Check("oven box serialized values applied",
                    Mathf.Approximately(ovenBox.center.z, -0.55f) && Mathf.Approximately(ovenBox.size.z, 2.35f));
            }

            Collider counterCollider = counter != null ? counter.GetComponent<Collider>() : null;

            // 1. Natural front approach: capsule pressed against the counter
            //    front face (z = 2.84), centered on the oven (x = 0.863).
            Vector3 standFront = new(0.863f, 0f, 2.33f);
            Check("front approach: capsule overlaps oven trigger", CapsuleTouches(standFront, ovenBox));
            Check("front approach: standing spot is physically valid (no solid overlap)", !CapsuleTouchesAnySolid(standFront));

            // 2. Overlap-fallback path used by UpdateRangeFromColliders: player
            //    root point inside the trigger volume.
            Vector3 rootPoint = new(0.863f, 0.17f, 2.33f);
            Check("front approach: rig root point inside oven trigger",
                ovenBox != null && (ovenBox.ClosestPoint(rootPoint) - rootPoint).sqrMagnitude <= 0.0001f);

            // 3. Raycast path: eye-height ray aimed at the oven trigger must
            //    reach it before any solid collider (mirrors the script logic).
            Vector3 eye = new(0.863f, EyeY, 2.33f);
            Vector3 aim = ((ovenBox != null ? ovenBox.bounds.center : oven.transform.position) - eye).normalized;
            Check("front approach: camera ray reaches oven before solids", FirstInteractionHit(eye, aim, 3f, oven.transform));

            // 4. Rice pot regression: capsule in front of the appliance proxy.
            SphereCollider riceSphere = rice.GetComponent<SphereCollider>();
            Check("rice sphere present and trigger", riceSphere != null && riceSphere.isTrigger);
            Vector3 standRice = new(-1.54f, 0f, 1.61f);
            Check("rice approach: capsule overlaps rice trigger", CapsuleTouches(standRice, riceSphere));

            // 5. No cross-eligibility: the rice standing spot must not touch the
            //    oven trigger and vice versa, so the InteractionGate never sees
            //    both as candidates from one spot.
            Check("no overlap: rice spot does not touch oven trigger", !CapsuleTouches(standRice, ovenBox));
            Check("no overlap: oven spot does not touch rice trigger", !CapsuleTouches(standFront, riceSphere));

            // 6. Room-center spot (by the table aisle entrance) should touch
            //    neither trigger.
            Vector3 standCenter = new(0f, 0f, 0f);
            Check("no overlap: room center touches no story trigger",
                !CapsuleTouches(standCenter, ovenBox) && !CapsuleTouches(standCenter, riceSphere));

            // 7. Exit interface zone must stay independent of the oven trigger.
            if (exitInterface != null)
            {
                Collider exitCollider = exitInterface.GetComponent<Collider>();
                Check("no overlap: oven trigger does not intersect exit interface",
                    ovenBox == null || exitCollider == null || !ovenBox.bounds.Intersects(exitCollider.bounds));
            }

            // 8. The trigger must not poke through the back wall into any
            //    reachable space: everything past Boundary_Back (z >= 3.575) is
            //    outside the walkable area, so just document the extent.
            GameObject backWall = GameObject.Find("Boundary_Back");
            if (backWall != null && ovenBox != null)
            {
                Collider wall = backWall.GetComponent<Collider>();
                Report.AppendLine($"    back wall front z {wall.bounds.min.z:F3}; oven trigger ends z {ovenBox.bounds.max.z:F3} (unreachable side)");
            }

            Finish();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BKValidation] Exception: " + e);
            EditorApplication.Exit(1);
        }
    }

    private static bool CapsuleTouches(Vector3 feet, Collider target)
    {
        if (target == null)
            return false;

        foreach (Collider hit in OverlapPlayerCapsule(feet, QueryTriggerInteraction.Collide))
        {
            if (hit == target)
                return true;
        }

        return false;
    }

    private static bool CapsuleTouchesAnySolid(Vector3 feet)
    {
        foreach (Collider hit in OverlapPlayerCapsule(feet, QueryTriggerInteraction.Ignore))
        {
            if (hit != null)
            {
                Report.AppendLine($"    solid overlap at {feet}: {hit.name}");
                return true;
            }
        }

        return false;
    }

    private static Collider[] OverlapPlayerCapsule(Vector3 feet, QueryTriggerInteraction triggers)
    {
        Vector3 bottom = feet + Vector3.up * (FeetY + CapsuleRadius);
        Vector3 top = feet + Vector3.up * (FeetY + CapsuleHeight - CapsuleRadius);
        return Physics.OverlapCapsule(bottom, top, CapsuleRadius, ~0, triggers);
    }

    private static bool FirstInteractionHit(Vector3 origin, Vector3 direction, float distance, Transform interactionRoot)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Ray(origin, direction), distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == interactionRoot || hit.collider.transform.IsChildOf(interactionRoot))
                return true;
            if (!hit.collider.isTrigger)
            {
                Report.AppendLine($"    ray blocked by solid '{hit.collider.name}' at {hit.distance:F2} m");
                return false;
            }
        }

        return false;
    }

    private static void Check(string label, bool ok)
    {
        Report.AppendLine($"  [{(ok ? "PASS" : "FAIL")}] {label}");
        if (!ok)
            failed = true;
    }

    private static void Finish()
    {
        Debug.Log("[BKValidation] Results:\n" + Report);
        EditorApplication.Exit(failed ? 1 : 0);
    }
}
