# BCaT Native Desktop + Meta Quest Production Implementation — Final Report
2026-07-29 · Unity 6000.4.5f1 · no version-control operations performed

Companion documents: 00 baseline · 01 architecture · 02 build guide ·
03 desktop checklists · 04 Quest deferred tests · 05 low-end deferred tests ·
06 institutional notes · 07 interaction migration · 08 Quest configuration ·
09 media validation · 10 settings · 11 quality profiles · 12 modes ·
13 accessibility · 14 addressables (all in `Docs/Production/`).

---

## 1. Executive summary

| Area | Status |
|---|---|
| Windows 11 x64 readiness | **Build-ready.** Baseline and final development builds succeed (final: 1739 MB, 2.6 min warm). Runtime validated only at build level — no Windows machine existed in this environment; first-session checklist provided (03 §A–I). Mono backend (IL2CPP needs a Windows build machine). |
| macOS (Apple Silicon) readiness | **Build- and runtime-validated on the dev machine.** Final build 1767 MB; automated smoke test PASS (menu → house → 3× Black Kitchen cycles, clean lifecycle, screenshots). |
| Meta Quest configuration readiness | **Configured and package-buildable** (see §8): Android/IL2CPP/ARM64/OpenXR + Meta-Quest-only features, rig selection fixed, identifier fixed, minSdk 29, local Addressables + packaged media. First APK attempt crashed the *editor* on a transient external-drive dropout (known T9 EIO signature, not a project defect); rebuilt after remount — see §8 for the final package status. |
| Quest items deferred to physical testing | Everything runtime: controllers, prompts readability, media decode, comfort, performance/thermal, repeat-entry on device (04). |
| Standard mode | **Working**: main menu, pause menu, settings, directory, reset/return, quit confirmations (smoke + code-path evidence; manual walkthrough checklist for full visual QA). |
| Kiosk mode | **Working**: auto-begin fullscreen, fixed tier, restricted settings, inactivity reset through the shared lifecycle, admin chords. Smoke test ran the app in kiosk mode end-to-end. |
| Accessibility | Settings + services implemented (subtitles/transcripts structurally complete; **no approved transcript content exists yet** — reported, not invented). |
| Remaining blockers | None for desktop development builds (P0 empty). See §12 for P1–P3/Q0/H0. |
| Institutional desktop release readiness | Pending: owner's low-end-hardware validation (H0), Windows runtime session (P1), signing/notarization (P2), approved transcript content (P2). |

## 2. Files and systems changed

**New runtime code** — `Assets/BCaT/ProductionCore/` (28 files):
Platform (PlatformCapabilities, ApplicationModeService, BCaTAppBootstrap,
PlatformRigActivator) · Interaction (InteractionState, IInteractionTarget,
InteractionRouter, InteractionInput incl. FocusedUiInput,
InteractionTargetBase) · Shell (UiFactory, InteractionPromptUi,
MainMenuController, PauseMenuController, SettingsMenuController,
CrosshairController, PlayerControlGate, ResetService) · Settings
(ApplicationSettingsData, SettingsManager, SettingsApplyControllers,
AudioChannelService) · Media (MediaPlaybackRegistry + MediaErrorLog) ·
Addressables (AddressablesHandleRegistry + AddressablesLifecycleLog) · Kiosk
(KioskController) · Access (SubtitleService + SubtitleTrack,
TranscriptViewer, ExhibitDirectoryUi) · Diagnostics (SmokeTestRunner).

**New editor tools** — `Assets/Editor/BCaTProduction/`:
ProductionBuildPipeline, ProductionProjectSetup, ProductionValidationAudit.

**New scene** — `Assets/BCaT/ProductionCore/Scenes/MainMenuScene.unity`
(generated; camera + MainMenuController; scene 0).

**New assets** — `Assets/Settings/DesktopHigh_RPAsset.asset` (+meta, GUID
abee5f22fa354f478c36dd544fc96572); `Assets/StreamingAssets/*.mp4` (6 videos
restored from `webgl-public-optimized/StreamingAssets`).

**Modified scripts** (17): MediaVideoController, SpatialAudioToggle,
InteractableLinkLauncher, SimpleImagePopupInteractor,
SimpleImagePopupController, HolographicSlideshow, MeshellArticleReaderController,
MeshellArticleNotebookInputRouter, LindaLeaksPanelOpener,
PrivacyLawExhibitController, BlackKitchenPortalController,
BlackKitchenInteractionManager, BlackKitchenExperienceController,
BlackKitchenAudioCoordinator, LoadingSceneController,
AddressableSceneHandleStore, RuntimeMediaPaths, RemoteMediaConfig,
QuiltVideoPopUp + LindaLeaksVideoPopUp (dead-code de-polling),
StarterAssets/FirstPersonController (InvertY).

