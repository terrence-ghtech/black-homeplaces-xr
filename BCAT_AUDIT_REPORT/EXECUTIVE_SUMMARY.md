# Executive Summary

Audit date: generated from local filesystem evidence. This is read-only; no Unity assets were modified by this audit generator.

## Direct Answers

- **Why is `webgl.data.br` 864 MB?** Exact packed attribution requires `LastBuild.buildreport`, but that file was not present at `/mnt/data/LastBuild.buildreport`. Static evidence shows the project contains very large source textures/models/audio plus build scenes that reference broad exhibit content. `webgl.data.br` is the packed Unity data file and is likely dominated by imported textures, meshes/models, audio clips, scenes, and any non-StreamingAssets video imported as VideoClip.
- **How much of the built WebGL folder is video?** Built `webgl/StreamingAssets` video totals about **248.3 MB**. Source video under `Assets` totals **421.5 MB**.
- **Are videos duplicated between Unity packed data and StreamingAssets?** Critical duplicate inclusion is possible wherever a video exists in `Assets/StreamingAssets` and is also referenced/imported as a VideoClip elsewhere. The static scan flags these in `VIDEO_AUDIT.csv`; exact confirmation inside `webgl.data.br` needs a build report with packed asset entries.
- **How much space is consumed by textures?** Source image/texture files under `Assets` total **5.3 GB** before Unity import compression/packing.
- **How much is consumed by meshes/models?** Source model/mesh files under `Assets` total **528.8 MB** before Unity import processing.
- **How much is consumed by audio?** Source audio files under `Assets` total **252.6 MB**.
- **How much is unused or indirectly included?** `Resources`/`StreamingAssets`/static ghost candidates are listed in `RESOURCES_STREAMING_ADDRESSABLES.csv` and `GHOST_ASSET_CANDIDATES.csv`. Exact safe deletion requires Unity dependency validation.

## Current Build Outputs

- Entire `webgl` folder: **1.1 GB**
- `webgl/Build/webgl.data.br`: **858.7 MB**
- `webgl/StreamingAssets`: **248.3 MB**

## Largest Built StreamingAssets Videos

- `webgl/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4` — 119.0 MB
- `webgl/StreamingAssets/in_my_sisters_room_xr.mp4` — 75.1 MB
- `webgl/StreamingAssets/Linda_Leaks_CHOF_720p.mp4` — 32.5 MB
- `webgl/StreamingAssets/you don't know about style my darling_720p.mp4` — 14.2 MB
- `webgl/StreamingAssets/such lovely gravy_720p.mp4` — 7.2 MB
- `webgl/StreamingAssets/subjected_to_recognition_720p.mp4` — 225.3 KB

## Five Highest-Impact Changes

1. **P0: Eliminate duplicate video inclusion**. Keep exhibit videos either as StreamingAssets/URL runtime files or imported VideoClips, not both. Conservative savings: 50 MB; likely: 150-250 MB; max plausible: 300+ MB.
2. **P1: Move large long-form videos to remote hosting/CDN for WebGL**. Conservative: 119 MB; likely: 200-248 MB; max: full StreamingAssets video payload. Runtime-loading risk; requires network/offline policy.
3. **P1/P2: Downscale/compress large document/page textures** such as full-page PNG article images and plaques. Conservative: 50 MB; likely: 150-300 MB; max depends on page count. Requires visual QA.
4. **P1/P2: Optimize scanned/photogrammetry models and embedded textures**, especially Black Kitchen and package scans. Conservative: 25 MB; likely: 100+ MB; max requires mesh/texture replacement. Requires visual/physics QA.
5. **P2: Remove unused package demo/sample content from build dependencies and scenes** after dependency proof. Conservative: 10 MB; likely: 50+ MB; max higher if unused prefabs pull dependencies into scenes.

## Estimated Size By Phase

- Phase 0 current: `webgl` about **1.1 GB**.
- Phase 1 video duplication/StreamingAssets policy: likely **919.7 MB**.
- Phase 2 texture/document compression: likely **769.7 MB**.
- Phase 3 model/audio/package cleanup: likely **669.7 MB**.

## Risk Summary

- Low risk: remove duplicate raw files only after Unity dependency proof; enable future WebGL build-size analysis; externalize videos using existing URL-capable scripts if already supported.
- Visual QA required: texture downscaling, material/shader simplification, scanned model decimation.
- Runtime-loading risk: changing StreamingAssets paths, Resources loading, video URL behavior, or scripts that load by string filename.
- Unity Play readiness after optimization: not ready until a Play Mode route test covers every exhibit/media path affected.

## Git And Filesystem Findings

See `FILESYSTEM_GIT_AUDIT.csv` for the largest 100 project files and largest 100 Git-tracked files. Notable large tracked files include duplicate StreamingAssets/exhibit videos, long WAVs, reference PLY/model files, and package textures.

Largest filesystem files:

- `quest.apk` — 861.1 MB
- `webgl/Build/webgl.data.br` — 858.7 MB
- `app-build-0.1.0-v1-IL2CPP.symbols.zip` — 260.4 MB
- `app-build.apk` — 159.1 MB
- `webgl/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4` — 119.0 MB
- `Assets/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4` — 119.0 MB
- `Assets/BCaT_assets/Ri/and that is the truth - you know what I'm meaning_720p.mp4` — 119.0 MB
- `Assets/BCaT_assets/9night/9night-soundscape.wav` — 95.4 MB
- `webgl/StreamingAssets/in_my_sisters_room_xr.mp4` — 75.1 MB
- `Assets/StreamingAssets/in_my_sisters_room_xr.mp4` — 75.1 MB

Largest Git-tracked files:

- `Assets/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4` — 119.0 MB
- `Assets/BCaT_assets/Ri/and that is the truth - you know what I'm meaning_720p.mp4` — 119.0 MB
- `Assets/BCaT_assets/9night/9night-soundscape.wav` — 95.4 MB
- `Assets/StreamingAssets/in_my_sisters_room_xr.mp4` — 75.1 MB
- `Assets/BCaT/Exhibits/BlackKitchen/Models/Reference/point_cloud_scan_REFERENCE_ONLY_DO_NOT_BUILD.ply` — 73.4 MB
- `Assets/SubstanceAssets/LittleGamesPack/Textures/ChessPieces/ChessPieces_Normal_OpenGL.png` — 66.5 MB
- `Assets/BCaT_assets/LindaLeaks/Linda_Leaks_CHOF_720p_audio.wav` — 53.4 MB
- `Assets/DevDen Arch Viz Scotland/Scenes/ArchViz/ReflectionProbe-3.exr` — 47.4 MB
- `Assets/SubstanceAssets/LittleGamesPack/Textures/GamePieces/GamePieces_Normal_OpenGL.png` — 44.5 MB
- `Assets/DevDen Arch Viz Scotland/HDRI/HDRI.png` — 42.7 MB
