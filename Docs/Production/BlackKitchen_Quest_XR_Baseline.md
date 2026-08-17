# Black Kitchen — Quest XR Embodiment Baseline

**Date:** 2026-08-10 · **Scene:** `Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity`

The smallest known-good Quest embodiment for Black Kitchen: correct spawn, correct
physical height, 1:1 head tracking, continuous stick walking. Nothing else.

Built from XR Interaction Toolkit 3.4.1, XR Core Utils 2.6.0, and Unity's own
`XRI Default Input Actions` sample. No configuration was taken from any other
BCaT scene or rig, so this baseline carries no inherited project-specific XR
assumptions.

## Hierarchy

```
Platform                                   (existing root, identity, scale 1)
└── Quest                                  authored INACTIVE, identity, scale 1
    └── XR Origin                          pos (0, 0.02, -4.43), rot identity, scale 1
        │   XROrigin, XRBodyTransformer, LocomotionMediator,
        │   ContinuousMoveProvider, ScenePlayerRig(kind = XR)
        └── Camera Offset                  local (0,0,0), scale 1
            ├── Main Camera                local (0,0,0), scale 1, tag MainCamera
            │     Camera, UniversalAdditionalCameraData,
            │     AudioListener, TrackedPoseDriver
            ├── Left Controller            local (0,0,0), scale 1 — TrackedPoseDriver
            └── Right Controller           local (0,0,0), scale 1 — TrackedPoseDriver
```

Every transform from `Platform` down to `Main Camera` has scale `(1,1,1)`; there is
no scaled ancestor anywhere in the tracking chain, so head motion is 1:1.

## Tracking origin

| Setting | Value | Why |
| --- | --- | --- |
| `RequestedTrackingOriginMode` | **Floor** | Explicit, not inherited. The runtime reports head poses that already include the wearer's height. |
| `CameraYOffset` | **0** | Floor mode ignores it; authored at 0 so no hidden height can exist in the rig. |
| `Camera Offset` local Y | **0** | Floor mode forces this to 0 at runtime; authored to match. |
| `Origin` | the `XR Origin` GameObject | The same object locomotion moves and the arrival controller places. |

Physical eye height therefore comes from the headset and only from the headset.
There is no compensation term anywhere that could fight floor tracking.

One consequence to confirm in the headset: Floor mode hands the choice of reference
space to the OpenXR runtime (that selection is native, not visible in the package
source). If Meta's runtime backs it with `STAGE`, the wearer starts at their real
physical offset and facing relative to their room's play-area centre rather than
exactly on the entrance marker; if it backs it with `LOCAL_FLOOR`, they start on
the marker. Either way the *height* is right, which is what this mode is chosen
for. If the wearer does start offset, the correct remedy is the runtime's own
recenter — not a rig-side offset or a corrective teleport, both of which are
exactly what this baseline exists to exclude.

## Head tracking

`TrackedPoseDriver` on `Main Camera` is the single tracked-pose owner for the view:

- `trackingType` = RotationAndPosition (so leaning and crouching translate the head)
- `updateType` = UpdateAndBeforeRender
- position ← `<XRHMD>/centerEyePosition`
- rotation ← `<XRHMD>/centerEyeRotation`
- tracking state ← `<XRHMD>/trackingState`

A project-wide search confirms **no** script writes to a camera transform, so
nothing competes with the driver.

Controllers use the same driver against `<XRController>{LeftHand|RightHand}/`
`devicePosition`, `deviceRotation`, `trackingState`. They are tracked transforms
only — no interactors, no visuals, no interaction rays.

All actions are **directly serialized** on the components rather than references
into a project `.inputactions` asset. `TrackedPoseDriver` and `XRInputValueReader`
each enable their own embedded action, so the rig needs no `InputActionManager`
and inherits no project input configuration.

## Locomotion

One provider, one path:

`ContinuousMoveProvider` + `LocomotionMediator` + `XRBodyTransformer`, all on
`XR Origin`.

| Setting | Value |
| --- | --- |
| `moveSpeed` | 1.5 m/s (the one tuned value — a normal walking pace) |
| `enableStrafe` | true |
| `enableFly` | false |
| `forwardSource` | none → the camera, so "forward" is where the wearer looks |
| left hand move input | `<XRController>{LeftHand}/{Primary2DAxis}` |
| right hand move input | **Unused** |
| `mediator` | wired explicitly (no runtime search) |

