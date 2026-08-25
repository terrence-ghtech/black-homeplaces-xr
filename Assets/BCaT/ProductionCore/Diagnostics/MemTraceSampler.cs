using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Diagnostics
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC — see <see cref="MemTrace"/>. Installed only by
    /// MemTrace.Install; nothing in the product references this type.
    ///
    /// The background thread exists because the interesting growth happens
    /// while the main thread is inside SceneManager's native load, where no
    /// coroutine or Update can run: a main-thread-only sampler would show one
    /// line before the load and one after, which is exactly the resolution we
    /// already have from dumpsys. The thread touches no Unity API other than
    /// Debug.Log (thread-safe) — every counter it reads comes from /proc.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class MemTraceSampler : MonoBehaviour
    {
        /// <summary>RSS growth since the last dump that triggers a full smaps breakdown.</summary>
        const long JumpTriggerBytes = 192L * 1024 * 1024;

        /// <summary>Cap on full smaps dumps: parsing allocates, and this must not become the problem.</summary>
        const int MaxSmapsDumps = 14;

        const int TopMappings = 15;

        static readonly char[] Space = { ' ' };

        Thread sampler;
        volatile bool running;
        bool rollupAvailable = true;
        int smapsDumps;
        long lastSampleRss;
        long lastDumpRss;

        void Awake()
        {
            MemTrace.NoteScene(SceneManager.GetActiveScene().name);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.lowMemory += OnLowMemory;

            // /proc exists on Android and Linux only. On a desktop editor or
            // player there is nothing to sample, and a thread failing every
            // tick would be pure noise.
            if (!File.Exists("/proc/self/statm"))
            {
                Debug.Log($"{MemTrace.Tag} t={MemTrace.Stamp()} ev=PROC_UNAVAILABLE " +
                          $"platform={Application.platform} (checkpoint marks still logged)");
                return;
            }

            running = true;
            sampler = new Thread(SampleLoop)
            {
                IsBackground = true,
                Name = "BCaT_MemTrace",
                Priority = System.Threading.ThreadPriority.BelowNormal,
            };
            sampler.Start();
        }

        void OnDestroy()
        {
            running = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // The thread is a background thread, so it cannot hold the process
            // open; it exits on its next tick.
            Application.lowMemory -= OnLowMemory;
        }

        void Update() => MemTrace.NoteFrame(Time.frameCount);

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MemTrace.NoteScene(scene.name);
            MemTrace.Mark("SCENE_LOADED_CALLBACK", $"scene={scene.name} mode={mode} rootCount={scene.rootCount}");
            StartCoroutine(FirstFrames(scene));
        }

        /// <summary>
        /// The frames right after activation, where deferred work (Awake/Start
        /// chains, video Prepare, shader warmup, GPU upload) lands.
        /// </summary>
        IEnumerator FirstFrames(Scene scene)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return null;
                MemTrace.Mark("POST_ACTIVATION_FRAME", $"scene={scene.name} n={i + 1}");
            }

            if (scene.name == SceneTransitionState.MainHouseSceneName)
                MemTrace.Snapshot("main-scene+6frames");

            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSecondsRealtime(1f);
                MemTrace.Mark("POST_ACTIVATION_SECOND", $"scene={scene.name} n={i + 1}");
            }

            if (scene.name == SceneTransitionState.MainHouseSceneName)
                MemTrace.Snapshot("main-scene+5s");
        }

        void OnLowMemory()
        {
            MemTrace.Mark("APPLICATION_LOW_MEMORY");
            // Force a breakdown at the moment Android says it is running out —
            // this is often the last thing logged before the kill.
            DumpSmaps("lowMemory");
        }

        // ---- background sampling -------------------------------------------

        void SampleLoop()
        {
            while (running)
            {
                try
                {
                    Sample();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{MemTrace.Tag} ev=SAMPLER_ERROR error={e.Message}");
                }

                Thread.Sleep(MemTrace.IntervalMs);
            }
        }

        void Sample()
        {
            long rss = 0, anon = 0, shmem = 0, swap = 0, pss = 0;
            long statmRss = MemTrace.ReadStatmRssBytes();

            if (rollupAvailable)
            {
                try
                {
                    using (var reader = new StreamReader("/proc/self/smaps_rollup"))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (TryReadKb(line, "Rss:", ref rss)) continue;
                            if (TryReadKb(line, "Pss:", ref pss)) continue;
                            if (TryReadKb(line, "Anonymous:", ref anon)) continue;
                            if (TryReadKb(line, "Shmem:", ref shmem)) continue;
                            if (TryReadKb(line, "Swap:", ref swap)) continue;
                        }
                    }
                }
                catch (Exception e)
                {
                    rollupAvailable = false;
                    Debug.LogWarning($"{MemTrace.Tag} ev=SMAPS_ROLLUP_UNAVAILABLE error={e.Message} " +
                                     "(falling back to statm RSS only)");
                }
            }

            // statm is the fallback and the cross-check: the two RSS figures
            // come from different kernel paths and should agree.
            if (rss == 0)
                rss = statmRss;

            // smaps_rollup on this kernel reports Anonymous but not Shmem, so
            // the remainder is file-backed plus any shared memory.
            long fileShmem = rss - anon - shmem;
            long delta = rss - lastSampleRss;
            lastSampleRss = rss;

            Debug.Log($"{MemTrace.Tag} t={MemTrace.Stamp()} " +
                      $"wall={DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} " +
                      $"ev=RSS_SAMPLE scene={MemTrace.CurrentScene} frame={MemTrace.CurrentFrame} " +
                      $"rssMB={MemTrace.Mbs(rss)}({(delta >= 0 ? "+" : "-")}{MemTrace.Mbs(Math.Abs(delta))}) " +
                      $"pssMB={MemTrace.Mbs(pss)} anonMB={MemTrace.Mbs(anon)} fileShmemMB={MemTrace.Mbs(fileShmem)} " +
                      $"shmemMB={MemTrace.Mbs(shmem)} swapMB={MemTrace.Mbs(swap)} statmRssMB={MemTrace.Mbs(statmRss)}");

            if (rss - lastDumpRss >= JumpTriggerBytes)
            {
                lastDumpRss = rss;
                DumpSmaps("jump");
            }
        }

        /// <summary>
        /// Full /proc/self/smaps breakdown: the only thing that answers "what
        /// is dumpsys calling Unknown". Mappings are aggregated by their
        /// backing key — a file path, or the kernel's anon tag
        /// (`[anon:libc_malloc]`, `[anon:scudo:primary]`, …) — so a multi-GB
        /// anonymous region is named rather than lumped in with everything else.
        /// </summary>
        void DumpSmaps(string reason)
        {
            if (smapsDumps >= MaxSmapsDumps)
                return;
            smapsDumps++;

            var byKey = new Dictionary<string, long>(256);
            var byCategory = new Dictionary<string, long>(16);
            long totalRss = 0;
            int mappings = 0;
            string key = "unmapped";

            try
            {
                using (var reader = new StreamReader("/proc/self/smaps"))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Every line starts in column 0, so the discriminator is
                        // the shape of the first token: a mapping header starts
                        // "start-end perms ...", a detail line starts "Rss:".
                        if (IsMappingHeader(line))
                        {
                            // Header: "start-end perms offset dev inode  path"
                            string[] parts = line.Split(Space, 6, StringSplitOptions.RemoveEmptyEntries);
                            key = parts.Length >= 6 ? parts[5].Trim() : "[anon]";
                            if (key.Length == 0)
                                key = "[anon]";
                            mappings++;
                            continue;
                        }

                        long bytes = 0;
                        if (!TryReadKb(line, "Rss:", ref bytes) || bytes == 0)
                            continue;

                        totalRss += bytes;
                        Accumulate(byKey, key, bytes);
                        Accumulate(byCategory, Categorize(key), bytes);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{MemTrace.Tag} ev=SMAPS_DUMP_FAILED reason={reason} error={e.Message}");
                return;
            }

            var categories = new StringBuilder(256);
            foreach (var pair in Sorted(byCategory))
                categories.Append(pair.Key).Append('=').Append(MemTrace.Mbs(pair.Value)).Append(' ');

            Debug.Log($"{MemTrace.Tag} t={MemTrace.Stamp()} " +
                      $"wall={DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} " +
                      $"ev=SMAPS_CATEGORIES reason={reason} dump={smapsDumps} scene={MemTrace.CurrentScene} " +
                      $"frame={MemTrace.CurrentFrame} totalRssMB={MemTrace.Mbs(totalRss)} " +
                      $"mappings={mappings} {categories}");

            int rank = 0;
            foreach (var pair in Sorted(byKey))
            {
                if (++rank > TopMappings)
                    break;
                Debug.Log($"{MemTrace.Tag} t={MemTrace.Stamp()} ev=SMAPS_TOP reason={reason} " +
                          $"dump={smapsDumps} rank={rank} MB={MemTrace.Mbs(pair.Value)} key='{pair.Key}'");
            }
        }

        static void Accumulate(Dictionary<string, long> map, string key, long bytes)
        {
            map.TryGetValue(key, out long existing);
            map[key] = existing + bytes;
        }

        /// <summary>
        /// True for a smaps mapping header. Detail keys ("Rss:", "VmFlags:")
        /// end their first token with a colon; a header's first token is the
        /// hex address range and contains a dash.
        /// </summary>
        static bool IsMappingHeader(string line)
        {
            int end = line.IndexOf(' ');
            if (end < 0)
                end = line.Length;
            if (end == 0)
                return false;
            if (line[end - 1] == ':')
                return false;
            return line.LastIndexOf('-', end - 1) > 0;
        }

        static List<KeyValuePair<string, long>> Sorted(Dictionary<string, long> map)
        {
            var list = new List<KeyValuePair<string, long>>(map);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }

        /// <summary>
        /// Buckets that map onto the candidate explanations for the Unknown
        /// growth: player asset mappings (data.unity3d, .resS, bundles), the
        /// APK itself, graphics/ion buffers, native heap, and plain anonymous
        /// reservations from Unity's own allocators.
        /// </summary>
        static string Categorize(string key)
        {
            if (key.IndexOf("data.unity3d", StringComparison.OrdinalIgnoreCase) >= 0)
                return "unity3d";
            if (key.IndexOf(".resS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf(".resource", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf(".bundle", StringComparison.OrdinalIgnoreCase) >= 0)
                return "resS_bundle";
            if (key.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || key.IndexOf(".apk", StringComparison.OrdinalIgnoreCase) >= 0)
                return "apk";
            if (key.EndsWith(".obb", StringComparison.OrdinalIgnoreCase))
                return "obb";
            if (key.IndexOf(".so", StringComparison.OrdinalIgnoreCase) >= 0)
                return "native_lib";
            if (key.IndexOf("dmabuf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("ion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("kgsl", StringComparison.OrdinalIgnoreCase) >= 0)
                return "gpu_dmabuf";
            if (key.IndexOf("ashmem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("memfd", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("shmem", StringComparison.OrdinalIgnoreCase) >= 0)
                return "shared";
            if (key.IndexOf("libc_malloc", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("scudo", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("jemalloc", StringComparison.Ordinal) >= 0)
                return "native_heap";
            if (key.IndexOf("dalvik", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("/dev/", StringComparison.Ordinal) == 0)
                return "runtime_dev";
            if (key.StartsWith("[", StringComparison.Ordinal))
                return "anon_tagged";
            if (key.StartsWith("/", StringComparison.Ordinal))
                return "other_file";
            return "anon_untagged";
        }

        static bool TryReadKb(string line, string field, ref long valueBytes)
        {
            if (!line.StartsWith(field, StringComparison.Ordinal))
                return false;

            int i = field.Length;
            while (i < line.Length && line[i] == ' ')
                i++;
            int start = i;
            while (i < line.Length && char.IsDigit(line[i]))
                i++;
            if (i == start)
                return false;

            if (long.TryParse(line.Substring(start, i - start), out long kb))
                valueBytes = kb * 1024L;
            return true;
        }
    }
}
