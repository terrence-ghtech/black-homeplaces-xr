# Standard & Kiosk Mode Report

## Mode selection
One shared application and scene set; `ApplicationModeService` resolves the
mode once per launch: CLI (`-kiosk`/`-standard`/`-bcatMode=…`) → 
`<persistentDataPath>/BCaT/mode.config.json` → default Standard. Kiosk is
honored only on Windows/macOS players. No scenes are duplicated; the mode
only changes shell behavior.

## Launch behavior
- **Standard**: MainMenuScene shows the desktop menu (Begin Experience /
  Settings / Accessibility / Credits / Quit with confirmation); cursor free;
  gameplay input blocked while the menu is open and restored on Begin.
- **Kiosk**: menu is skipped — the experience begins immediately, fullscreen
  enforced, administrator's fixed quality tier applied over any saved setting.
- **Quest**: menu scene auto-forwards into the house (no desktop shell).

## Menu behavior (standard)
Pause menu on Escape (only when no exhibit modal owns Escape): Resume,
Settings, Exhibit Directory, Reset Position, Return to Main Entrance, Quit to
Main Menu (confirm), Quit Application (confirm). Opening suspends
movement/look via the reference-counted PlayerControlGate, unlocks the cursor,
registers a Menu interaction blocker (world prompts hidden, router silent),
and preserves media state; Resume restores everything. Works in the main
house, in the Black Kitchen, and after returning from it (the shell is a
persistent service, not scene-bound).

## Settings behavior
Standard: all five tabs. Kiosk visitors: Audio + Accessibility only; quality
locked; quit absent. Kiosk administrators: Ctrl+Shift+F10 → unrestricted
panel + config/log paths.

## Quit behavior
Standard: both quit paths confirm before acting; quitting saves settings and
stops media. Kiosk: no visitor-facing quit; hold Ctrl+Shift+Q ≈2 s
(configurable) to exit.

## Inactivity behavior (kiosk)
Meaningful activity = key presses, mouse movement > deadzone, mouse buttons,
open menus/modals (which also count as engagement). Timeout configurable
(default 300 s; `-bcatKioskTimeout=` override; 0 disables). While registered
long-form media is playing and `allowResetDuringMedia=false` (default), the
reset is deferred (idle clock capped at half the timeout so it re-arms
quickly after the media ends).

## Reset behavior (kiosk)
Sequence (shared lifecycle, never a bare teleport): block input + suspend
controls → honest "Resetting the exhibit…" overlay → `MediaPlaybackRegistry.
StopAll()` → `InteractionState.ForceCloseAll()` (every open modal registered a
force-close handle) → `ResetService.ReturnToMainEntrance()` (in-house: media
stop + audio-coordinator exit prep + teleport to the captured entrance pose;
from Black Kitchen: full transition through the LoadingScene) → wait for the
transition → restore cursor/controls/prompts → overlay removed. Session
state (blockers, media registry) is cleared by the sweep; settings persist.

## Administrator controls (documented in 06_INSTITUTIONAL_NOTES.md)
Exit chord, settings chord, mode file, kiosk config file (timeout, media
policy, fixed tier, chord toggles), CLI overrides, log locations. No
credentials are hard-coded anywhere.

## Remaining issues / notes
- Kiosk validation was performed on macOS (the available desktop); the
  Windows kiosk run is covered by checklist §F for the first Windows session.
- The pause menu intentionally remains available in kiosk mode (without quit
  and with restricted settings) so visitors can reach the directory,
  reset-position, and accessibility options.
