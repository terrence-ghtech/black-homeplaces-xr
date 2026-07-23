# BCaT WebGL — Public Desktop Readiness Report
Date: 2026-07-23 · Branch: `public-webgl-optimization` · Unity 6000.4.5f1
Supporting reports: 00_stage0_baseline, 02_texture_optimization_results, 03_mesh_material_findings, 04_remote_video_manifest, 06_memory_lifecycle_audit, 07_08_build_validation_and_performance (this folder); full audit evidence in `webgl-temp-audit-reports/`.

---

## 1. Executive decision: **READY AS PUBLIC BETA** (desktop)

Not yet unconditional public release, for exactly three reasons, all bounded:
1. **Manual visual/interaction QA has not been performed** (this environment cannot render or play the build): a human pass through the house, the two resized hero GLBs (Black Kitchen scan, kitchen island), the Black Kitchen portal round-trip, and each video exhibit is required.
2. **Videos still ship in the deployment** (196 MiB) until CDN URLs are supplied — architecture is complete and centralized; flipping to remote requires editing one asset.
3. Initial payload is **292 MB** — under the 300 MB target but above the preferred 200 MB; reaching ~200 MB requires reducing BCaT documentary photographs and/or mesh decimation, both intentionally deferred pending visual QA.

## 2. Before / after (Measured unless noted)
| Metric | Before (production baseline) | After (webgl-public-optimized) |
|---|---|---|
| Initial .data.br | 705.8 MB | **282.5 MB (−60%)** |
| Initial Unity payload (data+wasm+js) | 715.4 MB | **292.4 MB** |
| Total base deployment (with videos) | ~894 MiB | ~519 MiB |
| StreamingAssets (videos) | 196 MiB | 196 MiB (CDN step pending, one-asset switch) |
| Remote Addressable bundles | — | Black Kitchen: **42.3 MB** (LZ4, downloads at portal) |
| Uncompressed data payload | 2,153.5 MB | 848.6 MB |
| Texture memory (packed, exact) | 1,960.7 MB | **694.6 MB** |
| Mesh memory (packed) | 135.7 MB | 104.9 MB |
| Largest asset in payload | BK scan 299.3 MB | bed.glb 33.7 MB |
| Peak renderer RSS (headless SwiftShader; approximate, env-specific) | 3,008 MB | 2,785 MB |
| Time to interactive, 25 Mbps cold | ≈4 min (computed) | **97.9 s (measured)** |
| Time to interactive, warm cache | not measured | **1.1 s** |
| FPS at spawn (headless) | — | 60 (locked, p95 16.7 ms) |
| Build duration | 28.2 min | 15.7 min |

