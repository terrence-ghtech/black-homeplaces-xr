# Media Validation Report

## Architecture (after Phase 6 hardening)

Resolution order — native desktop & Quest: **packaged StreamingAssets →
per-file remote URL → remote base URL (Unity CCD)**; WebGL remnant keeps its
legacy remote-first order. Empty result = graceful skip with visitor message.
Packaged manifest for Quest (`RemoteMediaConfig.packagedFileNames`) because
APK contents cannot be probed with File.Exists.

Lifecycle per controller (MediaVideoController, the sole live video
controller): prepare-on-demand with "Loading video…" state → 20 s prepare
watchdog → success (play + MediaPlaybackRegistry + subtitle notify) / failure
(visitor message, `[MediaError]` structured log, exhibit stays closeable) →
close stops playback, clears/releases RenderTextures (owned RTs destroyed on
teardown), clears URL, unsubscribes events, restores prompts/cursor. Kiosk
reset and return-to-entrance stop all registered media through one call;
scene transitions sweep the registry in LoadingSceneController.

Visitor-facing failure messages (no raw exceptions):
"This media is currently unavailable." · "This exhibit requires an internet
connection." · "The media file could not be loaded." — each with a close hint.

Error logging fields: exhibit name, requested path (query strings stripped —
no tokens logged), platform, error message, whether remote was attempted,
whether the player recovered.

## macOS validation (development machine, Apple Silicon M4)

| Item | Result |
|---|---|
| Bundle StreamingAssets path | ✅ packaged videos resolve inside .app (boot log evidence) |
| persistentDataPath | ✅ settings/smoke reports written under Application Support |
| URL formatting / escaping | ✅ CCD URLs escape spaces/apostrophes correctly (baseline + final boot logs) |
| MP4 (H.264 720p) playback | ⚠ resolver + packaging validated (boot logs, files in bundle); actual decode/playback requires the manual walkthrough (checklist §A) — the automated smoke test exercises scene cycles, not video playback |
| Remote playback | ✅ URL construction/escaping validated in boot logs; live CCD fetch requires the manual walkthrough |
| Missing-file behavior | ✅ resolver logs and returns empty → unavailable message |
| Case-sensitivity | ✅ manifest + resolver compare case-insensitively; packaged file names copied verbatim from the archive the exhibits reference |
| Apple Silicon plugins | ✅ no native plugins in the media path |
| RenderTexture cleanup / stop-on-close | ✅ code-path preserved from validated baseline + release on destroy |

## Windows validation

Build-level: ✅ packaged StreamingAssets folder present in the Windows build;
same resolver/watchdog code compiled in.
Runtime: ⚠ **no Windows machine was available in this environment** — path
separators (Path.Combine), byte-range playback via Media Foundation, and
external-link behavior are covered by `03_TEST_CHECKLISTS.md` §A/§G/§H for
the first Windows session. No Windows runtime claim is made.

## Quest preparation (no hardware claim)

Configured: packaged media in APK addressed by jar: URL; manifest-declared
existence; remote fallback; RenderTexture lifecycle identical to desktop;
audio through the same channel service. Deferred to headset testing: decoder
behavior, offline on-device, UI state during preparation, cleanup during
scene transitions on device — see `04_DEFERRED_TESTS_QUEST.md` §D.

## Offline behavior (desktop)

By construction with packaged media + local Addressables: packaged videos
resolve locally (no network request), remote-only media shows the connection
message after the 20 s watchdog, no indefinite loading states, and the player
is never locked (close always available via E/Esc and XR select). The
machine-level offline run (network disabled end-to-end) is checklist §G for
the owner's validation session.
