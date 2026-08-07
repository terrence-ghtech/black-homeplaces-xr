# BCaT Architecture Validation

Generated: 2026-08-07 10:34 · mode: report

**0 error(s), 42 warning(s).** No error-severity rule is failing; the build gate passes.

## Summary by rule

| Rule | Severity | Failures | Title |
|---|---|---|---|
| BCAT-D003 | Warning | PASS | Every XRSimpleInteractable resolves to an interaction target |
| BCAT-D004 | Warning | 2 | Every XRSimpleInteractable is reachable by XRI casters |
| BCAT-D005 | Warning | PASS | No missing script references |
| BCAT-D006 | Warning | PASS | At most one AudioListener per platform branch |
| BCAT-L001 | Warning | 17 | No Quest-only components outside Platform/Quest |
| BCAT-L002 | Warning | 2 | No Desktop-only components outside Platform/Desktop |
| BCAT-L003 | Warning | PASS | Platform/ contains only rigs and platform services |
| BCAT-L004 | Warning | PASS | DevOnly subtrees are editor-only |
| BCAT-L005 | Warning | PASS | Raw platform APIs used only in sanctioned files |
| BCAT-L006 | Error | PASS | World-interaction keyboard polling is centralized |
| BCAT-P001 | Warning | 4 | Exactly one ScenePlatformBinding per inhabited scene |
| BCAT-P002 | Warning | 1 | Platform branches are authored inactive |
| BCAT-P003 | Warning | 6 | One root Platform group with Desktop/Quest children only |
| BCAT-P004 | Warning | 2 | One EventSystem per scene with exactly one input module |
| BCAT-P005 | Warning | 2 | One rig per kind, both under Platform/ |
| BCAT-P006 | Warning | 1 | One XRInteractionManager, under Platform/Quest |
| BCAT-Q001 | Warning | 2 | Trigger-only interaction targets carry an XR select surface |
| BCAT-Q002 | Warning | 1 | Both desktop and XR prompts are valid |
| BCAT-S001 | Error | PASS | Transition destination scenes are loadable |
| BCAT-S002 | Error | PASS | Transition spawn ids resolve |
| BCAT-S003 | Warning | PASS | Each platform branch has a MainCamera |
| BCAT-S004 | Warning | 2 | Presentation scenes are head-tracked on Quest |
| BCAT-S005 | Error | PASS | Quality tiers exist with the expected names |
| BCAT-S006 | Error | PASS | Black Kitchen Addressables group uses local paths |
| BCAT-S007 | Error | PASS | Android application identifier is project-owned |

## Findings

### BCAT-D004 — Every XRSimpleInteractable is reachable by XRI casters

- `BH_XR_MainScene → _SceneContent/ImplementedContributorInstallations/RI/domino/DominoSpatialAudio` — XRSimpleInteractable has no non-trigger collider reachable by the XRI casters (both ignore triggers), so it is invisible in headset: no hover, no prompt, no select.
- `BH_XR_MainScene → _SceneContent/ImplementedContributorInstallations/RI/TV_Asset/TV_Preview` — XRSimpleInteractable has no non-trigger collider reachable by the XRI casters (both ignore triggers), so it is invisible in headset: no hover, no prompt, no select.

### BCAT-L001 — No Quest-only components outside Platform/Quest

