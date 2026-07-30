# BCaT Production Baseline State (recorded 2026-07-29, before implementation)

This document records the project state **before** the native desktop / Meta Quest
production implementation pass, per the implementation plan Phase 0 requirements.

## Unity and packages

- Unity Editor: **6000.4.5f1** (revision cc83ebd631f8)
- Editor install: `/Applications/Unity/Hub/Editor/6000.4.5f1`
- Playback engines present before this pass: `AndroidPlayer`, `WebGLSupport`, `MacStandaloneSupport` (built-in)
- Playback engine added during this pass (environment, not project, change):
  `WindowsStandaloneSupport` (Mono variations only — Windows IL2CPP cannot be
  cross-compiled from macOS; official `UnitySetup-Windows-Mono-Support-for-Editor-6000.4.5f1.pkg`
  payload installed 2026-07-29)
- Android toolchain: bundled OpenJDK, SDK, NDK present under AndroidPlayer playback engine.

Key packages (`Packages/manifest.json`):

| Package | Version |
|---|---|
| com.unity.addressables | 2.9.1 |
| com.unity.inputsystem | 1.19.0 |
| com.unity.render-pipelines.universal | 17.4.0 |
| com.unity.render-pipelines.high-definition | 17.4.0 (present, unused by active pipeline) |
| com.unity.xr.interaction.toolkit | 3.4.1 |
| com.unity.xr.openxr | 1.16.1 |
| com.unity.cinemachine | 3.1.6 |
| org.khronos.unitygltf | 2.19.5 (Needle scoped registry) |
| com.unity.probuilder | 6.0.9 |
| com.unity.timeline | 1.8.12 |
| com.unity.visualscripting | 1.9.11 |

## Player settings (ProjectSettings/ProjectSettings.asset)

- companyName: `BCaT Lab`
- productName: `Black Homeplaces: The XR House`
- bundleVersion: `0.1.0`
- applicationIdentifier (all platforms): URP **template defaults**
  (`com.UnityTechnologies.com.unity.template.urpblank` for Android) — needs correction.
- Color space: Linear (`m_ActiveColorSpace: 1`)
- fullscreenMode: 1 (Fullscreen Window), defaultScreen 1024x768, resizableWindow: 0,
  runInBackground: 1, visibleInBackground: 1, allowFullscreenSwitch: 1
- Scripting backend: Android = **IL2CPP** (`scriptingBackend: Android: 1`),
  Standalone = default (**Mono**)
- AndroidTargetArchitectures: 2 (**ARM64**)
- AndroidMinSdkVersion: 25, AndroidTargetSdkVersion: 0 (highest installed)
- Android graphics APIs (manual): **Vulkan + OpenGLES3**
- Standalone graphics APIs: automatic (Metal on macOS; D3D on Windows)
- apiCompatibilityLevel: 6 (.NET Standard 2.1)
- preloadedAssets: [] (empty)

## Build scene list (ProjectSettings/EditorBuildSettings.asset)

| # | Scene | Enabled |
|---|---|---|
| 0 | `Assets/BH_XR_MainScene.unity` | yes |
| — | `Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity` | **no** (loaded via Addressables) |
| 1 | `Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity` | yes |

Config objects registered: Addressables settings, Input System settings,
project-wide input actions (GUID `052faaac586de48259a63d0c4782560b` — not found under
`Assets/` or package cache; dangling reference to be verified in editor),
XR Management loader settings, OpenXR settings.

## XR configuration

- `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`: contains **Android build target
  settings only** (Keys `07000000`). No Standalone XR settings — XR never initializes
  in desktop builds (desired for the native desktop edition).
- Android XR manager: `m_AutomaticLoading: 0`, `m_AutomaticRunning: 0`,
  `m_InitManagerOnStart: 1`, single loader: `Assets/XR/Loaders/OpenXRLoader.asset`.
- OpenXR Android settings (`Assets/XR/Settings/OpenXR Package Settings.asset`):
  - renderMode: 1 (Single Pass Instanced / Multiview)
  - Enabled Android features: **Meta Quest Support (MetaQuestFeature)**,
    Meta Quest Touch Plus profile, Meta Quest Touch Pro profile, Oculus Touch profile.
  - All other-headset interaction profiles (HTC Vive etc.) present but **disabled**.
- Stale duplicate folders exist: `Assets/XR 1..XR 5`, `Assets/XR/Settings 1..Settings 5`
  (duplicated settings assets, not referenced by `EditorBuildSettings.m_configObjects`).
  Active set is `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` + `Assets/XR/Settings/`.

## Quality settings (ProjectSettings/QualitySettings.asset) — BEFORE changes

Three tiers, each with a dedicated URP asset in `Assets/Settings/`:

| Field | Mobile (idx 0) | PC (idx 1) | Quest (idx 2) |
|---|---|---|---|
| pixelLightCount | 2 | 2 | 2 |
| shadows | 2 | 2 | 2 |
| shadowResolution | 1 | 1 | 1 |
| shadowCascades | 2 | 2 | 2 |
| shadowDistance | 30 | 40 | 20 |
| antiAliasing | 2 | 0 | 4 |
| vSyncCount | 0 | 0 | 0 |
| lodBias | 1.2 | 2.0 | 1.2 |
| maximumLODLevel | 0 | 0 | 0 |
| textureMipmapLimit | 0 | 0 | 0 |
| URP asset | Mobile_RPAsset | PC_RPAsset | Quest_RPAsset |

