# Quest Stabilization Pass — Physical Device Log

Session started 2026-08-08. Device: Quest 3 over Wi-Fi ADB (`10.0.0.54:5555`),
Unity-bundled platform-tools 36.0.0 exclusively. Baseline: installed APK
sha256 `15f20142…` == local build of 2026-08-08 09:18 (platform-architecture
build, branch `feat/platform-architecture`).

Session infrastructure: host-side watchdog re-runs `adb connect` whenever the
serial drops (the Wi-Fi link flaps roughly every 2 minutes when the headset is
idle); headset screen timeout maxed, `svc power stayon true`,
`com.oculus.vrpowermanager.automation_disable` broadcast sent, Wi-Fi sleep
policy set to never. All device commands go through a retry wrapper that
reconnects between attempts.

---

## 1. Locomotion — "unexpected teleporting / camera jumps / nausea"

**Symptom (reported):** involuntary teleports, rapid viewpoint changes,
disorientation on the physical headset.

**Audit performed (static, exhaustive):**
- The scene rigs (inline XRI `XR Origin (XR Rig)` variants in both inhabited
  scenes, matching `BCaT_QuestRig.prefab`) have exactly one movement path:
  continuous move on the left stick (`DynamicMoveProvider`, left-hand input
  only, 2.5 m/s, gravity on) and smooth turn on the right stick
  (`ContinuousTurnProvider`, right-hand input only). The `Teleportation` and
  `Climb` GameObjects are authored inactive, both `ControllerInputActionManager`s
  have their teleport interactor/mode references nulled, snap turn is disabled,
  and `NearFarEnableTeleportDuringNearInteraction` is 0 on both hands.
- No `TeleportationArea`/`TeleportationAnchor` exists in any non-sample scene
  or prefab (verified by script GUID over all `.unity`/`.prefab` files).
- Runtime warps are confined to the scene-arrival spawn (fade-to-black covers
  it, physics disabled during the move) and the Black Kitchen fall-recovery
  (fires only below world Y −2.5).

**Conclusion:** the rig cannot teleport by input; any discrete jump a visitor
feels must come from physics (fall through a collision gap, character-controller
push-out) or tracking (recenter/resume), not locomotion config.

**Change:** added `XrLocomotionJumpLogger` (Quest + development builds only,
composed by `BCaTAppBootstrap`): logs one structured warning whenever the
camera or rig moves >0.35 m in a single frame, classifying it (vertical fall /
horizontal warp / head-only tracking jump) with character-controller state and
time-since-load. The next worn session will name the exact cause of each jump.
Files: `Assets/BCaT/ProductionCore/Diagnostics/XrLocomotionJumpLogger.cs`,
`Assets/BCaT/ProductionCore/Platform/BCaTAppBootstrap.cs`.

**Note for the owner:** both scene rigs are scaled ×1.44, which amplifies
physical head translation by 44% and shrinks apparent world scale. If nausea
persists after the jump log comes back clean, this scale is the next suspect —
it is a deliberate house-scale choice, so changing it is a design decision,
not part of this pass.

**Validation:** static config verified in scene YAML; jump logger ships in the
next build; physical-headset confirmation pending a worn session.

## 2. Controllers frequently do not appear

**Root cause (found in rig config + XRI source):** the rig's
`XRInputModalityManager` has `m_LeftHand`/`m_RightHand` = null (no hand
tracking wired) and deactivates a controller's GameObject the moment its
device stops being tracked (controllers set down, headset dozed, system menu).
Reactivation depends solely on a tracking-acquired event; when that event
lands while the app is paused — exactly the idle-between-builds pattern of a
development headset — it is missed and the controller stays invisible although
it is tracked and functional.

**Change:** added `XrControllerVisibilityGuard` (Quest only, composed by
`BCaTAppBootstrap`): polls 1 Hz; if a hand's device reports `isTracked` while
the manager's controller GameObject is inactive, it re-runs the manager's own
mode resolution (reflection into `UpdateLeftMode`/`UpdateRightMode`, so
internal state stays consistent) and force-activates as a fallback, logging
every intervention.
File: `Assets/BCaT/ProductionCore/Platform/XrControllerVisibilityGuard.cs`.

