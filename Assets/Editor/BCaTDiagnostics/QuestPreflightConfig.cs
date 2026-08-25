using System;
using System.Collections.Generic;

namespace BCaT.EditorTools.Diagnostics
{
    /// <summary>
    /// Every assumption the preflight makes, in one place and named, so no
    /// number in the report is buried inside a total. Each field carries the
    /// source it came from and how much it should be trusted.
    ///
    /// Confidence vocabulary used throughout the tool:
    ///   MEASURED   — read from the project, the imported asset, or the APK.
    ///   CALCULATED — derived from measured values with a documented formula.
    ///   ESTIMATED  — a model with a stated assumption; a range where possible.
    ///   ASSUMED    — a constant Unity cannot tell us; override it here.
    /// </summary>
    public static class QuestPreflightConfig
    {
        public const string MainScenePath = "Assets/BH_XR_MainScene.unity";
        public const string OutputDirectory = "Builds/Diagnostics";
        public const string TextReportName = "QuestMemoryPreflight.txt";
        public const string JsonReportName = "QuestMemoryPreflight.json";
        public const string AndroidPlatformName = "Android";

        /// <summary>
        /// The quality tier the player actually switches to on Quest
        /// (SettingsApplyControllers.ApplyQualityTier). The editor's ACTIVE tier
        /// is usually a desktop one, so reading the active tier would model the
        /// wrong render scale and MSAA.
        /// </summary>
        public const string QuestQualityTierName = "Quest";

        /// <summary>Candidate Quest release artifacts, most canonical first.</summary>
        public static readonly string[] ApkCandidates =
        {
            "Builds/Quest/Black Homeplaces XR - Quest.apk",
        };

        // ---- Baseline runtime allowance (PHASE 5) ---------------------------

        /// <summary>
        /// Unity runtime + IL2CPP/native libs + OpenXR + Meta runtime + basic
        /// device resources, BEFORE any main-scene content is resident.
        /// </summary>
        public const long BaselineRuntimeMB = 650;

        public const string BaselineSource =
            "Measured: BCaT Quest 3 Release trace — RSS stabilised at ~0.6 GB after launch " +
            "and before the BH_XR_MainScene load request was issued.";

        public const string BaselineConfidence = "MEASURED";

        // ---- Quest safety thresholds (PHASE 7) -----------------------------
        //
        // Calibrated against this project's own captured OOM on a Quest 3:
        // growth ran 0.6 -> 2.3 -> 3.9 -> 4.7-5.0+ GB, and lowmemorykiller
        // terminated the app in the 4.7-5.0 GB band. The safe budget therefore
        // sits below the lowest observed kill, not at the headset's 8 GB.

        /// <summary>Lowest RSS at which lowmemorykiller was actually observed to fire.</summary>
        public const long ObservedKillLowMB = 4700;
        public const long ObservedKillHighMB = 5000;

        // Thresholds sit BELOW the lowest observed kill, not at the headset's
        // 8 GB: Horizon OS keeps the remainder for the compositor, guardian and
        // system, and a build that only just fits will die on a busier day.
        public const long SafeBudgetMB = 3800;
        public const long WarningThresholdMB = 4200;
        public const long CriticalThresholdMB = 4700;

        public const string ThresholdSource =
            "Calibrated: lowmemorykiller terminated org.bcatlab.blackhomeplaces in the " +
            "4.7-5.0+ GB RSS band on this Quest 3 (8 GB device, Horizon OS / Android 14). " +
            "Critical is set at the LOWEST observed kill (4,700 MB); safe/warning sit below it.";

        // ---- Trace-calibrated transient overhead ---------------------------
        //
        // The formula-based headroom model below produced 0.5-1.1 GB for this
        // build, while the device peaked at 4.7-5.0+ GB against a predicted
        // resident total of ~2.7 GB — so the real transient overhead was
        // ~2.0-2.3 GB, two to four times the modelled band. These factors carry
        // that measurement forward as a multiple of predicted resident memory.
        //
        // Honest limitation: applied to THIS build the calibrated band
        // reproduces the observed kill band by construction, so it is not
        // independent evidence for this build's failure. Its value is
        // comparative — change the content, re-run, and the predicted peak moves
        // with the resident total. Replace these factors with per-phase numbers
        // once the [BCAT_MEMTRACE] capture attributes the anonymous growth.

        public const double CalibratedTransientFactorLow = 0.75;
        public const double CalibratedTransientFactorHigh = 0.86;

        public const string CalibratedTransientSource =
            "Measured: (observed device peak 4,700-5,000 MB - predicted resident ~2,690 MB) " +
            "/ predicted resident, from the Quest 3 Release trace of this build.";

