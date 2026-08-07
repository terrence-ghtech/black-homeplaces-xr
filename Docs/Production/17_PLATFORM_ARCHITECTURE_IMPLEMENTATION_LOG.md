# Platform Architecture — Implementation Log

Implementation of `16_PLATFORM_ARCHITECTURE_REVIEW.md`.

**Branch:** `feat/platform-architecture` (baseline commit `64be889`)
**Date:** 2026-08-07
**Unity:** 6000.4.5f1 · **Editor closed**, all work driven through `-batchmode -executeMethod`
**End state:** architecture validator **0 errors, 0 warnings** across all four production
scenes; macOS, Windows x64 and Quest APK all build green; desktop smoke test PASS.

---

## 1. Implementation order chosen, and why

The review proposed seven phases. I kept its dependency structure but reordered
two things, for reasons the audit made concrete.

| # | Step | Commit | Why here |
|---|---|---|---|
| 0 | Baseline snapshot | `64be889` | The working tree contained uncommitted work that my audit had described. Committing it unchanged first is what makes everything after it separable and revertible. |
| 1 | `BCaTArchitectureValidator`, report-only | `eed0c64` | **First, deliberately.** Every later step needed a mechanical answer to "did I break the hierarchy?". Its baseline run (0 errors, 42 warnings) reproduced every defect named in review §3 and found three that were not. |
| 2 | `BCaTPlatform` resolver + profiles | `a0a1c66` | Behaviour-preserving; nothing else can be built on two competing decision primitives. |
| 3 | Editor Platform Test Mode + dev-only stripper + platform Play Mode harness | `72adbe4` | **Moved earlier than the review's Phase 3/4 split.** The review flagged Editor Quest simulation as the highest-value capability; it also turns out to be the *precondition* for validating any scene migration on the Quest side, so the harness had to exist before a scene was touched. |
| 4 | `ScenePlatformBinding` + rig registry + profile-driven composition | `41dc795` | The applier the migrations need. |
| 5 | **Black Kitchen** migration | `9724ccd` | First full scene, as instructed: it carried the worst defects. |
| 6 | Main scene migration | `ac7432c` | Same tooling, now proven on a smaller scene. |
| 7 | Presentation scenes (menu, loading) | `2362bb5` | Needed before retiring legacy so every scene has a binding. |
| 8 | Retire `PlatformRigActivator` + `ScenePlatformRigSelector` | `4d2643c` | Only after the smoke test showed the activator deferring in all four scenes, i.e. provably inert. |
| 9 | `XrSelectSurface` + fix 3 Quest-unreachable interactables | `745ef69` | After the hierarchy was stable, so a content change could be attributed cleanly. |
| 10 | Build-time enforcement + Addressables safeguards | `b37b079` | **Moved after the content fixes**, because promoting rules to Error is only safe once the project satisfies them. |
| 11 | Fix a defect the smoke test found in step 10 | `35d2149` | See §4. |
| 12 | Project-owned rig prefabs | `f603c82` | **Moved last**, from the review's Phase 4. It is the lowest-value item and touches the most behaviour-critical objects, so it should not be able to block anything else. |

Working method for each step, per the instruction: establish or use a check that
can prove the change → make the smallest viable change → compile/test → only
then remove the obsolete mechanism.

---

## 2. What changed

### New runtime code (`Assets/BCaT/ProductionCore/`)

| File | Role |
|---|---|
| `Platform/BCaTPlatform.cs` | The single platform authority. Precedence: editor override → `-bcatPlatform=` → build define → XR device probe → desktop. Owns the only sanctioned access to build defines, `XRSettings` and XR Management. |
| `Platform/BCaTPlatformProfile.cs` | Platform policy as data: rig kind, locomotion, input provider, prompt style, UI input module, app shell, kiosk, swapchain ownership, quality tier, media source policy, diagnostics, Addressables profile. Includes a code-built fallback. |
| `Platform/ScenePlatformBinding.cs` | The per-scene applier. Activates one authored-inactive branch in `Awake`; also `ScenePlayerRigRegistry`. |
| `Platform/EditorOnlyObject.cs` | Marks development aids for build-time stripping. |
| `Interaction/XrSelectSurface.cs` | Builds an interactable's Quest aim surface at runtime, replacing hand-authored `*_QuestXRSelect` twins. |