**Validation:** compile + batch verify OK; on-device confirmation pending
(watch for `[XrControllerVisibilityGuard] Restored …` lines).

## 3. Lovely Gravy / Rianna Walcott video not interactable on Quest

**Root cause:** classic trigger-collider class.
`Kitchen_VideoInteraction`'s own interactable collider is trigger-only
(3.2×2.4×1.4), which Quest ray casters ignore. Its Quest aim twin
(`Kitchen_VideoInteraction_QuestXRSelect`) existed but its box was
0.59×0.57×0.60 offset (−1.02, 0.22, −0.06) from the exhibit center — about 4%
of the intended aim surface, and not where the video screen is.

**Fix:** twin box resized/recentered to mirror the parent's authored surface
exactly: size (3.2, 2.4, 1.4), center (1.0196, −0.2216, 0.0552) — compensating
the twin's local offset. Wiring untouched (selectEntered →
`MediaVideoController.OnXRSelect` was already correct). Object not moved.

**Validation:** `QuestInteractionVerify.Run` RESULT OK after edit; headset
retest pending.

## 4. Backyard "My Grandma's Garden" picture not interactable on Quest

**Audit:** statics are clean — `flowerbed` has a solid (non-trigger) 5×1.5×1.5
box on layer 0 wired into its `XRSimpleInteractable.m_Colliders`, selectEntered
→ `SimpleImagePopupInteractor.OpenFromXR` (correct EventDefined call), popup
positions itself in front of `Camera.main` (XR-safe), router accepts unless
blocked. This is NOT the trigger-collider bug class.

**Status:** needs on-device reproduction with the new build's logging
(`[InteractionRouter] XR hover entered/select accepted/rejected` lines will
say whether hover fails, select is rejected and why, or the popup opens
invisibly). Watch that exhibit specifically in the next worn session.
No blind change made — the layout and wiring are correct as authored.

## 5. Meshell Sturgis monitor steals focus / notebooks need precise aim

**Root cause (measured in scene YAML, world space):** the monitor's
interaction anchor sits 2.9 m in front of the monitor prop, directly over the
desk. Its desktop box (1.5 m cube) fully contained the notebooks' Quest aim
box and 81% of their desktop volume; its Quest aim box (1.2 m cube) contained
~91% of the notebooks' aim box (which was itself only 0.43×0.35×0.42 m). Both
targets had Priority 0, and the router resolved XR hover ties by dictionary
enumeration order — literally unstable under hand jitter — while desktop
scoring let the monitor's nearer/higher focus point win the angle term.

**Fixes (no object moved):**
- Monitor desktop box: 3×3×3 → 2.4×1.8×1.2 authored (world 1.2×0.9×0.6),
  center raised +0.3 — hugs the screen instead of the desk.
- Monitor Quest aim box: 2.4³ → 2.2×1.6×1.0 authored (world 1.1×0.8×0.5),
  center +0.3.
- Notebooks Quest aim box enlarged 0.43×0.35×0.42 → 0.95×0.45×0.85 world,
  covering all three notepads with generous margin.
- `LindaLeaksPanelOpener.Priority` 0 → 1: priority dominates both selectors,
  so the notebooks now win exactly when both targets are candidates.
- `InteractionRouter.SelectBestXRHoverTarget` tie-break changed from `>=`
  (last-enumerated wins → flipping) to strictly-greater with
  keep-the-current-target on ties — hover ownership is now stable under
  jitter for ALL equal-priority neighbors, not just this exhibit.

**Validation:** batch verify OK; headset retest pending (aim at monitor →
monitor; aim at notebooks → notebooks; jitter must not flip the prompt).

## 6. Content QA

