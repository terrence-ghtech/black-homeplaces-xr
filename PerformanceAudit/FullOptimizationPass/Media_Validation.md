# Media Delivery Validation

Date: 2026-07-28 (validated live from this machine)

## Delivery architecture (unchanged by this pass)

- All six large exhibit videos are delivered remotely via Unity Cloud Content Delivery (UCD).
- Resolution chain: `RuntimeMediaPaths.ResolveMediaUrl` → `RemoteMediaConfig` (`Assets/Resources/RemoteMediaConfig.asset`) → `remoteBaseUrl + Uri.EscapeDataString(fileName)`.
- `useRemoteInEditor: 1` — Editor Play Mode also uses remote URLs (no local StreamingAssets dependency).
- There is **no** `Assets/StreamingAssets` folder — videos are not duplicated in the build payload. No duplication found in Resources or Addressables either.
- The Black Kitchen scene is delivered as a remote Addressables scene bundle from the same UCD bucket (`ServerData/WebGL/blackkitchen_remote_scenes_all_*.bundle`).

## Per-file validation results (HTTP, live)

| File | Status | Notes |
|---|---|---|
| Linda_Leaks_CHOF_720p.mp4 | 200 OK | 27,498,077 bytes |
| and that is the truth - you know what I'm meaning_720p.mp4 | 200 OK | |
| in_my_sisters_room_xr.mp4 | 200 OK | |
| subjected_to_recognition_720p.mp4 | 200 OK | |
| such lovely gravy_720p.mp4 | 200 OK | |
| you don't know about style my darling_720p.mp4 | 200 OK | |
| blackkitchen_remote_scenes_all_ad2c56ef….bundle | 206 Partial | Addressables BK scene bundle reachable |

## Protocol details (validated on Linda_Leaks_CHOF_720p.mp4)

1. First hop: UCD client API answers `307` with a signed redirect to `*.cloudcontent.unity3dusercontent.com`; CORS `Access-Control-Allow-Origin` echoes the requesting origin, `Vary: Origin` present. Redirect link expires (~5 min), so URLs must not be cached client-side long-term — Unity VideoPlayer re-resolves per load, which is correct.
2. Final hop: `206 Partial Content`, `Content-Type: video/mp4`, `Accept-Ranges: bytes`, correct `Content-Range`. Byte-range streaming works, which is what the browser `<video>` element (WebGL VideoPlayer) requires for seek/progressive playback.
3. CORS is present on **both** hops with the origin echoed — WebGL playback from any origin passes preflight-less media fetches.

## On-demand loading and release

- Video URLs resolve lazily at interaction time (`ResolveMediaUrl` called by the pop-up controllers), not at scene load.
- Empty-URL guard: `ResolveMediaUrl` returns `string.Empty` and controllers skip playback if a file is unconfigured (guards added in a previous pass and still present in `MediaVideoController.cs` / `QuiltVideoPopUp.cs` / `LindaLeaksVideoPopUp.cs`).

## Findings / recommendations (no changes required for correctness)

1. **PASS** — all URLs valid, correct MIME, range support, CORS OK, no local duplication.
2. `Cache-Control: private, max-age=0` on the final content host: repeat views re-download full videos. If bandwidth cost or replay latency matters, front UCD with a caching CDN or use the existing Cloud Run service. Not a correctness issue.
3. The BK Addressables bundle hash will change after this pass's scene edits. **Before the next deployment**: rebuild Addressables for WebGL (`Build > New Build > Default Build Script` or the existing CI step) and upload the new `ServerData/WebGL` bundle to the UCD bucket, then re-release the badge. Until then, the currently deployed player keeps using the current (old) bundle and is unaffected.
