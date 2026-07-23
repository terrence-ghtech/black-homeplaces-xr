# Stage 0 — Protection & Baseline Measurements
Date: 2026-07-23
Workstream branch: `public-webgl-optimization` (created from `main` @ a8f0669; baseline commit 4d78955)

## Git protection (measured)
- Branch `public-webgl-optimization` created; full current project state committed (4d78955), including the audit workstream's evidence-based removal of 4,674 build-unreferenced assets (all recoverable: macOS Trash + git history; 5 untracked third-party .unitypackage archives Trash-only).
- Rollback: `git checkout main` restores the pre-audit tree; `git checkout public-webgl-optimization` restores this baseline. Per-asset originals of GLBs modified in Stage 2/3 are additionally kept in `GLB_Originals_Backup/` (outside Assets, gitignored — originals also live in git LFS history).
- Known-good builds preserved and never overwritten by this workstream:
  - `webgl-phase9/` — validated production build (untouched)
  - `webgl-temp-audit-reports/baseline_build_artifacts/` — byte-identical audit baseline artifacts + SHA-256 manifest
  - Audit reports in `webgl-temp-audit-reports/` (read-only from here on)
  - New builds go to `webgl-public-optimized/` only.

## Current production build measurements (measured, from audit baseline build 2026-07-23, Unity 6000.4.5f1)
| Metric | Value |
|---|---|
| .data.br (Brotli) | 705,833,122 B (~706 MB) |
| .wasm.br | 9,481,388 B (9.5 MB) |
| framework.js.br | 85,745 B |
| loader.js | 26,982 B |
| StreamingAssets (6 mp4) | 205,033,081 B (~196 MiB) |
| Total deployment folder | ~894 MiB |
| Build duration | 1,694.8 s (28.2 min) |
| Uncompressed .data payload | 2,153.5 MB (BuildReport packedAssets) |

## WebGL memory settings (measured, ProjectSettings)
initial 532 MB → max 2,048 MB, geometric growth (mode 2, step 0.2, cap 96 MB); threads off; exception support = explicitly-thrown; data caching on; Brotli, no decompression fallback; IL2CPP, stripping Medium.

## Scene list (enabled, in build)
1. Assets/BH_XR_MainScene.unity (sharedassets0: 1,806.4 MB uncompressed)
2. Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity (sharedassets1: 299.3 MB)
3. Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity (sharedassets2: 0.04 MB)

## Packed memory by type (measured, uncompressed in .data)
- **Texture2D: 1,960.7 MB (91%)** · Mesh: 135.7 MB · AudioClip: 37.5 MB · Shader: 15.5 MB · other < 4 MB
- ⇒ Texture payload is the dominant optimization lever; mesh payload is comparatively small.

## Largest packed assets (measured, uncompressed)
| MB | Asset |
|---|---|
| 299.3 | BCaT/Exhibits/BlackKitchen/Models/BlackKitchen_ScannedEnvironment.glb |
| 172.8 | My_Custom/modern_scandinavian_kitchen_island.glb |
| 168.0 | BCaT_assets/SewingRoom/bed.glb |
| 146.0 | BCaT_assets/BTMMP_Workstation_Assembly/flowerbarrel_with_tulips.glb |
| 56.3 | BCaT_assets/SewingRoom/old_sewing_box.glb |
| 27.3 | My_Custom/japanese_red_bridge.glb |
| 24.8 | BCaT_assets/Ri/glass_fish.glb |
| 22.4 | SubstanceAssets/.../GamePieces_Normal_OpenGL.png (+3 siblings ≈ 56 MB total) |
| 21.0 | BCaT_assets/9night/drum.glb |
| 19.8 | BCaT_assets/LindaLeaks/Models/LL_AntiqueCamera.glb |
| 17.2 | BCaT_assets/BTMMP_Workstation_Assembly/metal_table_asset.glb |
| 17.1 | My_Custom/zedah_prevalent_52_inch_luxury_fan.glb |

## Context from the interrupted audit rebuild (documented)
The audit's post-deletion rebuild was terminated externally (SIGTERM, exit 143) during its Brotli phase when this workstream was initiated; an earlier attempt crashed in Unity's build-report finalization (SIGSEGV in the SourceAssetDB LMDB read; resolved by clearing the stale `Library/SourceAssetDB-lock`). The deletions were verified build-neutral by construction (Category C: absent from BuildReport packedAssets; zero GUID/path references from kept files) and by Stage 8 validation (0 compile errors, 0 missing scripts across all 3 production scenes). The Stage 7 optimized build of this workstream will serve as the end-to-end proof of project health.

## Environment/tooling for this workstream (measured)
- Network available (packages.unity.com, registry.npmjs.org reachable) → Addressables install and gltf-transform usable.
- sips available; Blender NOT installed.
- Chrome 150 headless + CDP harness (webgl-temp-audit-reports/browser_validate.mjs) operational; baseline browser run: loaded in 10.0 s locally, 0 errors.