Deliberately absent: teleportation, teleport interactors, jump, climb, grab move,
snap turn, recenter, body rotation, comfort vignette, interaction rays,
XR Interaction Manager, `XRInputModalityManager`.

### Right-stick turning (added after headset validation of Baseline A)

One `ContinuousTurnProvider`, also on `XR Origin`, sharing the same
`LocomotionMediator`:

| Setting | Value |
| --- | --- |
| `turnSpeed` | 60 °/s |
| `enableTurnLeftRight` | true |
| `enableTurnAround` | **false** |
| right hand turn input | `<XRController>{RightHand}/{Primary2DAxis}` |
| left hand turn input | **Unused** — the left stick stays movement-only |
| `mediator` | wired explicitly |
| `transformationPriority` | 0 |

Smooth turning rather than snap, and those values, were read off the main house
scene rather than assumed: its Starter Assets rig instance sets
`SnapTurnProvider.m_Enabled = 0`, keeps `ContinuousTurnProvider` enabled with
`m_EnableTurnAround = 0`, and sets the Right Controller's
`ControllerInputActionManager` to `m_SmoothTurnEnabled = 1` /
`m_SmoothMotionEnabled = 0` — the combination that enables the continuous "Turn"
action and disables "Snap Turn".

Only the behaviour was reproduced, not the machinery: the input is a directly
serialized action on the same right-thumbstick path the main house "Turn" action
uses, so the baseline stays self-contained and pulls in no
`ControllerInputActionManager` or other Starter Assets input plumbing.

**There is exactly one artificial rotation source, and no `SnapTurnProvider`
anywhere in the rig.** Physical head rotation remains the primary look path, and
`forwardSource` still resolves to the camera, so walking stays relative to gaze.

### No CharacterController and no GravityProvider — deliberate

`ContinuousMoveProvider` projects its motion onto the origin's XZ plane, so with
nothing driving vertical motion the origin's Y never changes. Consequences:

- The wearer cannot fall and cannot be lifted by a step, so no fall-recovery or
  Y correction is needed anywhere.
- The view is exactly still the instant the stick is released.

The cost is that this baseline has **no walk collision** — the wearer can pass
through walls and counters. Collision and gravity are a later phase; adding a
CharacterController is what reintroduces height, step-offset and grounding
variables, which is precisely what this baseline exists to exclude.

## Spawn — one placement path

The rig is authored at the exact world transform of the existing
`BlackKitchenEntry` `SceneSpawnPoint`: position `(0, 0.02, -4.43)`, identity
rotation. The spawn point itself was read, never rewritten.

When the player arrives through a scene transition, the shared
`SceneArrivalController` re-applies that transform. Its XR path is a single
`SetPositionAndRotation` — the desktop `CharacterController` feet-alignment and
`desktopSpawnSafetyLift` branches are skipped for an XR rig — so the authored pose
and the arrival pose are byte-identical and arriving cannot produce a jump.

There is no second corrective step, no delayed teleport, and no added Y offset.

The floor at the spawn is `y = 0` (the top face of `DarkGroundingPlane`, which sits
at `y = -0.05` with a height of `0.1`). The origin is therefore 2 cm above the
floor — that 2 cm is the scene's own existing authored spawn value, shared with the
desktop rig, and was left untouched rather than "corrected" in the rig.

Writers to the origin transform, and no others:

1. `SceneArrivalController` — once, on arrival.
2. `ContinuousMoveProvider` via `XRBodyTransformer` — only while the stick is held.
3. `ResetService` — only on an explicit user reset / unstuck request.

`BlackKitchenExperienceController.UpdateFallRecovery` is a pre-existing per-frame
guard. For an XR rig it resolves its test transform to `cam.transform.root`, which
is the `Platform` root, permanently at `y = 0` and never below the `-2.5`
threshold — so it is inert on Quest, and it was left alone rather than repaired.

## Isolation

- No Main House file was read, referenced, or modified. Nothing was copied from
  `BH_XR_MainScene`, its rig, its locomotion, its settings, or its portal.
- The project's own `BCaT_QuestRig.prefab` was deliberately **not** used as a
  template, so no project-specific Quest behavior is inherited.