### New profile and rig assets

```
Assets/BCaT/ProductionCore/Platform/Resources/BCaT/Platform/BCaTPlatformProfile_Desktop.asset
Assets/BCaT/ProductionCore/Platform/Resources/BCaT/Platform/BCaTPlatformProfile_Quest.asset
Assets/BCaT/ProductionCore/Platform/Prefabs/BCaT_DesktopRig.prefab
Assets/BCaT/ProductionCore/Platform/Prefabs/BCaT_QuestRig.prefab
```

### New editor tooling (`Assets/Editor/BCaTProduction/`)

`BCaTArchitectureValidator.cs` (25-rule catalogue) ·
`BCaTBuildValidationStep.cs` (pre-build gate) ·
`BCaTPlatformTestMode.cs` (Auto / Desktop / Quest XR Simulated / Quest XR Device) ·
`BCaTPlatformPlayModeValidation.cs` (dual-platform Play Mode contract test) ·
`BCaTEditorOnlyStripper.cs` · `BCaTSceneMigration.cs` ·
`BCaTXrSelectSurfaceRepair.cs` · `BCaTPlatformProfileSetup.cs` · `BCaTRigPrefabs.cs`

### Modified runtime code

| File | Change |
|---|---|
| `Assets/Scripts/PlatformInteractionPrompt.cs` | `IsXRActive()` / `IsQuestRuntime` forward to `BCaTPlatform`. This is what makes the Editor test mode reach the nine exhibit call sites that ask here instead of through `PlatformCapabilities`. |
| `Platform/PlatformCapabilities.cs` | Every member forwards to the resolver or a profile field. |
| `Platform/BCaTAppBootstrap.cs` | Service composition from `ShowsAppShell` / `AllowsKioskMode` instead of the build define; dropped `PlatformRigActivator`; added the Addressables/platform startup assertion. |
| `Shell/UiFactory.cs` | `EnsureEventSystem` no longer activates an inactive EventSystem it finds, and adds the profile's module kind. |
| `SceneTransitions/SceneArrivalController.cs` | Asks `ScenePlayerRigRegistry` first; existing fallbacks retained. |
| `Exhibits/BlackKitchen/BlackKitchenExperienceController.cs` | `exitModalUsesXR` asks `BCaTPlatform.IsQuest`. |

### Deleted

`Platform/PlatformRigActivator.cs` · `SceneTransitions/ScenePlatformRigSelector.cs`

### Scenes

| Scene | Change |
|---|---|
| `BlackKitchen_MemoryScene` | root `Platform/{Desktop,Quest}` both authored inactive; rigs and XR Interaction Manager moved in; `Platform/Quest/DevOnly` + XR Device Simulator; two EventSystems → one at `SceneServices/UI/EventSystem` with no authored module; `ScenePlatformRigSelector` removed; `SceneServices` hosts the binding, arrival controller and spawn point. |
| `BH_XR_MainScene` | `BuildProfiles`→`Platform`, `Web`→`Desktop`, `XR`→`Quest` (renamed in place); both branches authored inactive; simulator relocated out of the desktop branch; root EventSystem → `SceneServices/UI/EventSystem`; `SceneServices` added; return spawn point moved with a world-pose drift guard; three `XrSelectSurface` components added. |
| `LoadingScene`, `MainMenuScene` | `Platform/{Desktop,Quest}` with the existing flat camera and a new head-tracked presentation camera; `SceneServices` with binding + EventSystem. |

### Settings

No `ProjectSettings/` change was required. Quality tiers, XR settings, Addressables
group paths and player metadata are unchanged. Notably, **no Standalone XR
settings entry was added** — Quest XR (Simulated) does not need one, which is why
it is the recommended workflow (see §6).

---

## 3. Validations performed

All runs were `-batchmode` against the real project.

