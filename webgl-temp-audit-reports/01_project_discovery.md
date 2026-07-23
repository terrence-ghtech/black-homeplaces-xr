# Stage 1 — Project Discovery
Date: 2026-07-23
Project: /Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST
Method: read-only inspection of ProjectSettings, Packages, Assets, and existing build outputs. No project files modified during this stage.

## Unity version
- Editor: **6000.4.5f1** (cc83ebd631f8) — installed at /Applications/Unity/Hub/Editor/6000.4.5f1 with WebGLSupport and AndroidPlayer modules.
- Unity Editor was NOT running at audit start (Temp/UnityLockfile present but stale; no Unity process).

## Render pipeline
- GraphicsSettings.m_CustomRenderPipeline → GUID 4b83569d67af61e458304325a23e5dfd = **Assets/Settings/PC_RPAsset.asset** (URP).
- Quality levels (QualitySettings.asset):
  - Level 0 "Mobile" (current, m_CurrentQuality: 0) → GUID 5e6cbd92db86f4b18aec3ed561671858 = **Assets/Settings/Mobile_RPAsset.asset**
  - Level 1 "PC" → PC_RPAsset.asset
- URP 17.4.0; HDRP 17.4.0 package is also installed (com.unity.render-pipelines.high-definition) but no HDRP pipeline asset is assigned in Graphics/Quality settings. Assets/HDRPDefaultResources exists in the project.

## Build target / WebGL player settings (ProjectSettings.asset)
- webGLCompressionFormat: **0 (Brotli)**
- webGLDecompressionFallback: 0 (off)
- webGLLinkerTarget: 1 (WebAssembly)
- webGLThreadsSupport: 0
- webGLInitialMemorySize: 532 MB, max 2048 MB, growth mode 2 (geometric)
- webGLExceptionSupport: 1 (explicitly thrown only)
- webGLDataCaching: 1
- webGLTemplate: APPLICATION:Default (built-in Default template; Assets/WebGLTemplates exists but is not the selected template)
- managedStrippingLevel WebGL: **2 (Medium)**
- il2cppCodeGeneration WebGL: 1
- scriptingBackend: WebGL implicitly IL2CPP (only Android:1 listed; WebGL is always IL2CPP)
- preloadedAssets: **[] (empty)**

## Build Settings scenes (EditorBuildSettings.asset)
Enabled (3):
1. Assets/BH_XR_MainScene.unity (682df8046c3564f1b92d250dd0fcf67b)
2. Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity (22bc6237247ca41a09d4974e94a690a7)
3. Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity (e4b6edc3be9c46caada6c5451608bc17)

Disabled: none listed.

Config objects (production roots referenced by settings):
- com.unity.input.settings → GUID 9e7be553448fa2546aea5752021cbcf7 — **DANGLING**: no asset with this GUID exists in Assets/, Packages/, or ProjectSettings/. Unity falls back to default InputSystem settings.
- com.unity.input.settings.actions → GUID 052faaac586de48259a63d0c4782560b = **Assets/StarterAssets/InputSystem/StarterAssets.inputactions** (project-wide input actions)
- com.unity.xr.management.loader_settings → **Assets/XR/XRGeneralSettingsPerBuildTarget.asset**
- com.unity.xr.openxr.settings4 → OpenXR package settings (Assets/XR/Settings/)

## XR
- XRGeneralSettingsPerBuildTarget contains settings for **Android only** (Keys: 07000000 = BuildTargetGroup.Android; OpenXR loader). No WebGL/Standalone XR loader entry → **XR does not initialize in the WebGL build**.
- XR Interaction Toolkit 3.4.1 + OpenXR 1.16.1 packages installed; XRI runtime settings live in Assets/XRI/Settings/Resources (Resources folder → always packed).
- Imported package samples: Assets/Samples/XR Interaction Toolkit/3.3.1 (Starter Assets, XR Device Simulator), Assets/StarterAssets.

## Resources folders (contents always packed into builds)
1. Assets/Resources → UnityGLTFSettings.asset (only file)
2. Assets/XRI/Settings/Resources → InteractionLayerSettings.asset, XRDeviceSimulatorSettings.asset, XRInteractionRuntimeSettings.asset
3. Assets/TextMesh Pro/Resources → TMP Settings, default font assets, shaders, style sheets
4. Assets/IgniteCoders/Simple Water Shader/Resources → (water shader package resources)

## StreamingAssets (copied wholesale into build)
6 MP4 videos, ~196 MB total:
| File | Size |
|---|---|
| and that is the truth - you know what I'm meaning_720p.mp4 | 106.9 MB |
| in_my_sisters_room_xr.mp4 | 52.8 MB |
| Linda_Leaks_CHOF_720p.mp4 | 27.5 MB |
| you don't know about style my darling_720p.mp4 | 11.5 MB |
| such lovely gravy_720p.mp4 | 6.1 MB |
| subjected_to_recognition_720p.mp4 | 0.23 MB |

## Addressables / AssetBundles
- No Assets/AddressableAssetsData. Addressables package not in manifest. **Not used.**
- No AssetBundle build config or runtime AssetBundle API usage found in project scripts. `com.unity.modules.assetbundle` module enabled but unused by project code.

