# BCaT Production Architecture — Native Desktop + Meta Quest

One shared Unity project, one shared scene set. Platform differences are
expressed through configuration and a small set of runtime services, never
through duplicated scenes or projects.

> **Updated 2026-08-07.** The platform layer was rebuilt per
> `16_PLATFORM_ARCHITECTURE_REVIEW.md`; see
> `17_PLATFORM_ARCHITECTURE_IMPLEMENTATION_LOG.md` for what changed and how it
> was validated. In short: one resolver (`BCaTPlatform`) plus one per-scene
> applier (`ScenePlatformBinding`) replaced `PlatformRigActivator` and
> `ScenePlatformRigSelector`; every scene now has a root `Platform` group with
> both branches **authored inactive**; and Quest is testable in the Editor via
> `BCaT → Platform Test Mode → Quest XR (Simulated)`.

```
Shared Core (Assets/BCaT/ProductionCore + existing systems)
├── Scenes: MainMenuScene → BH_XR_MainScene ⇄ LoadingScene ⇄ BlackKitchen_MemoryScene (Addressables)
├── Interaction contracts: IInteractionTarget / IExclusiveInteractionZone / InteractionRouter
├── Addressables lifecycle: AddressableSceneHandleStore + AddressablesHandleRegistry + logger
├── Media lifecycle: RuntimeMediaPaths + RemoteMediaConfig + MediaPlaybackRegistry + MediaErrorLog
├── Settings: ApplicationSettingsData / SettingsManager / apply-controllers (JSON, versioned)
├── Accessibility: SubtitleService, TranscriptViewer, text-scale/high-contrast theming
├── Navigation: ResetService (reset / unstuck / return-to-entrance), ExhibitDirectoryUi
└── Shared UI logic: UiFactory (runtime-built menus, consistent with existing runtime modals)

Desktop Profile (Windows 11 x64 · Apple Silicon macOS)
├── StarterAssets PlayerCapsule rig (kind=Desktop) activated by ScenePlatformBinding
├── DesktopInteractionInputProvider (E key + click, the ONLY sanctioned world-interaction poll)
├── Screen-space prompt (InteractionPromptUi) + per-exhibit world prompts
├── Desktop shell: MainMenuController, PauseMenuController, CrosshairController, quit confirmation
├── Quality tiers: Desktop Low / Desktop Standard (default) / Desktop High
├── Standard mode & Kiosk mode (ApplicationModeService, KioskController)
└── Windows/macOS media behavior: packaged StreamingAssets first, remote CCD fallback

Meta Quest Profile (Android + OpenXR, Quest feature group only)
├── XRI 3.4 XR Origin rig (kind=XR) activated by ScenePlatformBinding
├── QuestInteractionInputProvider (event-driven; XRSimpleInteractable → Router.RequestXRSelect)
├── World-space prompt HUD (InteractionPromptUi in WorldSpace mode, parented to the
│   head camera) — "Interact"/"Play — Name", never keyboard language. Plus the two
│   sanctioned exhibit-owned world prompts (Black Kitchen entrance, Privacy hologram)
├── Quest quality tier (fixed), Quest_RPAsset, IL2CPP/ARM64, Vulkan+GLES3
└── Comfort/locomotion: existing XRI rig configuration (validated on hardware by owner)
```

## Platform authority

`BCaT.Production.BCaTPlatform` is the single platform authority and the only
place allowed to touch build defines, `XRSettings` or XR Management. It resolves
the platform once, before the first scene object awakens, from an explicit
precedence chain: **editor override → `-bcatPlatform=` → build define → XR device
probe → desktop**. Only a probe-derived Desktop answer stays provisional (it may
promote to Quest once, as the previous per-call `IsXRActive()` polling did); a
Quest answer or any forced answer is final.

Platform *policy* lives in `BCaTPlatformProfile` assets (one per platform, in
`Resources/BCaT/Platform/`): rig kind, locomotion, input provider, prompt style,
UI input module, app shell, kiosk, swapchain ownership, quality tier, media
source policy, diagnostics verbosity, Addressables profile. Adding a capability
is a field, not a new `#if`.