| Validation | Result |
|---|---|
| `ProductionValidationAudit` (pre-existing) | PASS at baseline and unchanged |
| `BCaTArchitectureValidator` baseline | 0 errors, **42 warnings** |
| … after Black Kitchen | 19 warnings, no BK findings |
| … after main scene | 11 warnings |
| … after presentation scenes | 5 warnings |
| … after `XrSelectSurface` | **0 errors, 0 warnings** |
| … with all rules promoted to Error | exit 0 |
| `BCaTPlatformPlayModeValidation` — Black Kitchen | 28/28 PASS, Desktop **and** Quest-simulated |
| `BCaTPlatformPlayModeValidation` — main scene | 28/28 PASS, both platforms |
| `BlackKitchenAudioPlayModeValidation` | PASS — station tour, exclusivity, exit reflection, silence phases, zero autoplay, zero exclusivity violations |
| `ExhibitInteractionPlayModeValidation` | 9 failures — **identical set before and after**, verified by reverting to baseline and re-running (see §4) |
| macOS build | Succeeded, 0 errors; gate PASS; `DevOnly` stripped |
| Windows x64 build | Succeeded, 0 errors |
| Quest APK build | Succeeded; Android Addressables catalog + bundle validated; APK contains `aa/catalog.bin`, `aa/Android/`, key `BlackKitchen_MemoryScene` |
| Desktop smoke test, 3 cycles | **RESULT: PASS** — enter 2.3–2.4 s, exit 3.8 s, handles 1 in-kitchen / 0 at home, `no active handles` at the end, 43–61 fps |

### Content-preservation checks after the main scene migration

- GameObject count 707 → 711: exactly the five organizers added minus the one
  EventSystem removed.
- Terrain, lighting, volumes, boundaries, mural, notebook, Adinkra, LindaLeaks
  and the Black Kitchen portal all present with identical counts.
- Zero diff lines touching `TerrainData`, `LightingSettings` or `m_Lightmap`.
- Scene diff 221 insertions / 103 deletions in an 8.2 MB file.

---

## 4. Regressions encountered, and fixes

1. **Stale log read as a pass.** The `timeout` command does not exist in this
   shell, so a Play Mode run never launched and I read a leftover
   `ExhibitInteractionValidation.log` as a result. Fixed by deleting result logs
   before every run and checking exit codes.
2. **Nine exhibit-validation failures, provenance unknown.** Rather than assume
   they were pre-existing, I stashed the resolver change, moved the new files
   aside, re-ran at baseline, and got the **same nine**: stale `TEST_*` staging
   object names and XR prompt wording drift in the test's expectations. Not
   caused by this work; recorded in §7.
3. **Validator false positives, found and fixed while it was still report-only:**
   `CharacterController` treated as a desktop marker (the XRI rig carries one);
   `BCAT-D003` flagging exclusive-zone stations that dispatch through a relay;
   `BCAT-D006` counting one listener per rig as a duplicate; `BCAT-L004`
   requiring a marker on every descendant when the stripper destroys the marked
   root; `BCAT-L003` flagging rig-internal renderers in a presentation rig;
   `BCAT-Q002` flagging the Black Kitchen entrance's deliberately empty
   shared-HUD prompt.
4. **Presentation rigs carried controller visuals.** First attempt deactivated
   the controller subtrees, which left 40 renderers in a loading screen and
   spiked the validator to 45 warnings. Reverted both scenes with
   `git checkout`, changed the approach to unpack and **delete** those subtrees,
   re-ran: 5 warnings and much smaller scenes.
5. **My own Addressables startup check was wrong** — and the smoke test caught
   it emitting a false "content mismatch" error on a correctly built macOS
   player. Two mistakes: the platform folder is a *subfolder* of
   `Addressables.RuntimePath`, not part of it, and Addressables names it after
   the BuildTarget (`StandaloneOSX`), not the short platform name. Fixed in
   `35d2149` and re-verified by rebuilding and re-running the smoke test.
6. **`BCAT-L005` blocked on my own new code**, which reached for
   `RuntimePlatform` directly. Fixed by moving that knowledge into
   `BCaTPlatform.ExpectedAddressablesPlatformFolder` rather than whitelisting
   the file — the rule was right.
7. **My YAML dump tool misreported a prefab-instance override.** It flagged the
   main scene's XR rig as inactive after migration by matching any
   `m_IsActive: 0` in the instance body; only the *root-targeted* override
   matters, and that one was correctly removed. The Play Mode harness, which
   reads the real object model, was the authority.

---