## Runtime loading mechanisms (discovered by code search of Assets/**.cs, excluding Editor folders)
- **Resources.Load: none** in project runtime code.
- **SceneManager.LoadSceneAsync (string-based)**:
  - Assets/BCaT/SceneTransitions/Scripts/LoadingSceneController.cs:45 → loads SceneTransitionState.DestinationSceneName
  - Assets/BCaT/Exhibits/BlackKitchen/Scripts/BlackKitchenExperienceController.cs:371 and BlackKitchenPortalController.cs:93 → load loadingSceneName
  - Scene name constants (SceneTransitionState.cs): "BH_XR_MainScene", "BlackKitchen_MemoryScene", "LoadingScene" — exactly the 3 enabled scenes.
- **StreamingAssets video URLs** (VideoPlayer.url via Application.streamingAssetsPath):
  - Assets/Scripts/RuntimeMediaPaths.cs (path builder)
  - Assets/Scripts/MediaVideoController.cs (serialized videoFileName; scene instances reference: "and that is the truth - you know what I'm meaning_720p.mp4", "subjected_to_recognition_720p.mp4", "such lovely gravy_720p.mp4", "you don't know about style my darling_720p.mp4")
  - Assets/Scripts/QuiltVideoPopUp.cs → hardcoded "in_my_sisters_room_xr.mp4"
  - Assets/BCaT_assets/LindaLeaks/Scripts/LindaLeaksVideoPopUp.cs → serialized "Linda_Leaks_CHOF_720p.mp4" (in LindaLeaks_Exhibit_VintageCamera.prefab, instanced in BH_XR_MainScene)
  - **All 6 StreamingAssets videos are referenced by production scenes/prefabs/code.**
- **UnityWebRequest: none** in project runtime code.
- No JSON/config-file-driven asset path loading found in project runtime code.

## Runtime initialization / persistent systems
- Scene flow: BH_XR_MainScene ⇄ LoadingScene ⇄ BlackKitchen_MemoryScene via SceneTransitionState static class (no persistent GameObjects across scenes discovered by search; state carried in statics).
- preloadedAssets is empty; no [RuntimeInitializeOnLoadMethod] bootstrap of external assets found tied to asset loading.

## Production dependency roots (discovered, not assumed)
1. The 3 enabled Build Settings scenes (above)
2. Graphics/Quality URP pipeline assets: Assets/Settings/PC_RPAsset.asset, Assets/Settings/Mobile_RPAsset.asset (+ their renderer assets via dependency)
3. Assets/StarterAssets/InputSystem/StarterAssets.inputactions (project-wide input actions)
4. XR management settings (Assets/XR/…) — Android-only at runtime, still serialized into settings
5. All 4 Resources folders' contents (always packed)
6. All StreamingAssets files (copied wholesale; all 6 also referenced)
7. Project scripts compiled into Assembly-CSharp (subject to IL2CPP Medium stripping)

## Existing build outputs found in project root (evidence of current production configuration)
- **webgl-phase9/** — the current production WebGL build ("Phase 9", served by tools/serve_webgl.py by default):
  - Build/127e97601aab8155213b16a630621636.data.br — **705,798,466 bytes (~706 MB)**
  - Build/97bb6c951b33046c99ccda3a25c7f391.wasm.br — 9,481,388 bytes
  - Build/1cf9e49c1a27a570ecb00483bb4605bc.framework.js.br — 85,745 bytes
  - Build/ca10e9f6e1e2f32a022b15efaddafedb.loader.js — 26,982 bytes
  - StreamingAssets/ — 196 MB; total folder **878 MB**
- WebGL Builds/, build/, webgl-temp/… none named webgl-temp-audit (audit folder will be fresh).
- tools/serve_webgl.py — local Brotli-header HTTP server (default dir webgl-phase9, port 8080). This is the "existing Brotli-compatible local server" for Stage 11.

## Project scale
- Assets/ = 6.7 GB, 5,864 non-.meta files. Library/ = 33 GB (warm import cache for 6000.4.5f1).
- Free disk space on volume: 857 GB.
- Notable top-level Assets content: many third-party packs (Animated Tropical Vegetation, Coconut Palm Tree Pack, DevDen Arch Viz Scotland, Food Pack-Demo, Furniture Mega Pack, Idyllic Italian Coast Town, LowPolyLivingRoomPack, Yughues palms/cobble/pavements, danthaigames, BrokenVector, TerrainSampleAssets, TutorialInfo, UnityTechnologies, _Recovery (24+ recovered scene copies), AssetsStore, Emilulz_Assets, HyTeKGames, LumiStudio, picture-frame, etc.), BCaT/BCaT_assets (project content), XR/XR 1..XR 5 (duplicated XR settings folders), WebGLTemplates.

## Facts worth flagging (no action taken)
- The com.unity.input.settings config object GUID is dangling (asset missing). Left untouched.
- Multiple XR settings folders (XR, XR 1 … XR 5) exist; only Assets/XR is referenced by EditorBuildSettings config objects.
- No custom build scripts exist in Assets — previous builds were produced through the Editor GUI. Batch-mode build tooling will be added by this audit (documented as a created file).