- No legacy Black Kitchen XR system was restored. Verified absent from the scene:
  `BlackKitchenXrSelectRelay`, `*_QuestXRSelect`, `QuestXrSelectCollider`,
  `XrControllerVisibilityGuard`, `XrLocomotionJumpLogger`, `XrPoseProbe`, and any
  Black Kitchen XR prompt logic.
- Desktop is untouched. `Platform/Desktop` and every content, audio, media and
  exhibit object in the scene is byte-identical; the only pre-existing line the
  build replaced was `questBranch: {fileID: 0}` on `ScenePlatformBinding`.

`BCaTArchitectureValidator` still declares this scene `supportsQuest: false`.
That is correct for now and is the phase boundary: the flag turns on the rules
that require XR prompts, XR select surfaces and reachable interactables, none of
which this embodiment-only phase provides. Flip it when interaction lands.

## Validation

| Check | Result |
| --- | --- |
| `BCaTArchitectureValidator.RunBatch` | 0 errors, 0 warnings |
| Pre-build gate (`BCaTBuildValidation`) | PASS — 0 errors, 0 warnings |
| Play Mode, forced Desktop | 14/14 PASS — desktop rig, `InputSystemUIInputModule`, no XROrigin active |
| Play Mode, forced Quest | 14/14 PASS — `XR Origin` rig kind XR, one XROrigin, `XRUIInputModule`, `Camera.main` = `Main Camera` |
| Runtime exceptions | none |

Play Mode runs in the Editor have no XR device, so they prove structure,
activation and platform isolation — not felt tracking quality. Spawn accuracy,
physical eye height, head-tracking feel and walking must be confirmed in the
headset; see the checklist below.

## Main House entrance affordance (enabler for headset testing)

Baseline A could not be reached in headset because the Main House entrance portal
had no Quest affordance. Root cause: both of the portal's colliders
(`KitchenIslandTrigger`, `KitchenIslandInteractable`) are **triggers**, and both
XRI casters ignore trigger colliders — so the controller ray never hit the portal
and there was no hover, prompt, haptic or select. Desktop was unaffected because
it uses camera raycasts.

Fixed with the shared system only, no new runtime type:

| Requirement | How it is met |
| --- | --- |
| XR hover announcement | `XrSelectSurface` → `InteractionRouter.RequestXRHover` → shared `InteractionPromptUi` |
| Standard BCaT Quest prompt | `SharedInteractionPrompt.Format` → **"Enter — Black Kitchen"** |
| Haptics | Inherited: the Main House rig's interactors carry `SimpleHapticFeedback` (hover 0.25/0.1 s, select 0.5/0.1 s), so any `XRSimpleInteractable` gets the same pulses as every other interactable |
| One XR select surface | `XrSelectSurface` mirrors exactly **one** collider (`KitchenIslandInteractable`) into a non-trigger, contact-free aim twin |
| Select enters Black Kitchen | `selectEntered` → `RequestXRSelect` → `OnInteract` → the portal's existing `EnterBlackKitchen()` → `RequestTransition(BlackKitchen_MemoryScene, BlackKitchenEntry)` |

Two changes only:

1. `BlackKitchenPortalController.GetPrompt(bool xr)` now routes through
   `SharedInteractionPrompt.Format(xr, Enter, "Black Kitchen", desktopOverride: desktopPrompt)`.
   Desktop keeps its authored string verbatim (`"Press E to Enter Black Kitchen"`);
   only the Quest branch is new, so the headset never sees a keyboard key.
2. One `XrSelectSurface` on the `BlackKitchenPortalController` GameObject, with
   `sourceColliders` set explicitly to the `KitchenIslandInteractable` collider.
   It must sit on the object carrying the `IInteractionTarget` — `XrSelectSurface`
   resolves its owner on itself or an ancestor, and here the colliders are
   *siblings* of the controller, which is why the source is named explicitly
   rather than left empty.

Verified in Play Mode (forced Quest): `[XrSelectSurface] 'BlackKitchenPortalController'
built 1 XR aim surface(s)`, alongside the four other exhibits using the identical
mechanism. In the forced-Desktop phase **zero** surfaces are built, so desktop is
bit-for-bit unchanged.

Not restored, and not needed: `BlackKitchenXrSelectRelay`, `*_QuestXRSelect` twin
objects, `QuestXrSelectCollider`, and any Black Kitchen-specific XR prompt logic.

