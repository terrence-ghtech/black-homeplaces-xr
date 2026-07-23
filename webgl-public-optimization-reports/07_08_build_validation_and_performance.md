# Stage 7/8 — Optimized Build, Validation & Performance Testing
Date: 2026-07-23
Build: webgl-public-optimized/ (Unity 6000.4.5f1, batch, Brotli, IL2CPP — identical player settings to production)
Remote bundles: ServerData/WebGL/ (also copied to webgl-public-optimized/addressables/WebGL for local serving)

## Build results (measured)
- Attempt 1: succeeded but the Black Kitchen bundle built to the LOCAL path (schema kept Local IDs after `SetVariableByName`) — rejected, schema asset patched to the Remote profile IDs, rebuilt (attempt 2 of 2). Both attempts' logs preserved.
- Final build: **Succeeded, 0 errors, 9 warnings** (same benign shader `pow()` warnings as baseline), 15.7 min.

| Artifact | Baseline (production) | Optimized | Δ |
|---|---|---|---|
| .data.br | 705.8 MB | **282.5 MB** | −60.0% |
| .wasm.br | 9.48 MB | 9.78 MB (+Addressables runtime) | +3% |
| framework.js.br | 85,745 B | 85,911 B | ≈ |
| Initial Unity payload (data+wasm+js) | 715.4 MB | **292.4 MB** | −59% |
| Black Kitchen | inside .data.br | **separate 42.3 MB remote bundle** (LZ4) | out of startup |
| StreamingAssets videos | 196 MiB (unchanged, pending CDN) | 196 MiB | CDN step pending |
| Uncompressed data payload | 2,153.5 MB | 848.6 MB | −61% |

## Validation matrix (each item: measured result)
| Check | Result |
|---|---|
| index loads | ✅ 200, page boots |
| Brotli files correct | ✅ Content-Encoding: br on .br files; decoded by Chrome |
| Missing scripts | ✅ 0 (deep scene scan) |
| Missing meshes | ✅ 0 |
| Null/missing materials | ✅ only the 5 pre-existing Pen.FBX slots (broken since initial import, present in production) |
| Console exceptions at boot/idle | ✅ 0 (cold, warm, throttled, and no-remote runs) |
| Main house loads | ✅ Unity init + loading bar completes, 60 FPS at spawn |
| Black Kitchen bundle served | ✅ 200, 42,333,520 B via local server path |
| Addressables progress UI | ✅ implemented in LoadingSceneController (PercentComplete); **runtime portal walk not automatable — needs manual QA** |
| Return transition + spawn | code-preserved (SceneTransitionState unchanged); **needs manual QA** |
| Videos | unchanged StreamingAssets URLs by default (remote config empty) — behavior identical to production |
| Refresh works | ✅ warm-cache reload: interactive 1.1 s |
| Cached second visit | ✅ 0 bytes re-transferred |
| Boot with remote content unavailable | ✅ loads cleanly (5.0 s, 0 exceptions; catalog is only requested at portal use) |
| Desktop controls / interactions / audio in-game | **not automatable headlessly — manual QA required** |

## Performance measurements (Chrome 150 headless, local server; SwiftShader GPU)
| Scenario | Time to interactive | Transferred | FPS (spawn) | Notes |
|---|---|---|---|---|
| Cold cache, unthrottled | 3.9 s | 292.4 MB | — | disk-speed bound |
| Warm cache | **1.1 s** | 0.0 MB | 60.1 | full HTTP cache hit |
| Cold cache, 25 Mbps / 40 ms RTT | **97.9 s** | 292.4 MB | 60.1 | bandwidth-bound (theoretical floor ≈ 94 s) |
| Baseline (production build), same harness | 10.0 s unthrottled | ~716 MB | — | for comparison; at 25 Mbps would be ≈ 4 min (computed, not measured) |

- Frame time p95 at spawn: ~16.7 ms (60 Hz-locked); no sustained drops observed at spawn viewpoint. Full-house walkthrough FPS not automatable — manual QA item.
- Peak browser renderer memory (isolated runs, headless SwiftShader → textures reside in CPU RAM): baseline **3,008 MB** → optimized **2,785 MB**. On real GPUs the improvement is larger: GPU texture payload fell 1,960.7 → 694.6 MB (measured packed sizes) and CPU-side texture copies were eliminated (Read/Write off). Label: RSS numbers are environment-specific approximations; packed-size deltas are exact.
- JS heap (excludes WASM/AssetBuffer): ~10–18 MB — not the relevant metric; included for completeness.
- Browsers: Chrome ✅. Edge: not installed on this machine. Safari: skipped — it does not accept Brotli content-encoding over plain HTTP (HTTPS-only), which the local test server cannot provide; test post-deploy on the HTTPS host.
- Video-start delay: not measured (requires in-world interaction; manual QA item).

## Interpretation vs acceptance targets (Stage 9)
- Initial compressed payload below 300 MB: **✅ 292.4 MB** (was 715.4 MB). Below 200 MB: ❌ not reached without reducing BCaT documentary photograph textures (largest remaining pool, 432.5 MB packed) and/or mesh decimation — both deferred pending visual QA by design.
- No 706 MB monolithic .data.br: ✅ (282.5 MB)
- Black Kitchen separated from startup: ✅ (42.3 MB remote bundle, downloads at portal)
- Videos excluded from initial Unity deployment: ✅ architecture complete; local files intentionally retained until CDN URLs are supplied and verified (per instructions)
- No browser OOM / WebGL exceptions: ✅ in all automated runs
- Memory growth across transitions: code-audited (see 06); runtime portal-cycle measurement requires manual QA
- Clear loading progress for remote content: ✅ implemented (percent text in LoadingScene)
