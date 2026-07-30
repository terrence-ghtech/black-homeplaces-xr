using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BCaT.Production.Settings
{
    /// <summary>
    /// Display settings application: resolution, fullscreen/windowed, vsync,
    /// frame-rate limit, display selection. Quest ignores all of this (XR owns
    /// the swapchain), so applying is a no-op there.
    /// </summary>
    public static class DisplaySettingsController
    {
        public static void Apply(ApplicationSettingsData.DisplaySettings d)
        {
            if (PlatformCapabilities.IsQuestConfiguration || PlatformCapabilities.IsXRActive)
                return;

            // Kiosk installations always run fullscreen.
            bool fullscreen = ApplicationModeService.IsKiosk || d.fullscreen;

            // A previously selected display may be unplugged; fall back to primary.
            if (d.displayIndex > 0 && d.displayIndex < Display.displays.Length)
            {
                try { Display.displays[d.displayIndex].Activate(); }
                catch { d.displayIndex = 0; }
            }
            else if (d.displayIndex >= Display.displays.Length)
            {
                d.displayIndex = 0;
            }

            int width = d.width > 0 ? d.width : Display.main.systemWidth;
            int height = d.height > 0 ? d.height : Display.main.systemHeight;

            var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != mode)
                Screen.SetResolution(width, height, mode);

            QualitySettings.vSyncCount = Mathf.Clamp(d.vSyncCount, 0, 2);
            Application.targetFrameRate = d.targetFrameRate > 0 ? d.targetFrameRate : -1;
        }
    }

    /// <summary>
    /// Graphics settings application. The quality tier (Unity quality level +
    /// its URP asset) is the baseline; the granular options are applied as
    /// deltas on top of the tier's captured defaults so switching tiers stays
    /// predictable and reset-to-default is exact.
    /// </summary>
    public static class GraphicsSettingsController
    {
        class TierBaseline
        {
            public float renderScale;
            public float shadowDistance;
            public int msaa;
        }

        static readonly Dictionary<UniversalRenderPipelineAsset, TierBaseline> baselines =
            new Dictionary<UniversalRenderPipelineAsset, TierBaseline>();

        public static void Apply(ApplicationSettingsData.GraphicsSettings g)
        {
            // Quest quality is fixed on device: one tier, no user-facing overrides.
            if (PlatformCapabilities.IsQuestConfiguration)
                return;

            ApplyQualityTier(g.qualityTier);

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                var baseline = GetBaseline(urp);
                urp.renderScale = Mathf.Clamp(baseline.renderScale * g.renderScale, 0.5f, 2f);
                urp.shadowDistance = baseline.shadowDistance * Mathf.Clamp(g.shadowDistanceScale, 0.25f, 2f);
                if (g.antiAliasing >= 0)
                    urp.msaaSampleCount = Mathf.Clamp(g.antiAliasing, 1, 8);
                else
                    urp.msaaSampleCount = baseline.msaa;
            }

            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(g.textureQuality, 0, 2);

            ApplyCameraEffects(g.ambientEffects);
            ApplyTerrainDistances(g.terrainDistanceScale, g.vegetationDistanceScale);
        }

        static void ApplyQualityTier(string tierName)
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == tierName)
                {
                    if (QualitySettings.GetQualityLevel() != i)
                        QualitySettings.SetQualityLevel(i, applyExpensiveChanges: true);
                    return;
                }
            }
            Debug.LogWarning($"[Settings] Quality tier '{tierName}' not found; keeping " +
                             $"'{PlatformCapabilities.ActiveQualityTier}'.");
        }

        static TierBaseline GetBaseline(UniversalRenderPipelineAsset urp)
        {
            if (!baselines.TryGetValue(urp, out var b))
            {
                b = new TierBaseline
                {
                    renderScale = urp.renderScale,
                    shadowDistance = urp.shadowDistance,
                    msaa = urp.msaaSampleCount,
                };
                baselines[urp] = b;
            }
            return b;
        }

        static void ApplyCameraEffects(bool enabled)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null)
                data.renderPostProcessing = enabled;
        }

        class TerrainBaseline
        {
            public float basemapDistance;
            public float detailObjectDistance;
            public float treeDistance;
            public float treeBillboardDistance;
        }

        static readonly Dictionary<Terrain, TerrainBaseline> terrainBaselines =
            new Dictionary<Terrain, TerrainBaseline>();

        static void ApplyTerrainDistances(float terrainScale, float vegetationScale)
        {
            terrainScale = Mathf.Clamp(terrainScale, 0.25f, 2f);
            vegetationScale = Mathf.Clamp(vegetationScale, 0.25f, 2f);

            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null) continue;
                if (!terrainBaselines.TryGetValue(terrain, out var b))
                {
                    b = new TerrainBaseline
                    {
                        basemapDistance = terrain.basemapDistance,
                        detailObjectDistance = terrain.detailObjectDistance,
                        treeDistance = terrain.treeDistance,
                        treeBillboardDistance = terrain.treeBillboardDistance,
                    };
                    terrainBaselines[terrain] = b;
                }

                terrain.basemapDistance = b.basemapDistance * terrainScale;
                terrain.detailObjectDistance = b.detailObjectDistance * vegetationScale;
                terrain.treeDistance = b.treeDistance * vegetationScale;
                terrain.treeBillboardDistance = b.treeBillboardDistance * vegetationScale;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            baselines.Clear();
            terrainBaselines.Clear();
        }
    }

    /// <summary>
    /// Control settings application to the active desktop rig. Captures the
    /// authored RotationSpeed the first time it sees each controller so the
    /// sensitivity slider is always relative to the designed feel.
    /// </summary>
    public static class ControlSettingsController
    {
        static readonly Dictionary<StarterAssets.FirstPersonController, float> baseSpeeds =
            new Dictionary<StarterAssets.FirstPersonController, float>();

        public static void Apply(ApplicationSettingsData.ControlSettings c)
        {
            if (PlatformCapabilities.IsQuestConfiguration)
                return;

            float sensitivity = Mathf.Clamp(c.mouseSensitivity, 0.2f, 3f);

            foreach (var fpc in Object.FindObjectsByType<StarterAssets.FirstPersonController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!baseSpeeds.TryGetValue(fpc, out float baseSpeed))
                {
                    baseSpeed = fpc.RotationSpeed;
                    baseSpeeds[fpc] = baseSpeed;
                }
                fpc.RotationSpeed = baseSpeed * sensitivity;
                fpc.InvertY = c.invertY;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => baseSpeeds.Clear();
    }
}