Per-platform default quality: Standalone → 1 (PC), Android → 2 (Quest), WebGL → 0 (Mobile).

URP asset values — BEFORE changes:

| Field | Mobile_RPAsset | PC_RPAsset | Quest_RPAsset |
|---|---|---|---|
| HDR | 1 | 1 | 1 |
| MSAA | 2 | 1 | 4 |
| renderScale | 1.0 | 1.0 | 1.0 |
| LOD cross-fade | 1 | 1 | 1 |
| main light shadows | 1 | 1 | 1 |
| main shadowmap res | 1024 | 1024 | 1024 |
| additional lights mode | 1 (per-vertex) | 0 (per-pixel)* | 1 (per-vertex) |
| additional lights limit | 4 | 4 | 4 |
| additional light shadows | 0 | 1 | 0 |
| shadow distance | 30 | 25 | 20 |
| cascades | 1 | 1 | 1 |
| soft shadows | 0 | 1 | 0 |

*PC additional-lights value 0 = per-vertex disabled/`PerVertex`? URP enum: 0=Disabled, 1=PerVertex, 2=PerPixel — PC has additional lights **Disabled** with additional light shadows flag set (inert).

Renderers: `Mobile_Renderer.asset`, `PC_Renderer.asset` (no dedicated Quest renderer asset in `Assets/Settings/`).

## Addressables — BEFORE changes

- Settings: `Assets/AddressableAssetsData/AddressableAssetSettings.asset`
- Profiles: single profile **Default**:
  - Local.BuildPath: `[Addressables.BuildPath]/[BuildTarget]`
  - Local.LoadPath: `{Addressables.RuntimePath}/[BuildTarget]`
  - Remote.BuildPath: `ServerData/[BuildTarget]`
  - Remote.LoadPath: Unity CCD production badge URL
    (`https://16fa2ad5-….client-api.unity3dusercontent.com/…/release_by_badge/production/entry_by_path/content/?path=ServerData/[BuildTarget]`)
- `m_BuildRemoteCatalog: 0` (no remote catalog)
- Groups:
  - **Default Local Group** (local paths)
  - **BlackKitchen_Remote**: BuildPath=Remote.BuildPath, LoadPath=**Remote.LoadPath (CCD)**,
    LZ4, PackTogether, IncludeInBuild=1, contains the Black Kitchen memory scene.
- `ServerData/` contains **WebGL bundles only** plus two loose
  `blackkitchen_remote_scenes_all_*.bundle` files. **No StandaloneOSX, StandaloneWindows64,
  or Android Addressables content has ever been built** — Black Kitchen content must be
  rebuilt for all three native targets (also required because optimized meshes changed).
- `Assets/AddressableAssetsData/WebGL/` contains WebGL-era addressables link data (remnant).

## Known keyboard-polling scripts (production, recorded before migration)

From `grep Keyboard.current|Input.GetKey|KeyCode` (excluding samples/asset-store/editor):

1. `Assets/BCaT/Exhibits/BlackKitchen/Scripts/BlackKitchenExperienceController.cs`
2. `Assets/BCaT/Exhibits/BlackKitchen/Scripts/BlackKitchenInteractionManager.cs`
3. `Assets/BCaT/Exhibits/BlackKitchen/Scripts/BlackKitchenPortalController.cs`
4. `Assets/BCaT/Exhibits/PrivacyLawExhibit/Scripts/PrivacyLawExhibitController.cs`
5. `Assets/BCaT_assets/LindaLeaks/Scripts/LindaLeaksPanelOpener.cs`
6. `Assets/BCaT_assets/LindaLeaks/Scripts/LindaLeaksVideoPopUp.cs`
7. `Assets/Scripts/HolographicSlideshow.cs`
8. `Assets/Scripts/InteractableLinkLauncher.cs`
9. `Assets/Scripts/MediaVideoController.cs`
10. `Assets/Scripts/MeshellArticleNotebookInputRouter.cs`
11. `Assets/Scripts/MeshellArticleReaderController.cs`
12. `Assets/Scripts/QuiltVideoPopUp.cs`
13. `Assets/Scripts/SimpleImagePopupController.cs`
14. `Assets/Scripts/SimpleImagePopupInteractor.cs`
15. `Assets/Scripts/SpatialAudioToggle.cs`

(Non-production hits excluded: `Animated Tropical Vegetation/DemoSceneContent/HideShowButtons.cs`,
`RealBlend/Editor/*` editor tools.)

Detailed per-script interaction/media audits are in
`01_INTERACTION_MIGRATION_REPORT.md` and `06_MEDIA_VALIDATION_REPORT.md`.

## Build outputs present at project root (pre-existing, untouched)

`WebGL Builds/`, `build/`, `build.app`, `app-build.apk`, `quest.apk`,
`webgl-*` folders, `deployment/`, `ServerData/` — all pre-date this pass.
