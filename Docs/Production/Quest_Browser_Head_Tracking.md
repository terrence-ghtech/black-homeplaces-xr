# Quest external-browser head tracking

The reported symptom is normal tracking of Meta's system panels but apparently
reversed head movement in the immersive world behind them. The rig uses the
Input System `TrackedPoseDriver` with position, rotation, and tracking-state
actions. Its camera updates depend on those actions continuing to report a pose
after input focus moves to the browser. A stale camera pose behind a compositor
that still tracks the headset is the suspected cause; this has not yet been
confirmed by reproducing the problem on the headset.

Meta documents the distinction between system UI input focus and an immersive
app that remains visible:
[Focus Awareness](https://developers.meta.com/horizon/documentation/unity/unity-focus-awareness/).

## Change

`QuestBrowserHeadTracking.OpenUrl` arms a fallback immediately before the normal
`Application.OpenURL` call. The link launcher, holographic slideshow, and Adinkra
website button all use this entry point. The persistent Quest service:

- Does nothing in desktop players or the Editor, including Quest simulation.
- Captures only the active XR origin's main camera with an enabled pose driver.
- Waits for Android/OpenXR focus loss, expiring after ten seconds if the launch
  produces no focus handoff.
- Reads valid center-eye poses directly from the XR subsystem in LateUpdate and
  before rendering while the app is unfocused, unpaused, rendering XR, and the
  user is present.
- Writes the camera's local pose directly, without inversion, accumulation, or
  changing the XR origin, its heading, camera height settings, or input actions.
- Stops before writing on the first frame when both focus signals return.
  Later unrelated system-menu openings do not reactivate it.

The existing pose driver stays enabled throughout. No scene, prefab, gameplay
setting, package version, or global background-input setting is changed by this
fix. If the XR subsystem cannot supply a tracked center-eye rotation, the
fallback does not substitute an identity pose.

## Validation

Run `BCaT > Diagnostics > Quest Browser Head Tracking Self Test` in Unity, or:

```sh
Unity -batchmode -quit -nographics -projectPath "$PROJECT_PATH" \
  -executeMethod BCaT.EditorTools.QuestBrowserHeadTrackingSelfTest.RunWithArchitectureValidation \
  -logFile /tmp/bcat-browser-tracking-test.log
```

The batch command also runs the project's architecture validator, including the
build-blocking rule that native XR loader queries must live in `BCaTPlatform`.

The self-test checks idle/menu isolation, launch waiting, focus return, failed
launch expiry, pause/resume, repeated launches, cleanup, both yaw/pitch directions,
preservation of a translated/rotated rig, missing tracking, and rotation-only
tracking. These checks do not emulate the Quest compositor or prove that native
center-eye poses remain available in the affected headset state.

On a newly built Quest player:

1. Verify ordinary head movement and locomotion before opening a link.
2. Activate an external-link asset. With the browser and Library/system panels
   visible, turn left/right, look up/down, and lean. Check the house remains
   anchored and moves consistently with the panels' tracking.
3. Close/dismiss the panels and verify immediate normal gameplay, with no heading
   reset. Repeat the launch, including with Library already open.
4. Repeat for Adinkra and slideshow website buttons. Verify returning from headset
   sleep while the browser is open, and returning directly to gameplay.
5. Open the system menu independently after returning. The fallback must stay off.

Player logs contain `[QuestBrowserHeadTracking] Browser overlay: applying live
center-eye pose.` when a native pose is applied, and an end message when the
fallback releases the camera. No start message while the symptom occurs means
the native pose/focus preconditions need device-side investigation.
