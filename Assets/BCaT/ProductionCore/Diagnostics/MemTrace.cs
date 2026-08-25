using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Diagnostics
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC — memory attribution for the Quest main-scene OOM.
    /// Delete this file (and its call sites, all marked BCAT_MEMTRACE) once the
    /// cause is identified. It changes no product behavior: it only reads
    /// counters and logs.
    ///
    /// Three data sources, all landing in one logcat stream so they correlate
    /// without clock skew:
    ///
    ///   1. <see cref="Mark"/> — main-thread checkpoints along the load path,
    ///      carrying the Unity memory counters plus the process RSS from
    ///      /proc/self/statm (O(1) — no page-table walk, safe to call often).
    ///   2. <see cref="MemTraceSampler"/> — a background thread sampling
    ///      /proc/self/smaps_rollup every ~150 ms, so growth is still recorded
    ///      while the main thread is blocked inside a native scene load, and
    ///      dumping the largest /proc/self/smaps mappings whenever RSS jumps.
    ///      That dump is what attributes Android's "Unknown" memory.
    ///   3. <see cref="Snapshot"/> — loaded-object attribution: newly resident
    ///      objects over a size threshold, plus per-type totals.
    ///
    /// Everything is off if `adb shell setprop debug.bcat.memtrace 0` is set,
    /// so a run can be repeated without instrumentation for comparison.
    /// </summary>
    public static class MemTrace
    {
        public const string Tag = "[BCAT_MEMTRACE]";

        /// <summary>"0" disables all tracing.</summary>
        public const string EnabledProperty = "debug.bcat.memtrace";

        /// <summary>"0" disables the loaded-object snapshots only.</summary>
        public const string AssetsProperty = "debug.bcat.memtrace.assets";

        /// <summary>Background sampler period in milliseconds (default 150).</summary>
        public const string IntervalProperty = "debug.bcat.memtrace.ms";

        /// <summary>
        /// "1" holds scene activation for one frame so the before/after
        /// activation pair can be measured exactly. This DOES add a frame to
        /// the transition, so it is off by default — use it only on a
        /// follow-up run that needs activation isolated.
        /// </summary>
        public const string HoldActivationProperty = "debug.bcat.memtrace.holdactivation";

        /// <summary>Objects at or above this many bytes are reported individually.</summary>
        public const long LargeObjectBytes = 8L * 1024 * 1024;

        const long Mb = 1024 * 1024;

        static readonly System.Diagnostics.Stopwatch Clock = new System.Diagnostics.Stopwatch();
        static readonly HashSet<int> KnownObjects = new HashSet<int>();
        static readonly StringBuilder Line = new StringBuilder(512);

        static bool initialized;
        static bool enabled;
        static bool assetsEnabled;
        static bool holdActivation;
        static int intervalMs = 150;

        static long lastAllocated;
        static long lastReserved;
        static long lastRss;
        static float lastProgress = -1f;
        static double lastProgressLogMs;

        // Updated on the main thread, read by the sampler thread — a stale read
        // is harmless, so no lock.
        static volatile int currentFrame;
        static volatile string currentScene = "<none>";

        public static bool Enabled
        {
            get
            {
                if (!initialized)
                    Configure();
                return enabled;
            }
        }

        public static bool AssetsEnabled => Enabled && assetsEnabled;

        public static bool HoldActivation => Enabled && holdActivation;

        public static int IntervalMs => intervalMs;

        public static double ElapsedMs => Clock.Elapsed.TotalMilliseconds;

        // ---- lifecycle -----------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            initialized = false;
            enabled = false;
            KnownObjects.Clear();
            lastAllocated = 0;
            lastReserved = 0;
            lastRss = 0;
            lastProgress = -1f;
            lastProgressLogMs = 0;
            currentFrame = 0;
            currentScene = "<none>";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Enabled)
                return;

            var go = new GameObject("BCaT_MemTrace");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<MemTraceSampler>();

            Debug.Log($"{Tag} t={Stamp()} ev=TRACE_INSTALLED scene={SceneManager.GetActiveScene().name} " +
                      $"intervalMs={intervalMs} assets={assetsEnabled} holdActivation={holdActivation} " +
                      $"device='{SystemInfo.deviceModel}' os='{SystemInfo.operatingSystem}' " +
                      $"vramMB={SystemInfo.graphicsMemorySize} sysMemMB={SystemInfo.systemMemorySize} " +
                      $"gfx={SystemInfo.graphicsDeviceType} debugBuild={Debug.isDebugBuild}");

            Mark("PROCESS_BASELINE");
        }

        static void Configure()
        {
            initialized = true;
            Clock.Start();

            enabled = ReadProperty(EnabledProperty) != "0";
            assetsEnabled = ReadProperty(AssetsProperty) != "0";
            holdActivation = ReadProperty(HoldActivationProperty) == "1";

            if (int.TryParse(ReadProperty(IntervalProperty), out int ms) && ms >= 25 && ms <= 2000)
                intervalMs = ms;
        }

        static string ReadProperty(string key)
        {
            try
            {
                return BCaTPlatform.ReadAndroidSystemProperty(key) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // ---- main-thread checkpoints ---------------------------------------

        /// <summary>
        /// One timestamped checkpoint line with every counter and the delta
        /// against the previous checkpoint. Safe to call from anywhere on the
        /// main thread; costs one small /proc read plus the counter reads.
        /// </summary>
        public static void Mark(string eventName, string detail = null)
        {
            if (!Enabled)
                return;

            long allocated = Profiler.GetTotalAllocatedMemoryLong();
            long reserved = Profiler.GetTotalReservedMemoryLong();
            long monoUsed = Profiler.GetMonoUsedSizeLong();
            long monoHeap = Profiler.GetMonoHeapSizeLong();
            long gfxDriver = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long unusedReserved = Profiler.GetTotalUnusedReservedMemoryLong();
            long rss = ReadStatmRssBytes();

            lock (Line)
            {
                Line.Length = 0;
                Line.Append(Tag)
                    .Append(" t=").Append(Stamp())
                    .Append(" wall=").Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                    .Append(" ev=").Append(eventName)
                    .Append(" scene=").Append(SceneManager.GetActiveScene().name)
                    .Append(" frame=").Append(Time.frameCount);
                AppendCounter(Line, "allocMB", allocated, lastAllocated);
                AppendCounter(Line, "resMB", reserved, lastReserved);
                AppendCounter(Line, "rssMB", rss, lastRss);
                Line.Append(" unusedResMB=").Append(Mbs(unusedReserved))
                    .Append(" monoUsedMB=").Append(Mbs(monoUsed))
                    .Append(" monoHeapMB=").Append(Mbs(monoHeap))
                    .Append(" gfxDrvMB=").Append(Mbs(gfxDriver))
                    .Append(" vramMB=").Append(SystemInfo.graphicsMemorySize);

                if (!string.IsNullOrEmpty(detail))
                    Line.Append(" detail=").Append(detail);

                Debug.Log(Line.ToString());
            }

            lastAllocated = allocated;
            lastReserved = reserved;
            lastRss = rss;
        }

        /// <summary>
        /// Progress-loop checkpoint. Logs when the load has visibly advanced,
        /// when ~100 ms have passed, or when memory jumped — so a multi-hundred
        /// MB step is never averaged away, without emitting a line per frame.
        /// </summary>
        public static void Progress(string eventName, float progress)
        {
            if (!Enabled)
                return;

            double now = ElapsedMs;
            long rss = ReadStatmRssBytes();
            bool advanced = progress - lastProgress >= 0.01f;
            bool overdue = now - lastProgressLogMs >= 100.0;
            bool jumped = Math.Abs(rss - lastRss) >= 32 * Mb;

            if (!advanced && !overdue && !jumped)
                return;

            lastProgress = progress;
            lastProgressLogMs = now;
            Mark(eventName, $"progress={progress.ToString("0.000", CultureInfo.InvariantCulture)}");
        }

        public static void ResetProgress()
        {
            lastProgress = -1f;
            lastProgressLogMs = 0;
        }

        internal static void NoteFrame(int frame) => currentFrame = frame;

        internal static void NoteScene(string sceneName) => currentScene = sceneName ?? "<none>";

        internal static int CurrentFrame => currentFrame;

        internal static string CurrentScene => currentScene;

        internal static string Stamp() =>
            TimeSpan.FromMilliseconds(ElapsedMs).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

        static void AppendCounter(StringBuilder sb, string name, long value, long previous)
        {
            sb.Append(' ').Append(name).Append('=').Append(Mbs(value));
            long delta = value - previous;
            sb.Append("(").Append(delta >= 0 ? "+" : "-").Append(Mbs(Math.Abs(delta))).Append(")");
        }

        internal static string Mbs(long bytes) =>
            (bytes / (double)Mb).ToString("0.0", CultureInfo.InvariantCulture);

        // ---- process memory (cheap, main-thread safe) -----------------------

        /// <summary>
        /// Resident set size from /proc/self/statm — read straight out of the
        /// kernel's mm counters, so unlike smaps it needs no page-table walk
        /// and stays cheap even at 5 GB. Page size is assumed 4 KiB (true for
        /// arm64 Android through 14); the raw page count is logged by the
        /// sampler so a wrong assumption would be visible.
        /// </summary>
        public static long ReadStatmRssBytes()
        {
            try
            {
                string statm = File.ReadAllText("/proc/self/statm");
                int space = statm.IndexOf(' ');
                if (space < 0)
                    return 0;
                int end = statm.IndexOf(' ', space + 1);
                if (end < 0)
                    end = statm.Length;
                string field = statm.Substring(space + 1, end - space - 1);
                return long.TryParse(field, out long pages) ? pages * 4096L : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ---- loaded-object attribution -------------------------------------

        /// <summary>
        /// Reports what is resident now: every type total, plus each object at
        /// or over <see cref="LargeObjectBytes"/> that appeared since the last
        /// snapshot. Uses Profiler.GetRuntimeMemorySizeLong where it returns a
        /// figure and a coarse estimate otherwise (release players can return
        /// 0), flagging which was used per line so nothing is silently guessed.
        /// </summary>
        public static void Snapshot(string label)
        {
            if (!AssetsEnabled)
                return;

            Mark("SNAPSHOT_BEGIN", $"label={label}");

            SnapshotType<Texture2D>(label);
            SnapshotType<Cubemap>(label);
            SnapshotType<RenderTexture>(label);
            SnapshotType<AudioClip>(label);
            SnapshotType<Mesh>(label);
            SnapshotType<Material>(label);
            SnapshotType<UnityEngine.Video.VideoClip>(label);
            SnapshotType<TerrainData>(label);

            Mark("SNAPSHOT_END", $"label={label}");
        }

        static void SnapshotType<T>(string label) where T : UnityEngine.Object
        {
            T[] objects;
            try
            {
                objects = Resources.FindObjectsOfTypeAll<T>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} t={Stamp()} ev=SNAPSHOT_TYPE_FAILED type={typeof(T).Name} error={e.Message}");
                return;
            }

            long total = 0;
            long newlyResident = 0;
            int newCount = 0;

            for (int i = 0; i < objects.Length; i++)
            {
                T obj = objects[i];
                if (obj == null)
                    continue;

                bool estimated;
                long size = SizeOf(obj, out estimated);
                total += size;

                bool isNew = KnownObjects.Add(obj.GetInstanceID());
                if (!isNew)
                    continue;

                newCount++;
                newlyResident += size;

                if (size < LargeObjectBytes)
                    continue;

                Debug.Log($"{Tag} t={Stamp()} ev=LARGE_OBJECT label={label} type={typeof(T).Name} " +
                          $"name='{obj.name}' MB={Mbs(size)} estimated={(estimated ? 1 : 0)} " +
                          $"where={Where(obj)} shape={Describe(obj)}");
            }

            Debug.Log($"{Tag} t={Stamp()} ev=TYPE_TOTAL label={label} type={typeof(T).Name} " +
                      $"count={objects.Length} totalMB={Mbs(total)} " +
                      $"newCount={newCount} newMB={Mbs(newlyResident)}");
        }

        static long SizeOf(UnityEngine.Object obj, out bool estimated)
        {
            estimated = false;
            long size = 0;
            try
            {
                size = Profiler.GetRuntimeMemorySizeLong(obj);
            }
            catch (Exception)
            {
                size = 0;
            }

            if (size > 0)
                return size;

            estimated = true;
            return Estimate(obj);
        }

        /// <summary>
        /// Coarse fallback when the profiler API reports nothing: enough to
        /// rank objects, explicitly flagged as an estimate in the log.
        /// </summary>
        static long Estimate(UnityEngine.Object obj)
        {
            switch (obj)
            {
                case Texture2D t:
                    return (long)(t.width * (long)t.height * BytesPerPixel(t.format) * (t.mipmapCount > 1 ? 1.34 : 1.0));
                case Cubemap c:
                    return (long)(c.width * (long)c.width * 6 * BytesPerPixel(c.format) * (c.mipmapCount > 1 ? 1.34 : 1.0));
                case RenderTexture rt:
                    return rt.width * (long)rt.height * 4 * (rt.depth > 0 ? 2 : 1);
                case AudioClip a:
                    return (long)a.samples * a.channels * 2;
                case Mesh m:
                    return m.vertexCount * 48L;
                default:
                    return 0;
            }
        }

        static double BytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                    return 1;
                case TextureFormat.RGB24:
                    return 3;
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                    return 4;
                case TextureFormat.RGBAHalf:
                    return 8;
                case TextureFormat.RGBAFloat:
                    return 16;
                case TextureFormat.DXT1:
                case TextureFormat.ETC2_RGB:
                    return 0.5;
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ASTC_4x4:
                    return 1;
                case TextureFormat.ASTC_6x6:
                    return 0.45;
                case TextureFormat.ASTC_8x8:
                    return 0.25;
                default:
                    return 4;
            }
        }

        static string Describe(UnityEngine.Object obj)
        {
            switch (obj)
            {
                case Texture2D t:
                    return $"{t.width}x{t.height}/{t.format}/mips={t.mipmapCount}/readable={t.isReadable}";
                case Cubemap c:
                    return $"{c.width}/{c.format}/mips={c.mipmapCount}";
                case RenderTexture rt:
                    return $"{rt.width}x{rt.height}/{rt.format}/depth={rt.depth}";
                case AudioClip a:
                    return $"{a.length:0.0}s/{a.channels}ch/{a.frequency}Hz/{a.loadType}/loaded={a.loadState}";
                case Mesh m:
                    return $"verts={m.vertexCount}/subMeshes={m.subMeshCount}/readable={m.isReadable}";
                case UnityEngine.Video.VideoClip v:
                    return $"{v.width}x{v.height}/{v.frameCount}f/{v.frameRate:0.0}fps";
                case TerrainData td:
                    return $"heightmap={td.heightmapResolution}/alphamaps={td.alphamapTextureCount}/detail={td.detailResolution}";
                default:
                    return "-";
            }
        }

        /// <summary>
        /// Owning scene where the object is scene-bound. Shared assets belong to
        /// no scene, so they are reported by identity (name + shape) instead of
        /// a fabricated path — nothing at runtime maps an asset back to its
        /// project path.
        /// </summary>
        static string Where(UnityEngine.Object obj)
        {
            if (obj is Component component && component != null)
                return $"scene:{component.gameObject.scene.name}/{component.gameObject.name}";
            if (obj is GameObject go)
                return $"scene:{go.scene.name}/{go.name}";
            return "asset";
        }
    }
}