        // ---- XR eye buffers (PHASE 3) --------------------------------------
        //
        // XRSettings.eyeTextureWidth/Height only exist in a running player, so
        // the per-eye size has to be assumed here. These are Quest 3 values at
        // resolution scale 1.0; the URP asset's render scale is applied on top.

        public const int QuestEyeWidth = 2064;
        public const int QuestEyeHeight = 2208;
        public const string EyeBufferSource =
            "Assumed: Quest 3 per-eye recommended/panel resolution at scale 1.0. " +
            "Override here, or read XRSettings.eyeTextureWidth from a device log.";

        /// <summary>Colour bytes per pixel for the eye swapchain (RGBA8 / sRGB).</summary>
        public const int EyeColorBytesPerPixel = 4;

        /// <summary>Depth+stencil bytes per pixel (D24_S8 / D32).</summary>
        public const int EyeDepthBytesPerPixel = 4;

        /// <summary>Swapchain images the XR runtime keeps per eye.</summary>
        public const int EyeSwapchainImages = 3;

        /// <summary>
        /// Adreno resolves MSAA from tile memory, so a 4x MSAA eye buffer does
        /// NOT cost 4x the colour allocation. Set true only to model a driver
        /// that stores the multisampled surface.
        /// </summary>
        public const bool CountMsaaAsFullAllocation = false;

        // ---- Audio (PHASE 3) -----------------------------------------------

        /// <summary>
        /// Per-clip resident cost of a Streaming clip: the ring buffer plus
        /// decoder state, NOT the clip. Prevents a 95 MB source WAV from being
        /// counted as 95 MB of RAM.
        /// </summary>
        public const double StreamingClipBufferMB = 0.4;

        /// <summary>Bytes per decompressed sample per channel (Unity decompresses to PCM16 on mobile).</summary>
        public const int DecompressedBytesPerSample = 2;

        /// <summary>Vorbis bitrate model: kbit/s per channel at quality 1.0, scaled linearly by quality.</summary>
        public const double VorbisKbpsPerChannelAtQuality1 = 256.0;

        // ---- Video (PHASE 3) -----------------------------------------------

        /// <summary>
        /// Working set of one PREPARED VideoPlayer on Android: MediaCodec
        /// decoder, its reference frames, and the output surface. Only players
        /// that prepare during startup are counted.
        /// </summary>
        public const double PreparedVideoPlayerMB = 24.0;

        /// <summary>Default RenderTexture a video controller creates when none is authored.</summary>
        public const int DefaultVideoRenderTextureWidth = 1280;
        public const int DefaultVideoRenderTextureHeight = 720;

        // ---- Scene / serialized overhead -----------------------------------

        /// <summary>
        /// Per scene object: native GameObject + Transform + component headers
        /// and the managed wrappers that come with them.
        /// </summary>
        public const double PerSceneObjectKB = 2.5;

        // ---- Temporary load headroom (PHASE 6) -----------------------------
        //
        // Nothing in Unity's public API reports transient load memory, so this
        // is a model with an explicit low/high band. Each term is reported on
        // its own line in the text report.

        /// <summary>CPU-side copy still resident while textures/meshes upload.</summary>
        public const double TransientUploadCopyLowPct = 0.10;
        public const double TransientUploadCopyHighPct = 0.25;

        /// <summary>Serialized-file read + decompression buffers, as a share of the uncompressed data.unity3d.</summary>
        public const double SerializedReadBufferLowPct = 0.05;
        public const double SerializedReadBufferHighPct = 0.15;

        /// <summary>Allocator reservations and fragmentation over the resident total.</summary>
        public const double AllocatorReserveLowPct = 0.10;
        public const double AllocatorReserveHighPct = 0.20;

        /// <summary>Floor for the serialized read/decompression term when no APK is available.</summary>
        public const double SerializedReadBufferFloorMB = 100.0;

        // ---- Reporting -----------------------------------------------------

        public const int TopAssetCount = 30;
        public const int TopPrefabCount = 20;
        public const int TopOwnerCount = 20;

        /// <summary>Unload the editor's asset cache every N assets so the tool itself stays bounded.</summary>
        public const int UnloadEveryNAssets = 400;

        // ---- BCaT content categories (PHASE 4) -----------------------------
        //
        // Ordered rules, first match wins. Paths come from this project's own
        // folder layout; anything unmatched is reported under its top-level
        // folder rather than being folded into a catch-all, so a category is
        // never silently wrong.