Note on the capability flag: with Baseline A in place, flipping Black Kitchen to
`supportsQuest: true` now yields **0 findings** (measured) — the Quest branch, XR
rig and MainCamera it previously lacked are what the rule set was waiting for. It
was deliberately left `false`: it adds no enforcement of the portal (BCAT-Q001 keys
off colliders in the target's own subtree, and the portal's are siblings), and the
flip should be owned by whoever validates Black Kitchen's exhibit interaction on
Quest.

**Correction to an earlier claim in this document:** it previously said the audio
stations needed no XR help because their `IExclusiveInteractionZone` reads the
Quest interact button directly. That was wrong.
`QuestInteractionInputProvider.InteractPressedThisFrame` is hardcoded `false` —
Quest activation is normally meant to arrive event-driven via
`InteractionRouter.RequestXRSelect` — so the zone could highlight a station on
Quest but never activate one.

## Audio stations on Quest

Two things were true: the zone's activation path is dead on Quest (above), and
this rig deliberately has **no XRI interactors and no XRInteractionManager**, so
there is nothing to cast a select ray. Rather than introduce an interaction-ray
stack into the validated rig, the zone was extended to read the controller trigger
in place of the desktop interact key.

**The only file changed is `BlackKitchenInteractionManager.cs`.** No scene change,
no change to any station, clip, AudioSource, coordinator, collider or position.

- **Selection** stays the existing `ResolveTarget()` head/camera-gaze logic
  (direct camera-ray hit on a station trigger within `rayDistance`, then
  screen-centre angle, then distance). `Camera.main` on Quest is the head camera,
  so the wearer aims by looking. Untouched, and identical on both platforms.
- **Activation** is `interactPressed || QuestTriggerPressedThisFrame()`, then the
  existing `ActivateSelected()` → `BlackKitchenAudioInteractable.Toggle()` →
  `coordinator.RequestNarrative(...)`. Exactly the desktop path.
- **Input** is a private, non-serialized `InputAction` bound to
  `<XRController>{LeftHand}/{TriggerButton}` and `{RightHand}/{TriggerButton}`,
  created lazily only when `PlatformCapabilities.IsXRActive` and disposed in
  `OnDisable`. Nothing global changes: `QuestInteractionInputProvider` is
  untouched, so no other scene or zone gains per-frame Quest input. Desktop never
  creates the action at all (verified: the "Quest interact action enabled" line
  appears only in the forced-Quest phase).
- `WasPressedThisFrame()` gives exactly one activation per deliberate press, so
  holding the trigger cannot retrigger and the existing toggle/replay behaviour is
  preserved. Blockers and modals still gate it, because the router skips `ZoneTick`
  entirely while `InteractionState` is blocked.
- **Prompt** wording now passes `PlatformCapabilities.UseXRPrompts` to the same
  existing `SharedInteractionPrompt.Format` call instead of hardcoding `false`, so
  Quest reads "Play — Birthday Cake Story" rather than "Press E to…". Desktop
  passes `false` and resolves to the identical authored string as before.
- **Exit is gated to desktop.** The pre-existing exit branch inside `ZoneTick`
  would otherwise have been reached by the Quest trigger, and the exit modal is
  keyboard-driven — it would have stranded a headset user in a dialog they cannot
  dismiss. Quest exit interaction remains a later phase.

Deliberately **not** used here: `XrSelectSurface`, `XRSimpleInteractable`,
`XRRayInteractor`, `NearFarInteractor`, `XRInteractionManager`,
`InputActionManager`, and every legacy Black Kitchen XR system.

Still not wired for Quest, deliberately: videos, UI, portals, and every other
Black Kitchen content type. (The exit flow is now wired — see below.)

## Exit flow — three-way choice, siloed platform UIs

Activating Exit no longer starts the reflection audio and no longer offers a
two-way Stay/Exit modal. It presents a choice, and the audio only begins if the
user picks Listen.

`BlackKitchenExperienceController` owns the **decision** and implements
`IBlackKitchenExitChoiceHandler`:

