using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Writes meaningful Quest prompt wording into exhibits whose serialized
    /// prompt data was left blank, so no headset prompt reads as a bare verb
    /// ("Open", "View", "Listen", "Interact") or exposes an internal object
    /// name ("chair", "Fish_Asset", "pictureframe").
    ///
    /// Only the XR side is written (xrPrompt + objectName). desktopPrompt is
    /// never touched, so desktop wording is unchanged.
    ///
    /// Every name below is derived from data already authored in the project —
    /// the exhibit's own prompt copy, its scene naming, or the resource its link
    /// points at — not invented curatorial claims. Entries marked NEEDS-REVIEW
    /// had no authored name anywhere and use a descriptive label taken from the
    /// linked resource; the owner should confirm the wording.
    ///
    /// -executeMethod BCaT.EditorTools.QuestCuratorialPrompts.Run
    /// </summary>
    public static class QuestCuratorialPrompts
    {
        const string SceneAssetPath = "Assets/BH_XR_MainScene.unity";
        const string Root = "_SceneContent/ImplementedContributorInstallations/";

        sealed class Entry
        {
            public string Path;
            public string Verb;      // Quest action label
            public string Name;      // curatorial object name
            public string Source;    // where the name came from (audit trail)
        }

        static readonly Entry[] Entries =
        {
            new Entry
            {
                Path = Root + "Meshell_Sturgis/Garden/flowerbed",
                Verb = "View", Name = "My Grandma's Garden",
                Source = "exhibit's own authored desktopPrompt",
            },
            new Entry
            {
                Path = Root + "LindaLeaks_Exhibit/PhotoAlbum_Preview/Artifact_PhotoAlbum",
                Verb = "View", Name = "Linda Leaks Photo Album",
                Source = "exhibit + artifact naming in scene",
            },
            new Entry
            {
                Path = Root + "LindaLeaks_Exhibit/HousingMap_Preview/Artifact_HousingMap",
                Verb = "Open", Name = "Linda Leaks Housing Map",
                Source = "exhibit + artifact naming in scene (ArcGIS StoryMap link)",
            },
            new Entry
            {
                Path = Root + "BTMMP_Workstation_Assembly/pictureframe",
                Verb = "Open", Name = "The Breonna Taylor Memorial Mural Project",
                Source = "BTMMP assembly name + TheBreonnaTaylorMuralPrompt in scene",
            },
            new Entry
            {
                Path = Root + "RI/MuralExhibit",
                Verb = "View", Name = "Black Homeplaces Community Mural",
                Source = "the exhibit's own plaque (LabelBodyText); the earlier Breonna Taylor " +
                         "name belonged to the downstairs BTMMP workstation, not this mural",
            },
            new Entry
            {
                Path = Root + "RhythmAndRope_JumpRope",
                Verb = "Open", Name = "Rhythm and Rope",
                Source = "authored exhibit name",
            },
            new Entry
            {
                Path = Root + "RI/Photo_Asset/Photo-Album",
                Verb = "Open", Name = "Photo Album",
                Source = "object is a photo album (Heyzine flip-book link)",
            },
            new Entry
            {
                Path = Root + "RI/Chair_asset/chair",
                Verb = "Open", Name = "Black Homeplaces Overview",
                Source = "targetUrl slug bcatlab.org/blog/black-homeplaces-overview",
            },
            new Entry
            {
                Path = Root + "RI/domino/DominoSpatialAudio",
                Verb = "Listen", Name = "Dominoes",
                Source = "object identity (domino spatial audio station)",
            },
            new Entry
            {
                Path = Root + "RI/Fish_Asset",
                Verb = "Open", Name = "360° Homeplace Tour",
                Source = "NEEDS-REVIEW: no authored name; Kuula 360 collection link",
            },
        };

        public static void Run()
        {
            var report = new StringBuilder();
            int problems = 0;
            report.AppendLine("=== Quest curatorial prompt wording ===");
            report.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            Scene scene = EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);

            foreach (Entry entry in Entries)
            {
                GameObject go = FindByPath(scene, entry.Path);
                if (go == null)
                {
                    report.AppendLine($"  FAIL not found: {entry.Path}");
                    problems++;
                    continue;
                }

                string xrText = $"{entry.Verb} — {entry.Name}";
                bool written = false;

                foreach (Component component in go.GetComponents<Component>())
                {
                    if (component == null)
                        continue;

                    var so = new SerializedObject(component);
                    foreach (string fieldName in new[] { "prompt", "sharedPrompt" })
                    {
                        SerializedProperty prompt = so.FindProperty(fieldName);
                        if (prompt == null)
                            continue;

                        SerializedProperty xr = prompt.FindPropertyRelative("xrPrompt");
                        SerializedProperty objectName = prompt.FindPropertyRelative("objectName");
                        if (xr == null && objectName == null)
                            continue;

                        if (xr != null) xr.stringValue = xrText;
                        if (objectName != null) objectName.stringValue = entry.Name;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(component);
                        written = true;

                        report.AppendLine($"  OK {entry.Path}");
                        report.AppendLine($"     {component.GetType().Name}.{fieldName}: \"{xrText}\"");
                        report.AppendLine($"     name source: {entry.Source}");
                        break;
                    }

                    if (written)
                        break;
                }

                if (!written)
                {
                    report.AppendLine($"  FAIL no SharedInteractionPromptConfig field on {entry.Path}");
                    problems++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine();
            report.AppendLine(problems == 0 ? "RESULT: OK" : $"RESULT: {problems} FAILURE(S)");

            string text = report.ToString();
            Debug.Log(text);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "..", "Builds", "QuestCuratorialPrompts.txt"), text);

            if (Application.isBatchMode)
                EditorApplication.Exit(problems == 0 ? 0 : 2);
        }

        static GameObject FindByPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0])
                    continue;

                Transform current = root.transform;
                for (int i = 1; i < parts.Length && current != null; i++)
                    current = current.Find(parts[i]);

                if (current != null)
                    return current.gameObject;
            }
            return null;
        }
    }
}