## 5. Legacy systems removed

| Removed | Replaced by | Evidence it was safe |
|---|---|---|
| `ScenePlatformRigSelector` (component + class) | `ScenePlatformBinding` | No scene or prefab outside `_Recovery` referenced it; its five wired references are subsumed by the binding plus the profile |
| `PlatformRigActivator` | `ScenePlatformBinding` | Logged "deferring to it" for all four scenes across two full smoke-test cycles — it never changed anything |
| `FindBuildProfilesBranch` | root `Platform` group + binding | Matched the literal string `"BuildProfiles"`, so renaming an organizer silently changed build behaviour |
| `RemoveXRDeviceSimulatorIfPresent` | `EditorOnlyObject` + `BCaTEditorOnlyStripper` | Build log shows `Stripping development-only object 'Platform/Quest/DevOnly'`; the simulator is absent from the player rather than shipped-and-hidden |
| `EnsureQuestCamera` (Quest-only diagnostic repair) | binding's camera check on both platforms | Harness asserts `Camera.main` is owned by the active rig on both platforms |
| `DisableDesktopMovement` (type-name-string sweep) | authored-inactive branches | It existed only to clean up after a wrong-platform rig's `Awake`, which cannot now happen |
| Second EventSystem per scene; authored input modules | one EventSystem, module assigned by the binding | Harness asserts exactly one active EventSystem with exactly one enabled module of the profile's kind |

---

## 6. Editor workflow now available

**`BCaT → Platform Test Mode`** — Auto · Desktop · **Quest XR (Simulated)** · Quest XR (Device).

Quest XR (Simulated) is the headline capability. Previously the Quest hierarchy
could not be exercised in the Editor at all: `XRGeneralSettingsPerBuildTarget`
has an Android entry only, so `XRSettings.isDeviceActive` is always false in the
Editor, so the platform always resolved to Desktop and the XR rig was always
deactivated — and the XR Device Simulator, which does not set `isDeviceActive`
either, sat *inside the desktop branch* and was disabled together with it
whenever XR was active. The editor override is therefore the highest-precedence
source in the resolver: it must outrank the probe the simulator cannot satisfy.

In that mode the XR rig activates, the Quest profile applies, prompts read
`Interact` / `Play — Name`, the prompt canvas switches to world space, no
crosshair or pause menu is composed, and `Platform/Quest/DevOnly/XR Device
Simulator` comes up with the rig.

Quest XR (Device) additionally needs a Standalone entry in
`XRGeneralSettingsPerBuildTarget` with `InitManagerOnStart = false`. That entry
was deliberately **not** added: with automatic initialization it would flip
`isDeviceActive` in ordinary desktop Play Mode on any machine with an OpenXR
runtime installed, silently changing every desktop session. Simulated mode needs
none of it.

CI equivalent: `-bcatPlatform=Desktop|Quest`.

---

## 7. Remaining work and device-only checks

### Requires a physical Quest — not validated here

Everything below was validated **in simulation only**. Simulated Quest exercises
platform resolution, rig activation, service composition, prompt wording, canvas
mode and UI input module. It does **not** exercise real OpenXR, stereo rendering,
head/hand tracking, controller ray ergonomics, comfort or on-device performance.

1. **Headset pass through both scenes** — hover, prompt and select on every
   exhibit; confirm the single EventSystem change did not alter UI behaviour.
2. **The three newly fixed interactables** (`pillow__quilt`,
   `DominoSpatialAudio`, `TV_Preview`) — confirm they are now hoverable and
   selectable in headset. They were unreachable before this work; the fix is
   structurally correct but unproven on device.
3. **The head-tracked loading/menu presentation** — confirm head tracking during
   a Black Kitchen bundle load and that the view is no longer head-locked.
4. **`Platform/Quest` authored inactive on device** — confirm the rig activates
   correctly from a cold APK start.
5. Quest performance and the deferred tests in `04_DEFERRED_TESTS_QUEST.md`.

### Deliberately deferred, with reasons

- **The 13 existing `*_QuestXRSelect` twins and their duplicate relays remain.**
  They work on Quest today. Replacing a working headset interaction cannot be
  verified without a headset, and the instruction was to preserve current Quest
  behaviour unless the architecture required a change. `XrSelectSurface` is in
  place, so each is a mechanical, device-validated follow-up — the Black Kitchen
  is the best first candidate (12 relays for 6 logical targets).
