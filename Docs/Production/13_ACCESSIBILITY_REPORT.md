# Accessibility Report

## Implemented and connected
| Feature | Mechanism | Notes |
|---|---|---|
| Subtitle system | SubtitleService (persistent), settings-gated; desktop bottom band, Quest camera-anchored world canvas | **No approved subtitle/transcript source content exists in the project.** The system loads `SubtitleTrack` assets from `Resources/Subtitles`; none are installed, so nothing displays. Content must be authored from approved transcripts — never invented. |
| Transcript viewer | TranscriptViewer: keyboard/mouse, scrollable, text-scaled, high-contrast aware, reliable close, controls restored; reachable from the Exhibit Directory | Shows an explicit "no approved transcripts installed yet" notice rather than placeholder text. Structured for Quest UI but readability on device is deferred to headset validation. |
| Text size | Normal / Large (1.25×) / Extra-large (1.5×) applied by UiFactory (menus, dialogs, directory, transcripts), interaction prompt, subtitles, crosshair scale | Applies to shell UI; world-space exhibit canvases keep authored sizes (artwork untouched). |
| High contrast | UiFactory theme (near-black panels, yellow focus), prompt/subtitle outlines, crosshair colors | Never alters exhibit artwork or archival images. |
| Reduced motion | Settings flag honored by the shell (no added menu animation, camera effects, or head-bob exist in the desktop rig; transitions remain simple fades) | Quest comfort options deliberately deferred until physical comfort feedback (see 08). |
| Persistent prompts | Router doubles the focus-angle tolerance so prompts stay up | |
| Audio separation | Master / Narration / Ambience / Effects / Media sliders; Black Kitchen coordinator folds Narration/Ambience into its authoritative targets | |
| Keyboard-accessible menus | All shell UI built from uGUI Selectables with automatic navigation, visible focus states (SetSelectedGameObject on open), Escape handling | |
| Clear error messages | Visitor-phrased media messages; loading-scene failure message with automatic return | |
| Reset / unstuck / return to entrance | Pause menu entries backed by ResetService (safe teleport reusing the hardened arrival path) | |

## Exhibit navigation
Exhibit Directory (pause menu / kiosk-safe): production exhibits derived from
live scene content, grouped by authored area names, availability state per
exhibit, Return-to-Main-Entrance action, Transcripts access. No floor map —
no approved map asset exists and an improvised one could mislead visitors
(documented limitation, allowed by plan).

## Missing approved content (action for project team)
1. Transcripts/subtitle text for: the six exhibit videos and the six Black
   Kitchen narrations (5 stations + exit reflection). Once approved text
   exists, create `SubtitleTrack` assets (menu: BCaT → Subtitle Track) with
   `mediaId` = video file name or narrative id; they are picked up
   automatically by subtitles and the transcript viewer.
2. Approved exhibit descriptions for the directory (names/locations shown
   today are scene-derived facts; descriptions were omitted rather than
   invented).
3. Credits copy for the main-menu Credits panel (currently shows only
   product/company/version).

## Quest accessibility items awaiting physical validation
Subtitle world-canvas readability and placement; text sizes at headset
resolution; prompt legibility; comfort defaults. See
`04_DEFERRED_TESTS_QUEST.md` §G.
