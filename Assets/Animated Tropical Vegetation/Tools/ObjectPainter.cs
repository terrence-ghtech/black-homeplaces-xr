using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
#if UNITY_EDITOR
public class ObjectPainter : EditorWindow
{
    public List<GameObject> objectsToPaint = new List<GameObject>();
    public int brushSize = 1;
    public int spacing = 25;

    //Tag of the ground layer for trees to snap to
    public static string groundTag = "GND";

    public Vector2 randomScaleRange = new Vector2(1f, 1f);

    private Vector3 lastPaintPosition;
    public bool isPainting = false;
    private double lastClickTime = 0;
    private float clickCooldown = 0.1f;
    private GameObject parentObject;

    [MenuItem("Tools/Object Painter")]
    public static void ShowWindow()
    {
        GetWindow<ObjectPainter>("Object Painter");
    }

    [MenuItem("Tools/Object Painter/Snap trees to ground")]
    public static void SnapTreesToGround()
    {
        GameObject treeParent = GameObject.Find("Trees");
        List<GameObject> gameObjects = new List<GameObject>();
        if (treeParent == null)
        {
            Debug.LogWarning("No Trees parent object found.");
            return;
        }
        else
        {
            foreach (Transform child in treeParent.transform)
            {
                if (child.gameObject != null)
                {
                    gameObjects.Add(child.gameObject);
                }
            }
        }
        for (int i = 0; i < gameObjects.Count; i++)
        {
            GameObject tree = gameObjects[i];
            if (tree != null)
            {
                RaycastHit hit;
                if (Physics.Raycast(tree.transform.position + Vector3.up * 100, Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(groundTag)))
                {
                    tree.transform.position = hit.point;
                }
            }
        }
    }

    void OnEnable()
    {
        parentObject = GameObject.Find("Trees");
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnGUI()
    {
        GUILayout.Label("Object Painter Settings", EditorStyles.boldLabel);

        for (int i = 0; i < objectsToPaint.Count; i++)
        {
            objectsToPaint[i] = (GameObject)EditorGUILayout.ObjectField("Object " + (i + 1), objectsToPaint[i], typeof(GameObject), false);
        }

        if (GUILayout.Button("Add Object"))
        {
            objectsToPaint.Add(null);
        }

        if (GUILayout.Button("Remove Object"))
        {
            if (objectsToPaint.Count > 0)
            {
                objectsToPaint.RemoveAt(objectsToPaint.Count - 1);
            }
        }

        brushSize = Mathf.RoundToInt(EditorGUILayout.Slider("Brush Size", brushSize, 1, 100));
        spacing = Mathf.RoundToInt(EditorGUILayout.Slider("Spacing", spacing, 1, 50));
        groundTag = EditorGUILayout.TagField("Ground Tag", groundTag);
        randomScaleRange = EditorGUILayout.Vector2Field("Random Scale Range", randomScaleRange);

        if (!isPainting && GUILayout.Button("Start Painting"))
        {
            isPainting = true;
            SceneView.duringSceneGui += OnSceneGUI;
            if (parentObject == null)
            {
                CreateParentObject();
            }
        }

        if (isPainting && GUILayout.Button("Stop Painting"))
        {
            isPainting = false;
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        Event current = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider != null)
            {
                DrawBrush(hit.point, brushSize);

                if (current.type == EventType.MouseDrag && current.button == 0)
                {
                    if (Vector3.Distance(hit.point, lastPaintPosition) > spacing)
                    {
                        PaintObject(hit.point);
                        lastPaintPosition = hit.point;
                    }
                    current.Use();
                }

                if (current.type == EventType.MouseDown && current.button == 0)
                {
                    if (EditorApplication.timeSinceStartup - lastClickTime > clickCooldown)
                    {
                        PaintObject(hit.point);
                        lastClickTime = EditorApplication.timeSinceStartup;
                    }
                    current.Use();
                }

                if (current.type == EventType.MouseMove)
                {
                    sceneView.Repaint();
                }
            }
        }
    }

    void PaintObject(Vector3 position)
    {
        if (objectsToPaint.Count == 0) return;

        int randomIndex = Random.Range(0, objectsToPaint.Count);
        GameObject objectToPaint = objectsToPaint[randomIndex];

        Vector3 randomOffset = Random.insideUnitSphere * brushSize;
        randomOffset.y = 0;

        Vector3 paintPosition = position + randomOffset;
        paintPosition.y = position.y;

        Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);

        GameObject newObject = PrefabUtility.InstantiatePrefab(objectToPaint, null) as GameObject;
        newObject.transform.position = paintPosition;
        newObject.transform.rotation = randomRotation;

        float randomScale = Random.Range(randomScaleRange.x, randomScaleRange.y);
        newObject.transform.localScale = new Vector3(randomScale, randomScale, randomScale);

        if (parentObject != null)
        {
            newObject.transform.SetParent(parentObject.transform);
        }

        Undo.RegisterCreatedObjectUndo(newObject, "Paint Object");
    }

    void DrawBrush(Vector3 center, float radius)
    {
        Handles.color = new Color(1, 0, 0, 1f);
        Handles.DrawWireDisc(center, Vector3.up, radius, 2f);

        Handles.color = new Color(1, 0, 0, 0.1f);
        Handles.DrawSolidDisc(center, Vector3.up, radius);

        Handles.color = Color.white;
    }

    void CreateParentObject()
    {
        parentObject = new GameObject("Trees");
        Undo.RegisterCreatedObjectUndo(parentObject, "Create Parent Object");
    }
}
#endif