- **Upstairs mural (confirmed bug, fixed):** XR prompt/objectName on
  `MuralExhibit` said "The Breonna Taylor Memorial Mural"; the exhibit's own
  plaque (authoritative, by Rianna Walcott) says **Black Homeplaces Community
  Mural**. Fixed in scene (`xrPrompt`, `objectName`) AND in
  `Assets/Editor/BCaTQuest/QuestCuratorialPrompts.cs` (the registry entry that
  would have reintroduced it on the next prompt-repair run). The Breonna
  Taylor name belongs to the *downstairs BTMMP workstation* (Alisa Hardy),
  which keeps it.
- **BTMMP plaque body (fixed):** expanded the acronym as "the **Black Trauma**
  Memorial Mural Project" — wrong; BTMMP is the **Breonna Taylor** Memorial
  Mural Project (per the exhibit's own link launcher, asset names, and the
  implementation audit CSV). Scene text corrected.
- **Mural gallery item titles (fixed):** all 11 gallery `displayName`s were
  raw filename slugs shown on screen ("01 initial wall sketch", …, "11 mural
  process video") → retitled ("Initial Wall Sketch", …, "Mural Process
  Video"). Captions remain empty (content owner's call).
- **Flagged for the owner, not changed** (plausibly intentional or needs
  collaborator confirmation): lowercase artwork titles ("Such lovely gravy"
  is lowercase on its plaque too — looks like artist styling; "subjected to
  recognition" / "And that is the truth" popup titles differ in casing from
  their plaques); Adinkra plaques credit "Felicity Sena Dogbatse & Lisa Abena
  Osei" while the audit CSV says "Felicity & Elizabeth"; Black Kitchen scene
  credits Clarisa James (DIVAS for Social Justice) while the CSV lists
  Yuhanxiao Maggie Ma; "BTMMP: Telling the Story of Murals" plaque title
  leads with an unexpanded acronym; two "In My Sister's Room" plaques share
  one title (sound vs video installation).

## 6b. Scene-transition fades invisible on Quest (found by Black Kitchen audit)

**Symptom:** every scene transition on Quest is a hard cut; the authored
fade-to-black (exit) and fade-from-black (arrival) never show.

**Root cause:** both runtime fade canvases (`SceneArrivalFade`,
`BlackKitchenExitFade`) were created as `ScreenSpaceOverlay`, which the XR
compositor never renders. The code already used the correct world-space
pattern for the exit-reflection modal and the shared prompt UI — the two fade
overlays were the outliers.

**Fix:** new `BCaT.Production.Shell.FadeOverlayBuilder`: on Quest it parents a
WorldSpace canvas (3 m black quad, 0.32 m ahead) to the main camera; desktop
keeps ScreenSpaceOverlay. Both call sites now use it; fade alpha logic
unchanged. Files: `Assets/BCaT/ProductionCore/Shell/FadeOverlayBuilder.cs`,
`SceneArrivalController.cs`, `BlackKitchenExperienceController.cs`.

## 6c. Black Kitchen static audit results

- Spawn (0, 0.02, −4.43) sits 2 cm above a gap-free y=0 floor; boundary walls
  sealed; fall-recovery threshold has 2.5 m of margin. OK.
- Exit interface and all 5 audio stations have correctly wired non-trigger
  `_QuestXRSelect` twins (twins are children of their stations, so
  `OwnsCollider` matches them — the suspected ray-resolution tie bug is NOT
  real). OK.
- Exactly one EventSystem (no authored input module); both platform branches
  authored inactive; single XRInteractionManager under the Quest branch. OK.
- Deleted `Assets/Editor/BlackKitchenFixValidation.cs`: it validated
  `OvenInteraction`/`RiceBeansPotInteraction`, which the 2026-07-28 station
  rebuild intentionally removed — the validator could only fail and is a
  stale-harness build hazard.
- Noted: rig scale 1.44 leaves ~0.1–0.2 m of headroom between the scaled eye
  height and the stations' 2 m select volumes — aim must be level or slightly
  down; verify feel in headset.

## 6d. Systematic Quest-reachability sweep (all interactables, main scene)

Audited all 30 interaction-script instances (scene + every referenced prefab,
154 prefab GUIDs resolved). **Zero instances of the trigger-only-unreachable
bug class remain** — every trigger-collider interactable has a working solid
aim path (own solid collider, XrSelectSurface, or QuestXRSelect twin).

Two hardening items from the sweep:
- **RhythmAndRope_JumpRope**: its XRSimpleInteractable had `m_Colliders: []`
  and worked only via XRI's Awake auto-fill of child colliders — one
  trigger-flip away from silently breaking. Now explicitly wired to the
  prefab's root non-trigger BoxCollider.
- **Black Parlors "sliver" aim boxes**: false alarm — the 0.06–0.1 authored
  sizes sit under a 33×22×22 parent scale (world ≈ 2 m boxes). No change.

## 7. Scene transitions

**PASS on the physical device** (build of 2026-08-08 10:35, sha
`513d9d2c…`, installed and byte-verified): `SmokeTestRunner` launched via
`adb shell am start … -e unity "-bcatSmokeTest 2"` ran two full House↔Black
Kitchen cycles on the Quest 3:

```
cycle 1 in-kitchen (enter 3.5s)  scenes=1 handles=1 fps~72
cycle 1 back-home  (exit 6.6s)   scenes=1 handles=0 fps~73
cycle 2 in-kitchen (enter 2.6s)  scenes=1 handles=1 fps~71
cycle 2 back-home  (exit 6.6s)   scenes=1 handles=0 fps~72
RESULT: PASS
```

No duplicate scenes, no duplicate CharacterControllers (the runner fails on
either), Addressables handles return to the single resident hold after each
exit, frame rate steady at 71–73 fps through every load, managed heap stable
(11→20 MB across cycles). Startup baseline also clean: platform=Quest
resolved, single EventSystem, single camera, orderly MainMenu→Loading→House
flow; the 'MainEntrance' spawn fallback is by design (`ResetService.cs`).
A `debug.bcat.smoketest` system-property trigger was added to
`SmokeTestRunner` as an alternative launch mechanism for future sessions.

## 7b. Final build validation (on-device)

Final build (sha `7f076b59…`, includes the fade fix, jump-rope wiring, and the
`debug.bcat.smoketest` trigger) installed and byte-verified; smoke test rerun
on the headset: **RESULT: PASS** (cycle 2: enter clean, exit 6.8 s, 72 fps,
scenes=1, handles→0, return spawn resolved to 'ReturnPoint' at
(173.10, 6.80, 157.20), fade-from-black lifecycle ran via the new world-space
overlay, zero exceptions). Cycle 1 showed exit "192.9 s" at fps~29 — that is
the idle headset suspending mid-cycle (wall-clock timer + smoothed-delta noise
on resume), not a regression; the identical cycle on the same build took 6.8 s.

**First jump-logger field data** (exactly the instrument working as intended):
- Two `head moved without the rig` events (0.37 m / 0.97 m, deltaTime up to
  333 ms) at the moment the app resumed from suspend → **tracking
  re-acquisition after doze/resume is a real, measurable source of the
  reported "camera jumps."** This is Horizon OS re-localization, not app
  locomotion; if a worn session shows these outside of resume, suspect
  guardian/lighting tracking loss.
- One 27.1 m `rig moved` event at timeSinceLoad=0.1 s = the arrival spawn,
  correctly under the (now visible-on-Quest) fade, with the character
  controller disabled during the warp. Expected and harmless.

## 8. Remaining / pending on-device

- Install new build; verify sha256; rerun `-bcatSmokeTest 2` on-device.
- Worn-session checklist: locomotion feel + jump-logger capture; controller
  visibility across sleep/resume; Grandma's Garden repro with router logs;
  Meshell monitor/notebook focus behavior; Black Kitchen full pass; Lovely
  Gravy select; mural prompt wording in-headset.

---

# Black Kitchen XR Runtime Pose Investigation (2026-08-09)

**Symptoms (worn Quest 3, reported):** player ~half-height through the floor in
Black Kitchen only; walking works but body/head stays too low; head-look feels
like skipping/teleporting; pressing A (jump) makes lag/skipping/teleporting
substantially worse. Main House Quest is correct; desktop Black Kitchen is
correct.

## Phase 1 — Structural rig diff (Main House vs Black Kitchen)

Both scenes instantiate the same XRI Starter Assets rig
(`XR Origin (XR Rig)`, guid `f6336ac4…`), but the **Black Kitchen instance was
missing ~50 overrides that the Main House instance (and the canonical
`BCaT_QuestRig.prefab`) carries.** Effective differences in BK before repair —
all of them source-prefab defaults that Main House explicitly turns off:

| Area | Main House (known-good) | Black Kitchen (before repair) |
|---|---|---|
| Teleportation GO + both teleport interactors + 2 stabilized origins | inactive | **ACTIVE** |
| Climb GO | inactive | **ACTIVE** |
| Right ControllerInputActionManager | smooth turn, teleport refs nulled | **teleport/snap-turn mode, live teleport refs** |
| SnapTurnProvider / ContinuousTurnProvider | snap off, continuous on | **snap ON, continuous OFF** |
| DynamicMoveProvider right-hand input | cleared | **wired** (right stick feeds move AND snap-turn paths) |
| NearFarEnableTeleportDuringNearInteraction | 0 both hands | **1 both hands** |
| Camera Offset authored y | 1.1 | prefab default (1.36144, Device-mode only) |
| Rig renderer m_CastShadows | off (20 renderers) | **on** |
| Camera UniversalAdditionalCameraData | authored (post-processing off) | absent (runtime auto-add) |

Identical in both scenes (ruled out as the divergence): rig scale 1.44,
CharacterController overrides (h=1, r=0.2, center.y=0.5), XROrigin tracking
config (`RequestedTrackingOriginMode=NotSpecified`, `CameraYOffset=1.36144`),
GravityProvider and JumpProvider active, spawn/rig authored poses.

## Phase 5 — A button

`m_JumpInput` on the rig's **JumpProvider** is an InputActionReference to
`XRI Right Locomotion/Jump`, bound to `<XRController>{RightHand}/{PrimaryButton}`
— the A button. Jump height 1.25 rig-units ≈ **1.8 m world** at the 1.44 rig
scale; GravityProvider then pulls back at an effectively scaled gravity with
`m_UpdateCharacterControllerCenterEachFrame=1` and an **unscaled** 0.09 m
grounded sphere-cast. This shipped with the XRI 3.3 starter rig; nothing in
the project consumes A otherwise. It is active in BOTH scenes.

## Phase 4 — 1.44 scale

Quest-only world-scale compensation for the oversized scanned environment
(desktop rigs use different per-axis tweaks: 0.4 x/z in the Main House desktop
instance). Identical in both Quest scenes and the canonical prefab → **not the
divergence**; Main House proves it livable. Kept. (Known side effects: head
sway ×1.44, effective gravity/jump scaled ×1.44.)

## Phase 6 — Runtime repositioning audit

No Black Kitchen script writes rig/camera/offset/CC transforms except
`SceneArrivalController` spawn placement (fade-covered, logged, CC disabled
during the warp) and the fall-recovery warp (threshold y<−2.5, logged).
No recenter calls anywhere in project code.

## Phase 2/3 — Runtime instrumentation

`Assets/BCaT/ProductionCore/Diagnostics/XrPoseProbe.cs` (dev builds, Quest
only, added by BCaTAppBootstrap): 1 Hz full pose chain (origin/offset/camera
world+local, CC pos/center/height/feet-Y/grounded/velocity), tracking-origin
mode (subsystem + XROrigin requested/current), HMD/controller isTracked, HMD
local pose, per-window max frame-to-frame head and origin steps, every
locomotion provider's state, `trackingOriginUpdated` (recenter) events,
A-button edges with surrounding physics state, and a dump of which enabled
input actions resolve to the physical A button. Grep tags: `[XrPose]`,
`[XrPose:A]`, `[XrPose:Recenter]`, `[XrPose:Actions]`.

### Runtime comparison (physical Quest 3, desk-mounted, smoke-driven)

Pre-repair instrumented APK (sha `a0e78a18…`) vs repaired APK (sha
`214e8c29…`), identical capture procedure, logs archived under
`QuestTestLogs/PoseProbe/{baseline_prerepair,repaired}/`:

| Provider state (1 Hz samples) | Main House | BK pre-repair | BK repaired |
|---|---|---|---|
| SnapTurnProvider | off | **ON** | off |
| TeleportationProvider | off | **ON** | off |
| ClimbProvider | off | **ON** | off |
| ContinuousTurnProvider | on | on (both turn providers live) | on |
| DynamicMoveProvider | on (left-only input) | on (BOTH sticks wired) | on (left-only) |
| JumpProvider | on | on | off (documented deviation) |

**The runtime data confirms the structural diff exactly**: pre-repair BK ran
snap turn + teleportation + climb simultaneously with the smooth-turn/move
providers — one right-stick input fed snap turn AND smooth turn AND movement.
That is the reported "skipping/teleporting" head-look. After repair, BK's
provider set is identical to Main House.

Also established by the capture: tracking origin is `Floor` in BOTH scenes
(requested NotSpecified → Floor granted) — tracking mode is NOT the
divergence; zero `trackingOriginUpdated` (recenter) events during runs; and
Phase 7 perf is clean — BK holds 72 fps in every cycle on the repaired build
(enter 2.6–3.1 s, exit 6.6–6.7 s, scenes=1, handles→0, RESULT: PASS).

Desk-capture limitation (for the record): unworn, the HMD pose is not fed to
the camera (camera local stays zero) and the CharacterController collapses to
its 0.288 m minimum sphere, which rests ~0.28 m above the floor — so the
half-in-floor embodiment could not be reproduced or refuted from the desk. The
worn checklist below is the arbiter. If half-in-floor persists after this
repair, the next probe target is the CharacterControllerBodyManipulator height
update with the worn `[XrPose]` samples (`cc h=`, `feetY=`, `cam local=`)
in hand — the instrumentation is already in the build for exactly that.

### Worn-headset validation checklist (pending — requires a human)

Enter BK → floor/feet feel correct → stand still 30 s → slow look left/right
and up/down → walk → press A once (should do NOTHING now in BK) → keep
walking → turn → use several exhibits → exit → re-enter → repeat. Watch
`adb logcat -s Unity | grep -E "XrPose|JumpLogger"` during the session:
`[XrPose]` samples give feetY/cam/cc truth, `[XrPose:A]` proves what A does,
`[XrPose:Recenter]` flags re-localizations, `[XrLocomotionJumpLogger]` names
any discrete jump. In Main House, also press A once — if the same
lag/skip appears there, deactivate the `Jump` GameObject in the Main House rig
and `BCaT_QuestRig.prefab` too.

## Repair (staged in working tree)

Black Kitchen rig standardized to the canonical Main House configuration —
the 44 missing overrides inserted into the BK rig PrefabInstance (teleport/
climb/stabilized-origin GOs off, right-hand smooth turn, snap turn off,
right-hand move input cleared, teleport refs nulled,
NearFarEnableTeleportDuringNearInteraction=0, camera-offset y=1.1, cast-shadows
off). Prefab-internal reference re-assertions in the Main House instance
(NearFarInteractor, StartingGroupMembers) were verified to be no-op restatements
of source defaults and intentionally NOT copied.

**One documented deviation:** the BK rig's `Jump` GameObject is deactivated —
the A-button jump is an accidental XRI 3.3 starter-rig default, not a designed
feature, and the reported A-press destabilization is BK-specific. Main House
keeps jump this round (known-good reference untouched); if the worn Main House
check shows the same A-press instability, disable the Jump GO there and in
`BCaT_QuestRig.prefab` as well.

Desktop path untouched: all edits are inside the Quest rig PrefabInstance in
the authored-inactive `Platform/Quest` branch.
