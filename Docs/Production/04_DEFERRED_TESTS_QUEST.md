# Deferred User-Testing Package — Physical Meta Quest

**Status: NOT TESTED ON HARDWARE.** Everything in this file requires a
physical Meta Quest headset and is explicitly deferred to the project owner.
Nothing here has been claimed as passed. Build-time configuration findings are
in the final report; this checklist covers only what cannot be verified
without the device.

## Required package
`Builds/Quest/BlackHomeplaces-Quest.apk` (development build, IL2CPP/ARM64,
OpenXR with Meta Quest Support feature, Touch/Touch Plus/Touch Pro profiles).

## Installation
1. Headset in developer mode; connect USB; `adb devices` shows the headset.
2. `adb install -r Builds/Quest/BlackHomeplaces-Quest.apk`
3. Launch from **Library → Unknown Sources → Black Homeplaces: The XR House**.
4. Keep `adb logcat -s Unity` running during the session and save it.

## Headset information to record
Model (Quest 2 / 3 / 3S / Pro) · OS build · controller firmware · free storage
· guardian type (roomscale/stationary) · session ambient temperature.

## Test checklist

### A. Launch & rig
- [ ] App launches to the house (menu scene auto-forwards on Quest) without crash
- [ ] XR rig is active (head tracking, correct height); desktop rig absent
- [ ] Recenter (long-press Meta button) behaves correctly
- [ ] Startup time from launch to controllable: ______ s

### B. Controllers & interaction
- [ ] Ray interactors visible and stable from both controllers
- [ ] Select (trigger) activates exhibits; grip/direct interaction where exhibits use it
- [ ] Prompts show Quest wording ("Interact to …"); **no "Press E" text anywhere**
- [ ] World-space prompts readable at arm's length (note text too small/large)
- [ ] While one exhibit interface is open, other exhibits cannot be triggered
- [ ] UI raycasts hit menu/modal buttons reliably (exit modal Stay/Exit Now)

### C. Locomotion & comfort
- [ ] Movement (thumbstick) speed comfortable; note nausea triggers
- [ ] Turning: snap/smooth per rig configuration; snap angle acceptable
- [ ] No unintended height changes; stairs/thresholds navigable
- [ ] Fall recovery: jump off any ledge in Black Kitchen → returned to spawn
- [ ] Seated operation usable (if applicable)

### D. Media
- [ ] Each of the 6 video exhibits plays (packaged local files; airplane mode should still play them)
- [ ] Video + soundtrack stay in sync for a full clip
- [ ] Closing a video stops audio and clears the panel
- [ ] With Wi-Fi off entirely: videos still play (packaged); no hangs, no "Loading…" stuck > 20 s
- [ ] Black Kitchen: all 5 stations play exclusively (starting one stops the other), ambience ducks
- [ ] External-link exhibits: system browser prompt appears; returning to app resumes correctly

### E. Black Kitchen lifecycle & Addressables
- [ ] Portal enters through loading screen; spawn correct
- [ ] Exit Reflection modal works via ray select; both Stay and Exit Now paths
- [ ] Return to main house at kitchen-return spawn
- [ ] Repeat entry/exit ×5: no crash, no duplicated audio, no rising load times
- [ ] Full session ≥ 30 min: no thermal shutdown; note warmth/fan

### F. Performance & memory (record numbers)
- Average FPS in main house: ______ (target 72)
- Minimum FPS / worst area: ______
- FPS in Black Kitchen: ______
- `adb shell dumpsys meminfo org.bcatlab.blackhomeplaces` after 30 min: ______ MB
- Reprojection/judder locations: ______

### G. Visual quality & accessibility
- [ ] No missing/pink materials; terrain and vegetation render correctly
- [ ] Text/prompt readability at default size; with Large text (set on desktop first or via settings file)
- [ ] Subtitle overlay position readable when subtitle content exists (currently no approved tracks installed)
- [ ] High-contrast mode does not alter exhibit artwork

## Acceptance criteria
Stable 72 FPS in normal navigation (occasional dips acceptable), no crash in a
30-minute session, all exhibits interactable with Quest wording, media plays
offline, Black Kitchen survives 5 repeat entries, no thermal shutdown.

## Results template
```
Date/tester:            Headset model/OS:
Install OK:             Launch OK:            Startup s:
Rig/controllers:        Prompts correct:
Media offline:          BK repeat x5:
Avg FPS house/kitchen:  Min FPS(where):
Meminfo 30min:          Thermal notes:
Comfort notes:          Readability notes:
Failures/logs attached: 
```
