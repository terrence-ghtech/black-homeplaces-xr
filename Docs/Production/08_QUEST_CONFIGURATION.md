# Meta Quest Configuration Report (build-time findings only)

**No physical-headset claim is made anywhere in this report.** Runtime
behavior (controllers, media, comfort, thermal, frame rate) is deferred to
`04_DEFERRED_TESTS_QUEST.md`.

## Configuration inspected & status

| Area | Status | Detail |
|---|---|---|
| Build target | ✅ configured | Android; IL2CPP; ARM64 only (`AndroidTargetArchitectures: 2`) |
| Graphics APIs | ✅ configured | Vulkan + OpenGLES3 (manual order) |
| XR loader | ✅ configured | XR Management Android-only settings (no Standalone XR — desktop never initializes XR); OpenXR loader `Assets/XR/Loaders/OpenXRLoader.asset`, InitManagerOnStart=1 |
| OpenXR features | ✅ Quest-only | Enabled: Meta Quest Support, Meta Quest Touch Plus, Touch Pro, Oculus Touch profiles. All non-Quest headset profiles present but disabled. Render mode: Single Pass Instanced/Multiview |
| XR Interaction Toolkit | ✅ 3.4.1 | XR Origin rig (XRI starter prefab) marked `ScenePlayerRig(kind=XR)` in both production scenes |
| Rig selection | ✅ fixed this pass | Baseline defect: the main scene's XR rig container was authored inactive with **no rig selector in that scene** — Quest would have booted with the desktop rig. `PlatformRigActivator` (bootstrap service) now activates the platform rig in every scene, including through inactive organizer containers, and disables the XR Device Simulator in player builds |
| Quest input events | ✅ in source/scene | XRSimpleInteractable select wiring preserved; selects now validate through the shared InteractionRouter/blocker rules |
| Quest prompts | ✅ distinct | All prompt text sources return "Interact …" wording when XR is active; desktop keyboard language cannot appear (single GetPrompt(xr)/InteractionPromptText source) |
| Quality | ✅ configured | Dedicated `Quest` tier (Android default), Quest_RPAsset: 4×MSAA, 20 m shadows, per-vertex additional lights, no additional-light shadows. The malformed baseline QualitySettings.asset (Quest entry merged into PC entry, dangling empty entry that Android index 2 pointed at) was rewritten — this was a potential real Quest rendering blocker |
| Texture compression | ✅ | Android default ASTC per project settings; Quest texture overrides from the optimization pass preserved |
| Media paths | ✅ configured | Packaged StreamingAssets (APK) declared via `RemoteMediaConfig.packagedFileNames`; VideoPlayer uses the jar: StreamingAssets URL; remote CCD fallback retained. H.264 720p MP4s are within Quest decoder capabilities (runtime confirmation deferred) |
| Addressables | ✅ configured | BlackKitchen group local build/load paths; content builds into the APK with every player build |
| Scene transitions | ✅ shared | Same MainMenuScene(auto-forward on Quest) → house ⇄ LoadingScene ⇄ Black Kitchen flow as desktop |
| App identity | ✅ fixed this pass | Was URP-template identifier; now `org.bcatlab.blackhomeplaces`; minSdk raised 25 → 29 (Quest devices are API 29+) |
| Recenter/reset | ✅ in config | XRI rig recenter behavior + fall recovery + arrival teleport hardening are platform-shared |

## Items that CANNOT be verified without the headset (deferred)
Controller bindings in practice · ray/direct interaction feel · world prompt
readability · media playback/decode on device · offline behavior on device ·
comfort (locomotion/turn) · performance/thermal/memory · long-session
stability · subtitle readability. Full protocol with results template:
`04_DEFERRED_TESTS_QUEST.md`.

## Comfort settings position
The existing XRI rig's locomotion configuration is preserved unchanged
(per plan: expose only what can be verified without hardware; do not assume
every locomotion mode is required). Snap-turn/teleport/speed adjustments are
deliberately left to a post-headset-validation pass so defaults are chosen
against real comfort feedback rather than guesses.