## 3. Modified assets & settings (authoritative lists in git)
Commits on `public-webgl-optimization`: `4d78955` (state preservation incl. prior audit deletions), `ceb64c7` (optimization), `<final commit>` (schema fix + reports).
- 20 GLB importer .metas: `_textureCompression None→Normal`, `_texturesReadWriteEnabled 1→0` (list: 02 report).
- 2 GLB binaries re-encoded (same path/GUID; originals in `GLB_Originals_Backup/` + git LFS history): BlackKitchen_ScannedEnvironment.glb (textures ≤4096), modern_scandinavian_kitchen_island.glb (≤2048).
- 147 texture .metas: WebGL-only platform override max 1024 (full list: `webgl_texture_overrides.tsv`).
- Scripts changed: LoadingSceneController (Addressables path+progress+failure), MediaVideoController / QuiltVideoPopUp / LindaLeaksVideoPopUp (remote URLs, loading/error states, single-active coordination, release-on-end), RuntimeMediaPaths (+ResolveMediaUrl).
- Scripts added: RemoteMediaConfig.cs, VideoExhibitCoordinator.cs (+VideoLoadingIndicator), AddressableSceneHandleStore.cs; Editor tools BCATOptimizationTool.cs, BCATAddressablesSetup.cs (audit era: BCATWebGLAuditTool.cs).
- Assets added: Assets/Resources/RemoteMediaConfig.asset; Assets/AddressableAssetsData/* (settings, BlackKitchen_Remote group).
- Settings: Packages/manifest.json +com.unity.addressables 2.9.1; EditorBuildSettings — BlackKitchen scene **disabled** (not removed); .gitignore build-output entries. No PlayerSettings/quality/pipeline changes. No scene-object moves or rescales.
- Pre-existing uncommitted edits present at session start (LindaLeaks prefab/scripts .metas, LoadingScene.unity, various .meta touches) were preserved verbatim in `4d78955` — not authored by this workstream.

## 4. Assets moved to Addressables
Exactly one entry: `Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity` → group **BlackKitchen_Remote** (address `BlackKitchen_MemoryScene`, PackTogether, LZ4, remote paths) → bundle `blackkitchen_remote_scenes_all_592ada671b682ead7e0ffa10068a3de5.bundle` (42,333,520 B) + two small auto-generated local bundles (builtin assets, monoscripts) that correctly stay in the build. Shared-asset duplication: BK scene's heavy content (the scan) is exclusive to it; URP shaders/TMP remain in the base build — no meaningful duplication (Inferred from packed lists; not exhaustively verified with the Addressables analyze tool).

## 5. Video URL mapping (single config: Assets/Resources/RemoteMediaConfig.asset)
`remoteBaseUrl` = _empty (placeholder)_. Per-file entries (all awaiting CDN URL): Linda_Leaks_CHOF_720p.mp4 · and that is the truth - you know what I'm meaning_720p.mp4 · in_my_sisters_room_xr.mp4 · subjected_to_recognition_720p.mp4 · such lovely gravy_720p.mp4 · you don't know about style my darling_720p.mp4. Upload manifest with sizes: 04 report.

## 6. Remaining CDN steps
1. Upload the 6 videos (04 report) → set `remoteBaseUrl` (or per-file URLs) in RemoteMediaConfig.asset. CDN must support HTTPS, Range requests, video/mp4, CORS.
2. Upload `ServerData/WebGL/` bundle → set Addressables profile `Remote.LoadPath` (currently `http://127.0.0.1:8090/addressables/[BuildTarget]` for local testing) → rebuild Addressables + player.
3. Host the build folder with Brotli headers (as tools/serve_webgl.py does; `Content-Encoding: br`, correct MIME).
4. After video URLs verified in a staging pass: move the 6 .mp4 (+.meta) out of StreamingAssets and rebuild for the video-less release (drops deployment ~196 MiB).

## 7. Rollback
- Everything pre-optimization: `git checkout main` (audit deletions included there? **No** — main predates the audit deletions; the audit-deleted files are in macOS Trash and in main's history).
- Pre-optimization but post-audit state: `git checkout 4d78955`.
- Individual GLBs: restore from `GLB_Originals_Backup/` (or git LFS at `4d78955^`).
- Texture caps: revert the .meta changes (`git checkout 4d78955 -- <path>.meta`).
- Re-enable Black Kitchen as built-in: re-enable the scene in Build Settings — LoadingSceneController automatically uses SceneManager again (also the Quest-build path).
- Builds: `webgl-phase9/` (production) and `webgl-temp-audit-reports/baseline_build_artifacts/` (baseline + SHA-256) were never modified.

## 8. Known risks
- **Visual quality of the two resized GLBs** — Unknown until human review (4096 room atlas, 2048 island). DXT compression itself is the standard Unity import path and already live on 4 previously-remediated GLBs.
- 1024 caps on third-party pack textures may be noticeable on close inspection of some furniture/terrain — Unknown until walkthrough; Quest unaffected (WebGL-only overrides).
- Quest builds now require either re-enabling the BK scene in Build Settings or building Android Addressables — documented, one checkbox.
- Addressables 2.9.1 newly added: only the BK scene uses it; base app boots with remote content unavailable (measured).
- Pre-existing defect (not introduced): DevDen Pen.FBX references a missing material (5 slots) since initial import.
- Headless memory numbers are SwiftShader-specific; real-GPU behavior expected better but Unknown until manual testing.

## 9. Testing performed (exact)
Automated, this environment: batch scene validation (missing scripts/meshes/material slots, 3 scenes); BuildReport diffs (packed assets by type/folder/asset); Chrome 150 headless via CDP against local Brotli server — cold unthrottled, warm (refresh/cached revisit), cold @25 Mbps/40 ms, boot-without-remote-content; network/console/exception capture on every run; FPS + frame-time p95 at spawn; isolated peak-RSS comparison baseline vs optimized; Black Kitchen bundle HTTP servability. NOT performed (needs human): in-world walkthrough, portal round-trip, exhibit interactions, video playback QA, Safari/Edge, real-GPU profiling.

## 10. Measured / Inferred / Unknown — headline conclusions
- Payload −60%, texture payload −65%, BK separated (42.3 MB), warm reload 1.1 s, throttled cold 97.9 s, zero exceptions: **Measured**.
- Visitor-perceived load ≈ 1.5–2 min on ~25 Mbps connections: **Inferred** (bandwidth-bound).
- No visible quality regression: **Unknown** (pending human QA) — highest-confidence risk items listed above.
- Memory-leak-free transitions: code-verified (**Measured** at code level), runtime cycling **Unknown** pending manual QA.

## 11. Deployment checklist
☐ Manual QA pass (walkthrough, portal round-trip ×2, every video exhibit, resized-GLB close inspection)
☐ Upload 6 videos to CDN → fill RemoteMediaConfig → staging verify → remove local videos → rebuild
☐ Upload ServerData/WebGL → set Remote.LoadPath to CDN → rebuild Addressables + player
☐ Serve with Brotli + Range + CORS headers (HTTPS)
☐ Safari + Edge + integrated-GPU spot checks on the HTTPS staging URL
☐ Re-run the CDP validation harness against staging (scripts in this folder)
☐ Tag the release commit; keep `webgl-phase9/` as rollback
