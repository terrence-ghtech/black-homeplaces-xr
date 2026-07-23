# Stage 4 — Remote Video Architecture & URL Manifest
Date: 2026-07-23

## What was implemented (code-only; no scene or prefab edits)
- **Central config**: `Assets/Resources/RemoteMediaConfig.asset` (ScriptableObject, class `Assets/Scripts/RemoteMediaConfig.cs`). Resolution order per file: explicit per-file `remoteUrl` → `remoteBaseUrl` + URL-escaped file name → local StreamingAssets (current shipped behavior). `useRemoteInEditor` keeps the editor on local files by default.
- **Single resolver**: `RuntimeMediaPaths.ResolveMediaUrl(fileName)`; all three video controllers now route through it (`MediaVideoController`, `QuiltVideoPopUp`, `LindaLeaksVideoPopUp`). `MediaVideoController.videoUrlOverride` (serialized per-exhibit) still wins if set — unchanged behavior.
- **Loading state**: code-created "Loading video…" label (TextMeshProUGUI) appears inside the popup while a video prepares; removed on prepared. No prefab/scene modifications — created at runtime.
- **Failure handling**: `errorReceived` now shows "Video unavailable. Close and reopen the exhibit to try again." instead of a frozen black frame. Reopening retries (URL re-resolved, Prepare re-issued).
- **No simultaneous buffering**: new `VideoExhibitCoordinator` — opening any video exhibit closes the previously open one (its VideoPlayer stops and its URL is cleared, releasing the network stream/decoder).
- **Release at end of playback**: `loopPointReached` on non-looping players now calls `Stop()`, releasing decoder/buffer resources while the popup stays open.
- **No startup preloading**: unchanged — on WebGL, `Prepare()` is only ever called when the visitor opens an exhibit (proximity prefetch remains non-WebGL/Quest only, preserved).
- **Quest behavior preserved**: all changes are additive and platform-neutral; Quest keeps AudioSource routing, VideoClip editor fallback, and trigger-based prefetch. Same scripts, same serialized fields — nothing renamed or removed.

## Files that must be uploaded to a CDN (from Assets/StreamingAssets/)
| File | Size (bytes) | Used by |
|---|---|---|
| and that is the truth - you know what I'm meaning_720p.mp4 | 106,877,663 | MediaVideoController (Ri exhibit, MainScene) |
| in_my_sisters_room_xr.mp4 | 52,755,676 | QuiltVideoPopUp (Sewing Room) |
| Linda_Leaks_CHOF_720p.mp4 | 27,498,077 | LindaLeaksVideoPopUp (VintageCamera prefab) |
| you don't know about style my darling_720p.mp4 | 11,537,718 | MediaVideoController |
| such lovely gravy_720p.mp4 | 6,133,227 | MediaVideoController |
| subjected_to_recognition_720p.mp4 | 230,720 | MediaVideoController |
Total: ~196 MiB.

## Exactly what to fill in when CDN URLs exist (single location)
Open `Assets/Resources/RemoteMediaConfig.asset` (Inspector or text editor):
1. Set `remoteBaseUrl` to the CDN folder URL ending in `/` (e.g. `https://cdn.example.org/bcat/videos/`). File names are URL-escaped automatically (they contain spaces and apostrophes) — **or** fill any per-file `remoteUrl` to override individually.
2. CDN requirements: HTTPS, HTTP Range request support (video seeking), correct `Content-Type: video/mp4`, and CORS `Access-Control-Allow-Origin` covering the site origin.
3. No credentials are stored anywhere in the project (requirement met — config holds public URLs only).

## Release step (deferred by design)
Local videos are **intentionally still in StreamingAssets** — per instructions they must not be removed until remote URLs are supplied and verified. After verification, create the video-less release build by temporarily moving the six .mp4 files out of `Assets/StreamingAssets/` (keep the .meta files with them) and rebuilding; `RemoteMediaConfig` then serves every player from the CDN. This is documented in the final report's deployment checklist.
