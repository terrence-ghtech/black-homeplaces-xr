# Interaction Migration Report

## Scripts that previously polled keyboard interaction input (baseline)

Recorded before migration in `00_BASELINE_STATE.md` (15 production scripts).

## Migration outcome per script

| Script | Before | After |
|---|---|---|
| MediaVideoController | `Keyboard.current[interactKey]` + own raycast/trigger check | `IInteractionTarget` (LookRaycast → focus target; ProximityTrigger → proximity target); E/Esc-to-close via FocusedUiInput; Media blocker while open |
| SpatialAudioToggle | E + own raycast | `IInteractionTarget`, dynamic listen/pause verb; stops audio on disable |
| InteractableLinkLauncher | hardcoded `eKey` + per-frame raycast | `IInteractionTarget`; XR `OpenLink` respects blockers |
| SimpleImagePopupInteractor | E + own raycast, raw `XRSettings` prompt | `IInteractionTarget`; prompt via shared `InteractionPromptText` |
| SimpleImagePopupController | Esc/E close polling | FocusedUiInput; Modal blocker with force-close |
| HolographicSlideshow | Esc/E/arrows polling | FocusedUiInput (incl. InteractHeld release-guard); Modal blocker |
| LindaLeaksPanelOpener | E + raycast; album Q/R/E polling | `IInteractionTarget` for opening; album shortcuts via FocusedUiInput; XR open respects blockers |
| MeshellArticleNotebookInputRouter | E + RaycastAll parent-collider walk | `IInteractionTarget` (router LOS reproduces trigger-skip/self-skip semantics) |
| MeshellArticleReaderController | Esc close polling | FocusedUiInput; Modal blocker |
| PrivacyLawExhibitController | proximity+E open; Esc/E close | nested `PrivacyLawInteractionTarget` (proximity-gated availability); FocusedUiInput close; Modal blocker; XR open respects blockers |
| BlackKitchenPortalController | E + RaycastAll | `IInteractionTarget` (priority 1); XR routes via RequestXRSelect; control-filter list merged with code defaults (scene had stale short list) |
| BlackKitchenInteractionManager | E polling + own selection | `IExclusiveInteractionZone` — router owns input/blockers/cooldown, manager keeps validated station selection and shared prompt; forwards exit-aim presses |
| BlackKitchenExperienceController | E polling for exit + modal key polling | `HandleExitInteract()` fed by the zone; modal keys via FocusedUiInput (same Esc/S / Enter/E/L wording); Modal blocker while modal open |
| QuiltVideoPopUp (dead code, unreferenced) | E polling | polling removed; XR/programmatic entry points only |
| LindaLeaksVideoPopUp (dead code, unreferenced) | E polling | polling removed; XR/programmatic entry points only |

## Remaining direct polling (sanctioned)

- `BCaT/ProductionCore/Interaction/InteractionInput.cs` — DesktopInteractionInputProvider (E + click) and FocusedUiInput (modal keys). The single home for world/modal input reads.
- `BCaT/ProductionCore/Kiosk/KioskController.cs` — visitor-activity tracking and administrator chords (not interaction input).
- Non-production: `Animated Tropical Vegetation/DemoSceneContent/HideShowButtons.cs` (asset-store demo, not in any production scene), `RealBlend/Editor/*` (editor tools).

`ProductionValidationAudit` (BCaT → Production Setup → Validation Audit)
enforces this list and fails on any new direct poll in production folders.

## Router ownership rules

1. `InteractionState.IsBlocked` (Menu/Modal/Media/PlayerControl blockers or a
   scene transition) → no selection, no dispatch, prompts hidden, zones told to
   suppress prompts.
2. An active exclusive zone (Black Kitchen scene) → router forwards one
   cooldown-gated interact signal per frame; no parallel selection.
3. Otherwise: valid candidates = registered + available + within MaxDistance +
   within MaxViewAngle (unless proximity-based) + line-of-sight (triggers and
   own colliders never block). Selection = highest Priority, then smallest
   view angle, distance as tie-break. Exactly one target receives OnInteract.
4. Global cooldown 0.25 s between dispatches (duplicate-input suppression).
5. XR: RequestXRSelect applies rules 1 and 4 to event-driven selects.

## Prompt behavior

- Desktop: single screen-space bottom-center prompt (InteractionPromptUi) with
  the selected target's text ("Press E to …", context verbs preserved:
  watch/listen/pause/open/read/view photos/Enter Black Kitchen/Play|Stop
  <station>); per-exhibit world canvases keep their authored visibility logic.
- Quest: world-space prompts only, "Interact …" wording; desktop keyboard
  language never appears (wording sourced from the same GetPrompt(xr) call).
- Prompts hide during menus/modals/transitions (blockers) and return after.
- Accessibility "persistent prompts" doubles the view-angle tolerance.

## Known exceptions (by design)

- Black Kitchen station selection stays inside its manager (validated 31-step
  behavior preserved) under the exclusive-zone contract.
- Exhibit XR wiring calls existing public methods (`OpenLink`,
  `Open(SelectEnterEventArgs)`, `OnXRSelect`); these validate against
  blockers/router rather than being rewired in scenes/prefabs.
- Modal interfaces intentionally receive their own navigation keys through
  FocusedUiInput while they hold a Modal/Media blocker; the world router is
  silent for that duration.
