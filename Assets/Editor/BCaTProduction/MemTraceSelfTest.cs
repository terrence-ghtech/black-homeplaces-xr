using BCaT.Production.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC — exercises the MemTrace code paths inside a real
    /// Unity runtime (counters, /proc probe, loaded-object snapshot, formatting)
    /// so a null reference or a bad API assumption is found here rather than
    /// after a 20-minute Quest build. Delete with the rest of the tracing.
    ///
    ///   Unity -batchmode -quit -projectPath . -executeMethod BCaT.EditorTools.MemTraceSelfTest.Run
    /// </summary>
    public static class MemTraceSelfTest
    {
        [MenuItem("BCaT/Diagnostics/MemTrace Self Test")]
        public static void Run()
        {
            Debug.Log($"[MemTraceSelfTest] enabled={MemTrace.Enabled} assets={MemTrace.AssetsEnabled} " +
                      $"holdActivation={MemTrace.HoldActivation} intervalMs={MemTrace.IntervalMs} " +
                      $"statmRssBytes={MemTrace.ReadStatmRssBytes()}");

            MemTrace.Mark("SELFTEST_FIRST");
            MemTrace.Mark("SELFTEST_SECOND", "detailField=value");
            MemTrace.Progress("SELFTEST_PROGRESS", 0.5f);
            MemTrace.ResetProgress();
            MemTrace.Snapshot("selftest");
            MemTrace.Mark("SELFTEST_DONE");

            Debug.Log("[MemTraceSelfTest] completed without exceptions.");
        }
    }
}