| Decision | Effect |
| --- | --- |
| `ChooseListen()` | Starts the existing reflection audio via the existing `audioCoordinator`, closes the choice, keeps the player in the kitchen. Never waits for the clip. |
| `ChooseLeaveNow()` | Stops the reflection and runs the existing `ExitToMainHouseRoutine()`. Available whether or not audio is playing. |
| `ChooseCancel()` | Closes the choice and stays. Deliberately leaves already-playing audio alone — cancelling an exit should change nothing. |

Presentation is a platform adapter (`IBlackKitchenExitChoiceUi`), and the two are
independent so neither platform's input model constrains the other:

- **Desktop** — `BlackKitchenExitChoiceDesktopUi`: the existing screen-space
  overlay architecture with a third button added, plus keyboard shortcuts read
  through the sanctioned `FocusedUiInput` helper (L Listen · Enter/E Leave Now ·
  Esc/S Stay). Cursor and `FirstPersonController` handling is unchanged and now
  runs only on desktop.
- **Quest** — `BlackKitchenExitChoiceQuestUi`: a **world-anchored** panel chosen
  by head gaze and confirmed with the controller trigger — the same idiom the
  audio stations already use. No XRI interactors, interaction manager, rays, input
  asset or EventSystem involvement. It is world-anchored rather than head-locked
  because a head-locked panel cannot be gaze-selected; it re-centres only if the
  wearer walks more than 2.4 m away or turns more than 55° off it, which keeps it
  stable to aim at while making it impossible to strand the panel behind you while
  the choice is blocking other interaction.

Re-entry works: after Listen, the exit interface stays live, and re-activating it
re-opens the choice with Listen omitted (the body text says the reflection is
playing) so Leave Now and Stay remain available at any time.

The exit branch in `BlackKitchenInteractionManager.ZoneTick` is no longer gated to
desktop, since Quest now has its own presentation instead of the keyboard modal.

Zero scene changes: both adapters build their UI at runtime, as the previous modal
did.

### Exit discoverability — signage and the Quest affordance

**Why the plaque was a blank black rectangle.** `ExitInterface/ExitPrompt` is a
world-space canvas holding a dark background `Image` plus a `TMP_Text`. Every
frame `BlackKitchenExperienceController.Update()` writes that text and then sets
`exitPromptText.enabled = false`. That line implements the project-wide policy in
`WorldInteractionPromptVisual` — generic floating *activation* prompts are hidden
on every platform, because the shared bottom-of-view prompt is the only activation
surface. The text was correctly suppressed; the background never was. All that
survived on screen was the empty panel.

The fix does not fight that policy. The activation prompt is left exactly as it is,
and the plaque gains its own permanent **signage** label, which is not an
activation prompt and so is not policy-managed. It carries no
`PlatformInteractionPrompt`, so `LegacyInteractionPromptSuppressor` — which
identifies legacy prompts by that component's ownership — never touches it.

`BlackKitchenExitSign` on `ExitPrompt` (one component, two wired references):

- **Signage, both platforms**: a runtime `ExitSignLabel` child reading
  **"Exit Black Kitchen"**, bold, centred over the existing background. The canvas
  is already `RenderMode.WorldSpace` and parented to the exit interface, so it is
  spatially part of the exit and never head-locked on either platform. Typography
  is platform-conditional: 26 pt desktop, 34 pt Quest (the plaque is 0.90 m ×
  0.15 m at 0.0016 scale).
- **Quest targeting cue**: the component polls `controller.IsAimingAtExit()` — the
  same head-gaze test the interaction manager already uses — and on acquisition
  warms the background to amber, brightens the label, and reveals an
  `ExitSignHint` child reading "Pull the trigger to leave". Desktop gets none of
  this and keeps its existing behaviour.
- **Haptics**: `BlackKitchenQuestExitHaptics`, a local static helper using
  `UnityEngine.XR.InputDevices.SendHapticImpulse` on held controllers. Acquisition
  0.25 for 0.1 s, activation 0.5 for 0.1 s — matching what Main House's
  `SimpleHapticFeedback` produces. It no-ops when not XR. No global haptic
  service, no XRI interactor or `HapticImpulsePlayer`, nothing shared with Main
  House.

Activation is unchanged: `BlackKitchenInteractionManager.ZoneTick` already routes
gaze-at-exit plus trigger to `RequestExitChoice()`. The sign component never
activates anything, and the manager was not modified. The only controller change
is one line firing the activation pulse after every existing guard, so it cannot
fire on a rejected activation and no-ops on desktop.