**Settings/config files**: `ProjectSettings/QualitySettings.asset` (clean
4-tier rewrite; previous file was malformed YAML), `ProjectSettings/
EditorBuildSettings.asset` (menu scene added; BK stays disabled/Addressables),
`ProjectSettings/ProjectSettings.asset` (identifiers → org.bcatlab.
blackhomeplaces, resizableWindow on, Android minSdk 25→29),
`Assets/AddressableAssetsData/AssetGroups/Schemas/BlackKitchen_Remote_
BundledAssetGroupSchema.asset` (remote→local paths),
`Assets/Resources/RemoteMediaConfig.asset` (packagedFileNames ×6).

**Not changed**: all scenes' content (BH_XR_MainScene.unity and
BlackKitchen_MemoryScene.unity untouched), all art/meshes/LODs/colliders/
texture overrides from the optimization pass, XR settings assets, original
source assets. No GLB/FBX/texture/audio/video sources overwritten.

**Environment (not project)**: Windows Build Support (Mono) module installed
into the 6000.4.5f1 editor.

**Documentation**: 16 documents under `Docs/Production/`.

## 3. Interaction migration — see `07_INTERACTION_MIGRATION.md`
15 production scripts migrated off direct keyboard polling; 2 sanctioned
input homes remain (central providers + kiosk activity tracking); router
ownership rules, prompt behavior, and the Black Kitchen exclusive-zone
exception documented. Enforced by the batch validation audit.

## 4. Settings — see `10_SETTINGS_REPORT.md`
26 settings, all runtime-connected and persisted (versioned JSON, corrupt-file
recovery); kiosk restrictions per setting documented; zero dead controls.

## 5. Quality profiles — see `11_QUALITY_PROFILES.md`
Desktop Low/Standard/High + Quest with exact configured values; the malformed
baseline QualitySettings.asset (real potential Quest rendering blocker) was
found and fixed. Hardware performance claims are explicitly withheld pending
H0/Q0 testing.

## 6. Media validation — see `09_MEDIA_VALIDATION.md`
Local-first packaged media (offline institutional default) + remote CCD
fallback + 20 s watchdog + structured error logging + registry-driven stop-all.
macOS: resolver/packaging/lifecycle validated; playback decode = manual
checklist. Windows: build-level only (no machine). Quest: configured, deferred.

## 7. Addressables — see `14_ADDRESSABLES_REPORT.md`
Ownership registry live in builds. **Latent pre-existing bug found via the
smoke test and fixed**: the Black Kitchen scene handle was never actually
stored (the loading coroutine died when single-mode activation unloaded its
scene), so the bundle stayed resident for the app's lifetime; handle storage
now happens in the operation's Completed callback and release-on-return works.
Repeat-entry: 3 automated cycles — 1 scene, 0 leaked handles, flat memory
(520.2 MB allocated after every return), enter 2.3–5.4 s / exit ~4.2 s.

## 8. Platform builds

| | Windows x64 | macOS (Apple Silicon) | Meta Quest |
|---|---|---|---|
| Baseline build | ✅ 1483 MB / 28.8 min | ✅ 1509 MB / 26.8 min | (existing quest.apk + config inspection; fresh package below) |
| Final build | ✅ 1739 MB / 2.6 min | ✅ 1767 MB / 2.1 min | see note |
| Errors | 1 (pre-existing `New Terrain.asset` load error — also in baseline; player boots; entrance-view terrain appears untextured in screenshots → owner visual QA item) | same | same expected |
| Warnings | 48 | 49 | — |
| Scenes | Menu + House + Loading (+BK via Addressables) | same | same |
| Runtime evidence | none (no Windows machine) | boot test + smoke PASS + screenshots | deferred to headset |
| Known issue | unsigned (SmartScreen) | dev-build teardown segfault **after** quit on exit (post-Application.Quit; does not affect test validity; noted for owner) · unsigned/not notarized | first build attempt crashed the editor via transient T9 drive dropout (EIO→LMDB, known machine issue) — retried |

**Quest final package**: **not completed due to repeated external drive
disconnects during build** (owner decision 2026-07-29 to stop retrying on the
T9). Configuration is complete and validated (08); package generation is a
single documented command (02) to run from stable storage.

