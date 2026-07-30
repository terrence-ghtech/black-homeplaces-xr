# Settings Report

Storage: versioned JSON, `<persistentDataPath>/BCaT/settings.json`
(schemaVersion 1, forward-migration hook, corrupt-file backup+defaults,
missing-file defaults, reset-to-defaults button). Applied at startup, after
every scene load, and on every change. No PlayerPrefs in exhibit code; no
progress/session persistence by design.

| Setting | Runtime connection | Persist | Platforms | Standard mode | Kiosk visitor |
|---|---|---|---|---|---|
| Resolution | Screen.SetResolution | ✅ | Win/macOS | ✅ | ✖ hidden |
| Fullscreen / windowed | FullScreenMode switch (kiosk forces fullscreen) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Display selection | Display.Activate w/ fallback to primary when missing | ✅ | Win/macOS multi-monitor | ✅ (when >1 display) | ✖ hidden |
| VSync | QualitySettings.vSyncCount | ✅ | Win/macOS | ✅ | ✖ hidden |
| Frame-rate limit | Application.targetFrameRate (Off/30/60/120) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Quality tier | QualitySettings.SetQualityLevel by name | ✅ | Win/macOS (Quest fixed on device) | ✅ | ✖ locked to admin tier |
| Render scale | URP asset delta over tier baseline (0.5–1.5) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Shadow distance | URP shadowDistance delta (0.5–1.5×) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Texture quality | globalTextureMipmapLimit (Full/Half/Quarter) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Anti-aliasing | URP msaaSampleCount (tier default/Off/2×/4×) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Ambient effects | UniversalAdditionalCameraData.renderPostProcessing | ✅ | Win/macOS | ✅ | ✖ hidden |
| Terrain distance | Terrain.basemapDistance delta per captured baseline | ✅ | Win/macOS | ✅ | ✖ hidden |
| Vegetation distance | Terrain detail/tree/billboard distance deltas | ✅ | Win/macOS | ✅ | ✖ hidden |
| Master volume | AudioListener.volume | ✅ | all | ✅ | ✅ |
| Narration volume | AudioChannelService + BK coordinator fold-in | ✅ | all | ✅ | ✅ |
| Ambience volume | AudioChannelService + BK ducking fold-in | ✅ | all | ✅ | ✅ |
| Effects volume | AudioChannelService (registered sources) | ✅ | all | ✅ | ✅ |
| Media volume | AudioChannelService (video AudioSources) | ✅ | all | ✅ | ✅ |
| Mouse sensitivity | FirstPersonController.RotationSpeed multiplier over authored baseline (0.2–3×) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Invert Y | FirstPersonController.InvertY (added field) | ✅ | Win/macOS | ✅ | ✖ hidden |
| Subtitles | SubtitleService gate | ✅ | all (Quest readability deferred) | ✅ | ✅ |
| Text size | 1×/1.25×/1.5× scale in UiFactory, prompts, subtitles, viewers | ✅ | all shell UI | ✅ | ✅ |
| High contrast | UiFactory theme + crosshair + prompt outlines (never exhibit art) | ✅ | all shell UI | ✅ | ✅ |
| Reduced motion | Shell honors flag (no added camera/menu motion; simple fades kept) | ✅ | Win/macOS | ✅ | ✅ |
| Persistent prompts | Router focus-angle tolerance ×2 | ✅ | all | ✅ | ✅ |

Notes:
- Granular graphics options that Unity cannot safely change at runtime
  (shadowmap resolution, cascade count) are tier-bound by design and
  documented in the quality report — no dead controls are exposed.
- Kiosk visitors see only Audio + Accessibility tabs; the administrator chord
  (Ctrl+Shift+F10) opens the unrestricted panel.
- Settings apply per-machine; Quest reads the same file from its own
  persistentDataPath (no Quest settings UI this pass — documented limitation).