- `BH_XR_MainScene → BuildProfiles/Web/Test_Headset_W_Keyboard/XR Device Simulator` — Quest-only component 'XRDeviceSimulator' is outside Platform/Quest.
- `BH_XR_MainScene → EventSystem` — Quest-only component 'XRUIInputModule' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → EventSystem` — Quest-only component 'XRUIInputModule' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Interaction Manager` — Quest-only component 'XRInteractionManager' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)` — Quest-only component 'XROrigin' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)` — Quest-only component 'XRInputModalityManager' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Gaze Interactor` — Quest-only component 'XRGazeInteractor' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Gaze Interactor` — Quest-only component 'TrackedPoseDriver' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Left Controller` — Quest-only component 'TrackedPoseDriver' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Left Controller/Near-Far Interactor` — Quest-only component 'NearFarInteractor' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Left Controller/Poke Interactor` — Quest-only component 'XRPokeInteractor' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Left Controller/Teleport Interactor` — Quest-only component 'XRRayInteractor' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Main Camera` — Quest-only component 'TrackedPoseDriver' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Right Controller` — Quest-only component 'TrackedPoseDriver' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Right Controller/Near-Far Interactor` — Quest-only component 'NearFarInteractor' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Right Controller/Poke Interactor` — Quest-only component 'XRPokeInteractor' is outside Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)/Camera Offset/Right Controller/Teleport Interactor` — Quest-only component 'XRRayInteractor' is outside Platform/Quest.

### BCAT-L002 — No Desktop-only components outside Platform/Desktop

- `BlackKitchen_MemoryScene → DesktopRigRoot/DesktopRig` — Desktop-only component 'FirstPersonController' is outside Platform/Desktop.
- `BlackKitchen_MemoryScene → DesktopRigRoot/DesktopRig` — Desktop-only component 'StarterAssetsInputs' is outside Platform/Desktop.

### BCAT-P001 — Exactly one ScenePlatformBinding per inhabited scene

- `BH_XR_MainScene` — No ScenePlatformBinding. Every scene with a platform group must have exactly one binding, on an always-active object, to apply the resolved platform.
- `BlackKitchen_MemoryScene` — No ScenePlatformBinding. Every scene with a platform group must have exactly one binding, on an always-active object, to apply the resolved platform.
- `LoadingScene` — No ScenePlatformBinding. Every scene with a platform group must have exactly one binding, on an always-active object, to apply the resolved platform.
- `MainMenuScene` — No ScenePlatformBinding. Every scene with a platform group must have exactly one binding, on an always-active object, to apply the resolved platform.

### BCAT-P002 — Platform branches are authored inactive

- `BH_XR_MainScene → BuildProfiles/XR` — Platform branch is authored ACTIVE. Both branches must be authored inactive; ScenePlatformBinding activates exactly one in Awake. An authored-active branch runs its Awake/OnEnable on the wrong platform.

### BCAT-P003 — One root Platform group with Desktop/Quest children only

- `BH_XR_MainScene → BuildProfiles` — Platform group is still named 'BuildProfiles'; rename to 'Platform'. The name is load-bearing for legacy branch selection.
- `BH_XR_MainScene → BuildProfiles/Web` — Legacy platform branch name 'Web'; rename to 'Desktop'.
- `BH_XR_MainScene → BuildProfiles/XR` — Legacy platform branch name 'XR'; rename to 'Quest'.
- `BlackKitchen_MemoryScene` — No root GameObject named 'Platform'. Platform rigs and platform services must live in one root platform group.
- `LoadingScene` — No root GameObject named 'Platform'. Platform rigs and platform services must live in one root platform group.
- `MainMenuScene` — No root GameObject named 'Platform'. Platform rigs and platform services must live in one root platform group.

### BCAT-P004 — One EventSystem per scene with exactly one input module

- `BlackKitchen_MemoryScene → DesktopEventSystem` — 2 EventSystems in this scene. There must be exactly one, under SceneServices/UI, whose input module is chosen at runtime by ScenePlatformBinding.
- `BlackKitchen_MemoryScene → DesktopEventSystem` — EventSystem has no BaseInputModule: UI pointer events are dead in this scene until something adds one at runtime.

### BCAT-P005 — One rig per kind, both under Platform/

- `BlackKitchen_MemoryScene → DesktopRigRoot` — Rig (kind=Desktop) is outside the platform group; rigs must live under Platform/Desktop or Platform/Quest.
- `BlackKitchen_MemoryScene → XR Origin (XR Rig)` — Rig (kind=XR) is outside the platform group; rigs must live under Platform/Desktop or Platform/Quest.

### BCAT-P006 — One XRInteractionManager, under Platform/Quest

- `BlackKitchen_MemoryScene → XR Interaction Manager` — XRInteractionManager is outside Platform/Quest.

### BCAT-Q001 — Trigger-only interaction targets carry an XR select surface

- `BH_XR_MainScene → _SceneContent/Home/FirstFloor/SewingRoom/Quilt/pillow__quilt` — Interaction target 'MediaVideoController' has only trigger colliders and no XR select surface, so it is unreachable by the XRI casters on Quest. Add an XrSelectSurface component.
- `BH_XR_MainScene → _SceneContent/ImplementedContributorInstallations/RI/domino/DominoSpatialAudio` — Interaction target 'SpatialAudioToggle' has only trigger colliders and no XR select surface, so it is unreachable by the XRI casters on Quest. Add an XrSelectSurface component.

### BCAT-Q002 — Both desktop and XR prompts are valid

- `BH_XR_MainScene → _SceneContent/ImplementedContributorInstallations/BlackKitchenPortal_ROOT/BlackKitchenPortalController` — XR prompt is empty.

### BCAT-S004 — Presentation scenes are head-tracked on Quest

- `LoadingScene` — Presentation scene has a camera but no Quest presentation branch: on Quest the view would be head-locked (no tracked pose) for the whole scene.
- `MainMenuScene` — Presentation scene has a camera but no Quest presentation branch: on Quest the view would be head-locked (no tracked pose) for the whole scene.
