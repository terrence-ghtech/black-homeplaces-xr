using System;
using System.Collections;
using System.IO;
using System.Text;
using BCaT.Production.Addressing;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Diagnostics
{
    /// <summary>
    /// Automated native-build smoke test, launched with -bcatSmokeTest [cycles].
    /// Drives the real production flow: waits for the main house, then runs
    /// repeated Black Kitchen enter/exit cycles through the shared transition
    /// lifecycle, recording per-cycle timings, memory, active Addressables
    /// handles, and scene counts. Writes a JSON+text report next to the player
    /// log and exits with code 0 (pass) or 2 (lifecycle failure detected), so
    /// desktop repeat-entry validation can run unattended from the command line.
    /// </summary>
    public static class SmokeTestRunner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arm()
        {
            int cycles = 0;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-bcatSmokeTest", StringComparison.OrdinalIgnoreCase))
                {
                    cycles = 3;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int n) && n > 0)
                        cycles = n;
                }
            }
            if (cycles <= 0) return;

            var go = new GameObject("BCaT_SmokeTest");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<SmokeTestBehaviour>().cycles = cycles;
        }

        sealed class SmokeTestBehaviour : MonoBehaviour
        {
            public int cycles = 3;
            readonly StringBuilder report = new StringBuilder();
            bool failed;

            IEnumerator Start()
            {
                report.AppendLine($"BCaT smoke test — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine(PlatformCapabilities.Describe());
                report.AppendLine($"cycles={cycles}");

                float startupBegin = Time.realtimeSinceStartup;

                // Reach the main house (through the menu/loading flow if present).
                yield return WaitForScene(SceneTransitionState.MainHouseSceneName, 180f);
                Line($"main house loaded at t={Time.realtimeSinceStartup - startupBegin:F1}s");
                yield return new WaitForSeconds(3f);
                Snapshot("baseline");

                for (int i = 1; i <= cycles; i++)
                {
                    float cycleStart = Time.realtimeSinceStartup;

                    // Enter Black Kitchen through the shared lifecycle.
                    if (!SceneTransitionState.RequestTransition(
                            SceneTransitionState.BlackKitchenSceneName,
                            SceneTransitionState.BlackKitchenEntrySpawnId,
                            SceneTransitionState.MainHouseSceneName))
                    {
                        Fail($"cycle {i}: enter transition request rejected: {SceneTransitionState.LastError}");
                        break;
                    }
                    SceneManager.LoadSceneAsync(SceneTransitionState.LoadingSceneName, LoadSceneMode.Single);
                    yield return WaitForScene(SceneTransitionState.BlackKitchenSceneName, 300f);
                    if (failed) break;
                    float enterTime = Time.realtimeSinceStartup - cycleStart;
                    yield return new WaitForSeconds(2f);
                    Snapshot($"cycle {i} in-kitchen (enter {enterTime:F1}s)");

                    // Return to the main house.
                    float exitStart = Time.realtimeSinceStartup;
                    if (!SceneTransitionState.RequestTransition(
                            SceneTransitionState.MainHouseSceneName,
                            SceneTransitionState.MainHouseKitchenReturnSpawnId,
                            SceneTransitionState.BlackKitchenSceneName))
                    {
                        Fail($"cycle {i}: exit transition request rejected: {SceneTransitionState.LastError}");
                        break;
                    }
                    SceneManager.LoadSceneAsync(SceneTransitionState.LoadingSceneName, LoadSceneMode.Single);
                    yield return WaitForScene(SceneTransitionState.MainHouseSceneName, 300f);
                    if (failed) break;
                    float exitTime = Time.realtimeSinceStartup - exitStart;
                    yield return new WaitForSeconds(2f);
                    Snapshot($"cycle {i} back-home (exit {exitTime:F1}s)");

                    if (SceneManager.sceneCount > 1)
                        Fail($"cycle {i}: {SceneManager.sceneCount} scenes loaded after return.");

                    int rigs = FindObjectsByType<CharacterController>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                    if (rigs > 1)
                        Fail($"cycle {i}: duplicate player rigs detected ({rigs} CharacterControllers).");
                }

                // Post-run lifecycle checks.
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
                yield return new WaitForSeconds(1f);
                Snapshot("final");
                Line(AddressablesHandleRegistry.Dump());

                if (AddressablesHandleRegistry.ActiveCount > 1)
                    Fail($"final: {AddressablesHandleRegistry.ActiveCount} active addressables handles " +
                         "(expected <= 1: the resident main-scene hold).");

                Line(failed ? "RESULT: FAIL" : "RESULT: PASS");
                WriteReport();

                if (!Application.isEditor)
                    Application.Quit(failed ? 2 : 0);
            }

            IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
            {
                float deadline = Time.realtimeSinceStartup + timeoutSeconds;
                while (SceneManager.GetActiveScene().name != sceneName)
                {
                    if (Time.realtimeSinceStartup > deadline)
                    {
                        Fail($"timeout waiting for scene '{sceneName}' " +
                             $"(active='{SceneManager.GetActiveScene().name}', " +
                             $"transition={SceneTransitionState.IsTransitionInProgress}, " +
                             $"lastError='{SceneTransitionState.LastError}')");
                        yield break;
                    }
                    yield return null;
                }
                // Let arrival controllers finish their frame-delayed work.
                yield return new WaitForSeconds(1.5f);
            }

            void Snapshot(string label)
            {
                Line($"[{label}] scene={SceneManager.GetActiveScene().name} " +
                     $"scenes={SceneManager.sceneCount} " +
                     $"handles={AddressablesHandleRegistry.ActiveCount} " +
                     $"managedMB={GC.GetTotalMemory(false) / (1024f * 1024f):F1} " +
                     $"reservedMB={Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f):F1} " +
                     $"allocatedMB={Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):F1} " +
                     $"fps~{1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f):F0}");

                try
                {
                    string dir = Path.Combine(Application.persistentDataPath, "BCaT", "smoke_screens");
                    Directory.CreateDirectory(dir);
                    string safe = label.Replace(' ', '_').Replace('(', '_').Replace(')', '_');
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, safe + ".png"));
                }
                catch { /* screenshots are best-effort evidence */ }
            }

            void Line(string message)
            {
                report.AppendLine(message);
                Debug.Log("[SmokeTest] " + message);
            }

            void Fail(string message)
            {
                failed = true;
                Line("FAIL: " + message);
            }

            void WriteReport()
            {
                try
                {
                    string dir = Path.Combine(Application.persistentDataPath, "BCaT");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir,
                        $"smoketest_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(path, report.ToString());
                    Debug.Log("[SmokeTest] Report written to " + path);
                }
                catch (Exception e)
                {
                    Debug.LogError("[SmokeTest] Could not write report: " + e.Message);
                }
            }
        }
    }
}
