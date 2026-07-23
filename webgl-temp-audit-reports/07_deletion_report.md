# Stage 7/8 — Deletion Analysis, Execution, Validation
Date: 2026-07-23
Full manifests: baseline/deletion_manifest.tsv (per-file evidence), baseline/retained_reclassified.tsv, baseline/trash_units.txt, baseline/deletion_log.txt

## Deletion criteria verification (per prompt, all 8 conditions)
Every deleted asset satisfied ALL of:
1. **Not reachable from the production dependency graph** — absent from the AssetDatabase.GetDependencies closure of every discovered root (Stage 4, tags = "-").
2. **Not referenced by project settings** — GUID scan of ProjectSettings/*.asset found no references; not a Graphics/Quality/Input/XR config object.
3. **Not referenced by runtime loading** — project has no Resources.Load/AssetBundle/UnityWebRequest loads; scene-name and video-file string loads resolve only to retained assets (verified by code sweep).
4. **Not referenced by serialized assets** — GUID scan of every kept .unity/.prefab/.asset/.mat/etc. and every kept .meta (importer remaps): 0 hits for deleted files (2 candidates that hit were retained).
5. **Not referenced through string-based loading** — `"Assets/…"` string-literal scan of all .cs files; all folder prefixes named by tooling were retained wholesale.
6. **Not required by platform-specific configuration** — XR (Android) settings tree, WebGL template, quality/pipeline assets untouched; deleted assets appear in no platform config.
7. **Not required by production tooling** — editor scripts' referenced paths/folders retained (BlackKitchen, PrivacyLawExhibit, LindaLeaks, Meshell articles/Pages, RealBlend palettes, BakedVertexPaintMeshes); .cs/.asmdef/editor folders never deleted.
8. **Not required to produce the current production build** — every deleted file was Category C: absent from the baseline BuildReport packedAssets and from the build output; the rebuild (Stage 9/10) empirically confirms.

Recoverable copy confirmed before deletion: all items moved to macOS Trash (no permanent erase); 4,669 of 4,674 also recoverable from git history (5 exceptions are third-party `.unitypackage` archives not tracked by git — listed in deletion_manifest.tsv gitTracked=no).

## What was deleted (measured)
- **4,674 asset files, 4,880.4 MB source**, plus their .meta files → 6,376 filesystem items moved to Trash as 3,188 units (272 whole folders + 2,916 files). Zero move errors (deletion_log.txt).
- 100% Category C (in project, excluded from the production build). Net effect on build content: none expected (verified in Stage 10/12).

By top-level folder (files / source MB): Furniture Mega Pack 1,428/1,634.2 · TerrainSampleAssets 186/1,394.5 · DevDen Arch Viz Scotland 532/532.1 · Idyllic Italian Coast Town 384/183.7 · SubstanceAssets 217/177.8 · RealBlend 31/177.2 · BCaT_assets 4/147.5 · YughuesFreeCobbleMaterials 59/140.1 · Shaded Spectrum 16/117.8 · HyTeKGames 577/98.7 · _Recovery 25/97.8 · Animated Tropical Vegetation 226/52.1 · My_Custom 15/37.5 · danthaigames 4/37.1 · Coconut Palm Tree Pack 85/34.3 · Pandazole 291/5.9 · UnityTechnologies 107/3.9 · SimpleNaturePack 51/2.7 · Food Pack-Demo 232/2.5 · LowPolyLivingRoomPack 139/2.2 · BrokenVector 41/0.6 · misc root files (Tree.prefab, Tree_Textures, NewLayer 2–4.terrainlayer, Readme.asset, MeshellNotebookInteractionProxy.obj, BH_XR_MainScene/LightingData.asset stale bake) · Emilulz/TutorialInfo/IgniteCoders demo leftovers.

The only BCaT-authored deletions were 4 video files that are staging duplicates of identically-named files served from StreamingAssets in production:
- BCaT_assets/Ri/and that is the truth - you know what I'm meaning_720p.mp4 (124.8 MB)
- BCaT_assets/Ri/you don't know about style my darling_720p.mp4 (14.9 MB)
- BCaT_assets/Ri/such lovely gravy_720p.mp4 (7.5 MB)
- BCaT_assets/Meshell_Sturgis/subjected_to_recognition_720p.mp4 (0.2 MB)
(All four exist in Assets/StreamingAssets and are in git LFS.)

## What was retained despite being Category C (38 assets, reclassified D)
- 2 crossref hits (RealBlend preview shader path-string; TutorialInfo icon referenced by a kept .meta).
- 19 under folder paths that production editor tooling references (BlackKitchen exhibit workspace incl. the 77 MB REFERENCE_ONLY point cloud, PrivacyLawExhibit textures, entire LindaLeaks folder incl. its 34 MB source video, RealBlend VertexColorPalettes).
- 17 by policy: project-authored BCaT content with insufficient evidence of abandonment (ExhibitCanvases template prefabs used by the exhibit-standardization workflow, Meshell article source PDFs whose titles appear in tooling strings, My Grandma's Garden.heif source of a tooling-referenced PNG, Ri/360_images, BlackParlors picture-frame source, BFM_Chest_OnChair.prefab).

## Stage 8 validation (measured, automated)
- Unity batch refresh + compile after deletion: **0 compile errors**.
- All 3 production scenes opened; missing-script scan: **BH_XR_MainScene 2,860 GameObjects / 0 missing; BlackKitchen_MemoryScene 111 / 0; LoadingScene 2 / 0**.
- Import log: no missing-reference / could-not-be-found / broken-text errors.
- Resources folders and StreamingAssets untouched (verified present).
- **0 assets restored** — no deletion caused a failure.
- Not performed (cannot be automated headlessly): manual gameplay traversal of exhibits in-editor. Runtime behavior is validated via the browser harness in Stages 6/11.

## Project storage after deletion (measured)
- Assets/: 6.7 GB → **2.1 GB**; non-meta files 5,867 → **1,191** (see 12_comparison for exact numbers).