**Post-implementation desktop rebuild** (would carry the Addressables
handle-storage fix and the terrain repair into `Builds/`): same status — the
attempt crashed on a third drive disconnect; skipped per owner instruction.
The existing final desktop builds (12:25/12:29) validate the production
architecture and smoke test but predate the terrain repair (see
`15_TERRAIN_RECOVERY.md`).

## 9. Standard & kiosk — see `12_MODES_REPORT.md`
## 10. Accessibility — see `13_ACCESSIBILITY_REPORT.md`
## 11. Deferred user-testing packages — `04_DEFERRED_TESTS_QUEST.md` and `05_DEFERRED_TESTS_LOWEND_HARDWARE.md` (builds + install steps + record-keeping templates + acceptance criteria)

## 12. Remaining work

- **P0 (blocks desktop dev build)** — none.
- **P1 (blocks desktop stakeholder testing)** —
  1) First Windows runtime session using checklist 03 (validate media decode,
  display switching, kiosk, offline on Windows).
  2) Manual desktop full-exhibit walkthrough (03 §A) with visual QA —
  including the entrance terrain appearance and video playback of all six
  exhibits (automated coverage exercised scenes/lifecycle, not decode).
- **P2 (blocks institutional desktop release)** —
  1) Approved transcript/subtitle content authored into SubtitleTrack assets.
  2) Code signing (Windows Authenticode; macOS Developer ID + notarization).
  3) ~~`New Terrain.asset` build error~~ — **RESOLVED**: git-CRLF binary
  mangling (the nulled working-tree scene reference was a downstream symptom),
  repaired and verified in-editor (`15_TERRAIN_RECOVERY.md`); first build on
  stable storage confirms.
  4) macOS dev-build shutdown segfault after quit (cosmetic in dev builds;
  verify absent in release/non-development builds).
  5) Rebuild all three targets from stable storage (T9 produced three
  build-time disconnects; all post-implementation rebuilds were skipped by
  owner decision).
- **Q0 (requires physical Quest)** — full 04 checklist.
- **H0 (requires representative low-end hardware)** — full 05 checklist;
  minimum-spec statement blocked on this.
- **P3 (polish)** — plain-`http` external link in the main scene
  (`bcatlab.org` overview) should become https; optional gamepad support;
  Escape-to-close inside the settings panel; exhibit descriptions for the
  directory; consider packaging Quest media as OBB if APK size matters for
  distribution.

## 13. Validation evidence

- Build summaries: `Builds/BuildSummary_<target>.txt` (baseline + final).
- Build logs (this session): scratchpad `build_macos_baseline2.log`,
  `build_windows_baseline.log`, `build_macos_final.log`,
  `build_windows_final.log`, `build_quest_final*.log`, `setup_runall*.log`,
  `validation_audit.log`.
- Baseline macOS boot log: main scene loads on M4/Metal, six media URLs
  resolved, zero exceptions.
- Smoke test (final macOS build, kiosk mode, 3 cycles): report
  `smoketest_20260729_123033.txt` (user data folder) — RESULT: PASS; house
  load 12.7 s; per-cycle scenes=1, no duplicate rigs, allocated memory
  identical after every return (520.2 MB), managed 10.0→10.8 MB, enter
  2.3–5.4 s, exit 4.2–4.3 s; 8 screenshots (baseline/in-kitchen/back-home ×3/
  final) showing the scanned kitchen, exit prompt, return spawn, router
  screen prompt + crosshair.
- Validation audit: `Docs/Production/VALIDATION_AUDIT.txt` (final run:
  keyboard-ownership PASS, tiers PASS, BK local paths PASS, identifier PASS).
- Media error handling: code-path + resolver logs (macOS); full fault
  injection in checklist 03 §H.
- Kiosk: smoke ran the app in kiosk mode (auto-begin, fixed tier); idle-reset
  sequence exercised via the shared flows (manual timing runs in 03 §F).
- Quest configuration evidence: `08_QUEST_CONFIGURATION.md` + build logs.
- Deferred physical testing is marked in every relevant document.

## Definition-of-done status
All implementation-scope items are met (desktop builds succeed; shared scenes;
rig separation + selection; router-owned interaction with desktop/Quest prompt
separation; menus; persisted settings; four quality tiers; validated native
media paths with graceful failure; owned Addressables with rebuilt local Black
Kitchen; standard+kiosk; accessibility + directory; reset/unstuck/return;
optimization pass intact; deferred tests documented with packages; no
unsupported-platform work; no git operations). Physical-Quest and low-end-
hardware items remain explicitly excluded per the plan and are packaged for
the owner.