`ScenePlatformBinding` applies that decision per scene, in `Awake`. It configures
the scene's single `EventSystem` **before** activating the rig branch — XRI's
`RegisteredUIInteractorCache` auto-creates its own EventSystem when it cannot
find an *active* one, so the other order yields two EventSystems and two
`XRUIInputModule`s on Quest.

Correctness does not depend on execution order: both platform branches are
**authored inactive**, and an inactive object never runs `Awake` at all.

### Capability facade (retained)

`BCaT.Production.PlatformCapabilities` remains as a facade over the resolver,
because a lot of production code already asks there. Every member now forwards:
IsDesktop / IsWindows / IsMacOS / IsQuestConfiguration / IsXRActive /
SupportsKeyboardMouse / SupportsQuestControllers / SupportsExternalLinks /
SupportsLocalMediaFileChecks / SupportsRemoteMedia / SupportsKioskMode /
ActiveMode / ActiveQualityTier. It wraps the existing
`InteractionPromptText.IsXRActive()` helper, and no capability exists for
phones, tablets, WebGL, or non-Quest headsets.

## Bootstrap

`BCaTAppBootstrap` (`RuntimeInitializeOnLoadMethod`) creates the one persistent
`BCaT_AppServices` object — the project's only DontDestroyOnLoad object —
hosting: PlatformRigActivator, InteractionRouter, PauseMenuController +
CrosshairController (desktop), KioskController (kiosk mode), SubtitleService.
It applies persisted settings at startup and after every single-mode scene
load, and captures the scene entry pose for ResetService. No scene edits were
required to introduce any of these systems.

## Interaction ownership (Phase 2)

Flow: candidates register with `InteractionRouter` → per-frame validation
(availability, distance, view angle, line of sight with trigger-skip and
self-collider-skip) → exactly one selected target → platform input provider →
one `OnInteract` dispatch → platform prompt (screen-space text on desktop,
target-owned world canvas on Quest). Global suppression comes from
`InteractionState` blockers (Menu, Modal, Media, Loading, Transition,
PlayerControl) plus the existing `SceneTransitionState.IsTransitionInProgress`.
A global 0.25 s cooldown suppresses duplicate activations.

The Black Kitchen station manager is registered as an
`IExclusiveInteractionZone`: the router keeps input/blocker/cooldown ownership
and forwards one per-frame signal; the manager keeps its validated
ray→angle→distance station selection and its shared prompt, and forwards
exit-aim presses to the experience controller. Focused/modal interfaces
(article readers, slideshows, image popups, the exit modal, video popups) read
their navigation keys from `FocusedUiInput` — the audited, single home for
modal keyboard reads — and register Modal/Media blockers with a force-close
action that the kiosk reset invokes.

XR path: existing XRSimpleInteractable wiring is preserved; select handlers
route through `InteractionRouter.RequestXRSelect` (or check
`InteractionState.IsBlocked` where prefab wiring calls methods directly), so
Quest obeys the same ownership rules without scene rewiring.

## Settings (Phase 4)

`ApplicationSettingsData` (schema-versioned) persists as JSON at
`<persistentDataPath>/BCaT/settings.json`; corrupt files are backed up and
replaced with defaults; missing files produce defaults. Apply-controllers:
Display (resolution/fullscreen/display index/vsync/fps cap), Graphics (tier +
render scale, shadow-distance scale, texture quality, MSAA, post-processing,
terrain/vegetation distance — applied as deltas over each tier's captured
baseline), Audio (AudioChannelService: Narration/Ambience/Effects/Media
channels over registered sources + AudioListener master; the Black Kitchen
coordinator folds the Narration/Ambience values into its own targets so its
validated exclusivity/ducking stays authoritative), Controls (mouse
sensitivity as a multiplier over the authored RotationSpeed + InvertY),
Accessibility (subtitles, text size, high contrast, reduced motion, persistent
prompts). No PlayerPrefs are scattered in exhibit code; no player progress is
persisted.

## Media lifecycle (Phase 6)