- **`LegacyInteractionPromptSuppressor` and the 16 `PlatformInteractionPrompt`
  instances remain.** Retiring them means editing 16 curatorial canvases in the
  main scene; that is content work whose failure mode is blanked exhibit panels
  (this has happened before — see the suppressor's own comments). Not structurally
  obsolete until those instances go.
- **Exhibit content regrouping** into `Environment` / `Navigation` /
  `Interactables` / `Media` was not done. It is presentational grouping with real
  risk (large prefab instances, collision proxies) and no platform benefit.
- **Existing rig instances were not re-pointed** to the new project-owned rig
  prefabs; see §1 step 12.
- **Build Profile assets were not committed** to `Assets/Settings/Build Profiles`.
  The pipeline asserts `activeBuildTarget` and now also sets and asserts the
  Addressables profile, which closes the substantive risk; creating profile
  assets programmatically risks a half-configured profile and is better done in
  the Build Profiles window.
- **`ExhibitInteractionPlayModeValidation` has 9 pre-existing failures** (stale
  `TEST_*` staging object names, XR prompt wording expectations that predate the
  `Action — Name` format). Confirmed present at baseline. Worth fixing, but it is
  test drift, not a platform defect.
- **`Assets/XR/Settings 1`–`5`** (five empty leftover folders) and the remaining
  dead `UNITY_WEBGL` media branches were left alone as out-of-scope cleanup.

### Environment note

The macOS player returns exit code **139** after writing its report and
completing an orderly shutdown (`RESULT: PASS`, then Physics cleanup, Input
System shutdown, `PlayerConnection::Cleanup`). A pre-existing build from
2026-07-30 — predating all of this work — does the same, so the teardown
segfault is not caused by these changes. Consistent with the known
T9-external-drive issue recorded for this project. Worth resolving before
institutional deployment; not investigated here.

---

## 8. How to re-run everything

```bash
U=/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity

# Architecture validation (0 = clean, 1 = error, 2 = warnings only)
$U -batchmode -nographics -quit -projectPath . -logFile v.log \
   -executeMethod BCaT.EditorTools.BCaTArchitectureValidator.RunBatch

# Platform contract, both platforms, per scene
$U -batchmode -projectPath . -logFile p.log \
   -executeMethod BCaT.EditorTools.BCaTPlatformPlayModeValidation.RunBlackKitchen
$U -batchmode -projectPath . -logFile p.log \
   -executeMethod BCaT.EditorTools.BCaTPlatformPlayModeValidation.RunMainScene

# Exhibit validations
$U -batchmode -projectPath . -logFile a.log -executeMethod BlackKitchenAudioPlayModeValidation.Run
$U -batchmode -projectPath . -logFile e.log -executeMethod ExhibitInteractionPlayModeValidation.Run

# Builds (each runs the architecture gate first and aborts on any error)
$U -batchmode -nographics -quit -projectPath . -buildTarget OSXUniversal \
   -executeMethod BCaT.EditorTools.ProductionBuildPipeline.BuildMacOS -logFile bm.log
$U -batchmode -nographics -quit -projectPath . -buildTarget Win64 \
   -executeMethod BCaT.EditorTools.ProductionBuildPipeline.BuildWindows -logFile bw.log
$U -batchmode -nographics -quit -projectPath . -buildTarget Android \
   -executeMethod BCaT.EditorTools.ProductionBuildPipeline.BuildQuest -logFile bq.log

# Desktop smoke test on the built player
"Builds/macOS/BlackHomeplaces.app/Contents/MacOS/Black Homeplaces: The XR House" \
   -kiosk -bcatSmokeTest 3 -logFile smoke.log
```

Reports: `Docs/Production/ARCHITECTURE_VALIDATION.md` (+ `.json`),
`Library/BCaTPlatformValidation.log`, `Library/BlackKitchenAudioValidation.log`,
`Library/ExhibitInteractionValidation.log`, `Builds/BuildSummary_<target>.txt`,
and the smoke report under `[persistentDataPath]/BCaT/`.