Cost to note: the sign polls `IsAimingAtExit()` each frame on Quest, which is one
extra short `Physics.RaycastAll`. That was accepted in exchange for leaving the
interaction manager and the audio-station path completely untouched.

### Quest UX pass — orientation, prompt hierarchy, controller-ray exit menu

**Entry orientation.** `BlackKitchenQuestOrientation` on `BlackKitchenExperience_ROOT`
builds a Quest-only, world-anchored card 2 m in front of the arrival pose reading
"Explore the Black Kitchen / Walk around to discover stories. / When a story
appears, pull either trigger to listen." It is passive — `blocksRaycasts` off, no
interaction blocker — so walking, turning, gaze discovery and activation all work
while it is up. It fades and destroys itself after 14 s **or** on the first story
activation, whichever comes first. Desktop disables the component in `Start`.

**Prompt hierarchy** in `BlackKitchenInteractionManager.UpdatePrompt()`:

```
exit-choice panel  >  exit target  >  audio discovery  >  nothing
```

The panel level is already handled by the router, which skips `ZoneTick` entirely
while `InteractionState` is blocked. The exit-target branch is Quest-gated and
returns early, so `Play — <story>` and the exit instruction can never be on screen
together. A new `HasActivatedAnyStory` flag (set after the existing `Toggle()`, with
no change to activation) drops the teaching suffix once the visitor has started a
story.

Prompts are single-line with a `·` separator — `Exit Black Kitchen · Pull trigger to
exit`, `Play — <story> · Pull trigger to listen` — because `InteractionPromptUi` is a
one-line 900×62 widget with word-wrapping off that the Main House also uses.
Two-line text would overflow it, and modifying it would reach outside Black Kitchen.

**Quest exit menu is now controller point-and-select**, replacing head-gaze
navigation. Smallest standard XRI UI setup: WorldSpace Canvas +
`TrackedDeviceGraphicRaycaster` + ordinary UI `Button`s, driven by the
`XRUIInputModule` that `ScenePlatformBinding` already installs for the Quest
profile. Two `XRRayInteractor`s are created **at runtime** as children of the
existing `Left`/`Right Controller`, with `enableUIInteraction` on and
`uiPressInput`/`selectInput` bound to directly-serialized `{TriggerButton}` actions.

They are `SetActive(false)` except while the panel is open, so Black Kitchen never
becomes a general controller-ray experience and the audio stations keep their
gaze/proximity + trigger model. Being runtime-only, they add nothing to the frozen
rig in the scene.

Two deliberate choices to check on hardware:

- **No line renderer.** `XRInteractorLineVisual` needs a `LineRenderer` material,
  which would mean `Shader.Find` at runtime — unreliable in a stripped build and
  liable to render magenta. Hover feedback is therefore the Button's own
  `highlightedColor` (raised to high contrast, 0.05 s fade) on 350×100 targets.
- **Haptics reuse the existing local helper.** XRI's `SimpleHapticFeedback` does not
  fire for UI clicks — those go through `XRUIInputModule`, not interactable select —
  so menu selection calls the already-validated `BlackKitchenQuestExitHaptics`
  rather than adding a second mechanism.

The exit sign's on-plaque hint was removed (the shared exit prompt now carries that
instruction) and its highlight stands down while the panel is open.

## Worn-headset checklist (owed)

Install the Quest APK, then confirm the entrance first:

0. Point a controller at the Black Kitchen entrance: the shared prompt reads
   **"Enter — Black Kitchen"**, you feel the same hover pulse as other exhibits,
   and pulling the trigger enters the kitchen.

Then, inside Black Kitchen:

1. You arrive standing at the kitchen entrance, facing into the room.
2. Your eye height matches your real standing height — the counter reads at the
   right height on your body.
3. Turning your head left/right follows the headset exactly, with no lag, drift or
   overshoot.
4. Looking up and down is correct and level.
5. Leaning and crouching move your viewpoint 1:1, with no exaggerated translation.
6. No unexpected view rotation at any point — the world never yaws on its own.
7. No camera jump on arrival, and none a second or two later.
8. Left thumbstick walks forward/back and strafes, relative to where you are looking.
9. Releasing the stick leaves the view perfectly still.
10. Expected in this phase: you can walk through walls, and there is no gravity.