Resolution order on desktop/Quest: packaged StreamingAssets first (offline
institutional default; on Quest, packaged files are declared in
`RemoteMediaConfig.packagedFileNames` because APK contents cannot be probed) →
per-file remote URL → remote base URL. WebGL remnants keep their legacy
remote-first order. `MediaVideoController` adds: prepare watchdog timeout
(default 20 s) with a visitor-facing message, `MediaErrorLog` structured
failures (exhibit, sanitized path, platform, error, fallback, recovery),
registration with `MediaPlaybackRegistry` (kiosk deferral + StopAll), and
subtitle notifications. All previous cleanup behavior (stop on close,
RenderTexture release/clear, URL clearing, event unsubscription, cursor
restore) is preserved.

## Addressables lifecycle (Phase 7)

Every load has a named owner and a stored handle. The existing
`AddressableSceneHandleStore` remains the owner of the Black Kitchen scene
handle; it now reports create/release to `AddressablesHandleRegistry`, which
detects duplicate loads, releases-without-ownership, and leaked handles, and
feeds the repeat-entry smoke test. `AddressablesLifecycleLog` is verbose in
development builds and opt-in (`-bcatAddressablesLog`) in release builds.

The BlackKitchen group's build/load paths moved from the WebGL-era remote CCD
profile variables to **Local.BuildPath/Local.LoadPath**: institutional desktop
and Quest installs must work offline, no CCD content was ever published for
the three native targets, and the optimized-mesh change forced a rebuild
anyway. Content rebuilds automatically with every player build
(ProductionBuildPipeline builds Addressables before the player).

Heavy-transition sequence (unchanged flow, now instrumented): block
interaction (transition flag) → fade → stop media (MediaPlaybackRegistry +
audio coordinator exit preparation) → single-mode load of LoadingScene →
`Resources.UnloadUnusedAssets` + one controlled GC in the loading scene (the
pre-existing, profiler-justified single collection — not repeated forced GC) →
load destination (built-in or Addressables) → release the previous scene
handle → arrival teleport → fade in.

## Standard and kiosk modes (Phase 8)

`ApplicationModeService` resolves the mode once per launch: command line
(`-kiosk` / `-standard` / `-bcatMode=…`) overrides
`<persistentDataPath>/BCaT/mode.config.json`, default Standard. Kiosk
configuration lives in `kiosk.config.json` (timeout, media-deferral policy,
fixed tier, admin chord toggles) with CLI overrides
(`-bcatKioskTimeout=`, `-bcatKioskQuality=`). Kiosk behavior: fullscreen
enforced, quality locked, quit hidden from menus, Display/Graphics/Controls
tabs withheld from visitors, inactivity tracking (keys, mouse move/buttons,
menu and media activity) that defers while long-form media plays, and a reset
sequence that runs entirely through the shared lifecycle (stop media →
force-close all blockers → return-to-entrance transition → restore
cursor/controls/prompts). Administrator controls (documented, not
discoverable): hold Ctrl+Shift+Q to quit; Ctrl+Shift+F10 for the unrestricted
settings panel with config/log paths. No credentials are stored anywhere.

## Accessibility (Phase 9)

Subtitles: `SubtitleService` + `SubtitleTrack` assets under
`Resources/Subtitles` keyed by media id (video file name or Black Kitchen
narrative id). **No transcript content is invented** — as of this pass no
approved transcript/subtitle source material exists in the project, so the
system ships structurally complete but empty, and this gap is reported in the
accessibility report. The transcript viewer, text-size scaling, high-contrast
theming, reduced motion, and persistent prompts all flow from the settings.
The exhibit directory derives its entries from live scene content (known
exhibit component types grouped by their authored organizer ancestors) so it
can never present invented exhibits; no floor map is shown because no approved
map asset exists.

## Quality tiers (Phase 5)

Desktop Low (0) / Desktop Standard (1, default) / Desktop High (2) / Quest (3).
Mapping from the previous tiers: Mobile→Desktop Low (URP Mobile_RPAsset),
PC→Desktop Standard (URP PC_RPAsset), new DesktopHigh_RPAsset (2048 shadowmap,
45 m shadows, 2 cascades, 2×MSAA, soft-shadow quality high, lodBias 2.5),
Quest unchanged (Quest_RPAsset). The previous QualitySettings.asset was
malformed YAML (the Quest entry was merged into the PC entry and a dangling
empty entry followed); the rewrite normalized it. Exact values:
`Docs/Production/00_BASELINE_STATE.md` (before) and the quality report
(after).