        public static readonly (string Fragment, string Category)[] CategoryRules =
        {
            // Exhibits — most specific first.
            ("/BCaT/Exhibits/BlackKitchen", "Black Kitchen"),
            ("/BCaT/Exhibits/Mural", "Mural"),
            ("/BCaT/Exhibits/PrivacyLawExhibit", "Privacy Law"),
            ("/BCaT/Exhibits/Adinkra", "Adinkra"),
            ("/BCaT/Exhibits/KitchenScholars", "Kitchen Scholars"),
            ("/BCaT/Exhibits/RhythmAndRope", "Rhythm and Rope"),
            ("/BCaT_assets/9night", "Nine Night"),
            ("/BCaT_assets/LindaLeaks", "Linda Leaks"),
            ("/BCaT_assets/BlackFamilyMuseumArchive", "Black Family Museum"),
            ("/BCaT_assets/BlackParlors", "Living Room / Parlor"),
            ("/BCaT_assets/SewingRoom", "Sewing Room"),
            ("/BCaT_assets/KitchenScholars", "Kitchen Scholars"),
            ("/BCaT_assets/Adinkra", "Adinkra"),
            ("/BCaT_assets/Meshell_Sturgis", "Meshell Sturgis"),
            ("/BCaT_assets/rhythm_n_rope", "Rhythm and Rope"),
            ("/BCaT_assets/HOMED", "HOMED"),
            ("/BCaT_assets/BTMMP_Workstation_Assembly", "BTMMP Workstation"),
            ("/BCaT_assets/ExhibitCanvases", "UI / Exhibit canvases"),
            ("/BCaT_assets/Ri", "Ri"),
            ("/BCaT/SceneTransitions", "UI / Loading screen"),
            ("/BCaT/ProductionCore", "UI / Production shell"),
            ("/BCaT/OptimizedMeshes", "Optimized meshes (shared)"),

            // Vegetation.
            ("/Animated Tropical Vegetation", "Vegetation / trees / bushes"),
            ("/Coconut Palm Tree Pack", "Vegetation / trees / bushes"),
            ("/SimpleNaturePack", "Vegetation / trees / bushes"),
            ("/ALP_Assets", "Vegetation / trees / bushes"),
            ("/Emilulz_Assets", "Vegetation / trees / bushes"),
            ("Free Plants Pack", "Vegetation / trees / bushes"),
            ("/Pandazole_Ultimate_Pack", "Vegetation / trees / bushes"),

            // Terrain.
            ("/TerrainSampleAssets", "Terrain"),
            (".terrainlayer", "Terrain"),
            ("New Terrain.asset", "Terrain"),

            // Exterior / architecture / furnishing.
            ("/BrokenVector", "Fence / exterior"),
            ("/Patio Furniture", "Fence / exterior"),
            ("/YughuesFreePavementsMaterials", "Fence / exterior"),
            ("/Idyllic Italian Coast Town", "House architecture"),
            ("/DevDen Arch Viz Scotland", "House architecture"),
            ("/Furniture Mega Pack", "House interior / furniture"),
            ("/LowPolyLivingRoomPack", "House interior / furniture"),
            ("/Gogo Casual Pack", "House interior / furniture"),
            ("/picture-frame", "House interior / furniture"),
            ("/Food Pack-Demo", "House interior / furniture"),
            ("/PolyOne", "House interior / furniture"),

            // Engine / shared.
            ("/TextMesh Pro", "UI / text"),
            ("/XRI", "XR / interaction"),
            ("/XR", "XR / interaction"),
            ("/StarterAssets", "XR / interaction"),
            ("/Settings", "Shared / render pipeline"),
            ("/Materials", "Shared / materials"),
            ("/Resources", "Shared / Resources"),
            ("/IgniteCoders", "Shared / water shader"),
            ("/Shaded Spectrum", "Shared / shaders"),
            ("/SubstanceAssets", "Shared / materials"),
            ("Packages/", "Engine / package"),
        };

        /// <summary>Lightmaps and probe data are recognised by asset naming, not folder.</summary>
        public static readonly string[] LightingNameFragments =
        {
            "Lightmap-", "ReflectionProbe-", "LightingData", "lightingdata",
        };

        public const string LightingCategory = "Lighting / probes";

        public static string CategoryFor(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "Unknown";

            string normalized = "/" + assetPath.Replace('\\', '/');

            foreach (string fragment in LightingNameFragments)
                if (normalized.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return LightingCategory;

            foreach ((string fragment, string category) in CategoryRules)
                if (normalized.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return category;

            // Unmatched: report the owning top-level folder so the line is
            // still actionable and never mislabelled.
            string[] parts = assetPath.Split('/');
            if (parts.Length >= 2 && parts[0] == "Assets")
                return "Uncategorised: Assets/" + parts[1];
            return "Uncategorised";
        }
    }
}
