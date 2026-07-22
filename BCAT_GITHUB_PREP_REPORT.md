# BCaT GitHub Preparation Dry-Run Report

Project root: `/Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST`

## Executive Summary

This dry run used Git-aware commands without staging files. The prospective commit set was computed with:

```sh
git ls-files -z --cached --others --exclude-standard
```

Technical large-file handling is correctly configured: every prospective committed file over 25 MiB resolves to `filter=lfs`. Generated build outputs, root APKs, WebGL output, Unity `Library`, symbol ZIPs, and backup-build folders are ignored and are not in the prospective commit set.

The minimum technical blockers for the initial private backup have been resolved. The six `.meta` files associated with intentionally ignored generated/package artifacts are now ignored by exact path. Empty directories, existing folder `.meta` files, and stale local paths inside `Assets/_Recovery` are accepted non-blockers for this initial private backup.

## Initial Backup Decisions

- `Assets/_Recovery` is intentionally retained as noncanonical recovery material.
- The seven stale local VideoPlayer URLs are accepted because they occur only in legacy recovery scenes.
- Empty directories are not preserved by Git.
- No placeholder files were added.
- Existing folder `.meta` files remain untouched for this initial backup.
- Empty-folder behavior will be validated during the clean-clone test.
- Licensing and privacy will be controlled by keeping the GitHub repository private with no collaborators initially.
- This is a technical backup decision and not a determination of redistribution rights.

## Existing Git Configuration

- Git is initialized.
- Current branch: `main`
- No remote is configured.
- No files are staged.
- Git LFS is installed locally.
- Root `.gitattributes` contains LFS rules and Unity/source text rules.
- Package-level `Packages/org.khronos.unitygltf/.gitattributes` remains present and unchanged.

Command results:

```text
git branch --show-current
main
```

```text
git remote -v
<no output>
```

```text
git lfs status

Objects to be committed:


Objects not staged for commit:
```

`git status --short` result:

```text
?? .gitattributes
?? .gitignore
?? Assets/
?? BCAT_CANVAS_STANDARDIZATION.md
?? BCAT_GITHUB_MIGRATION_MANIFEST.txt
?? BCAT_GITHUB_PREP_REPORT.md
?? BCAT_IMPLEMENTATION_AUDIT.csv
?? BCAT_IMPLEMENTATION_AUDIT.md
?? BCAT_PROJECT_DEPENDENCY_AUDIT.md
?? BCAT_VIDEO_XR_FIX_PASS2.md
?? ComputeCommandBuffer.cs
?? GITHUB_MIGRATION_AND_ASSET_NOTICE.md
?? IBaseCommandBuffer.cs
?? IComputeCommandBuffer.cs
?? IRasterCommandBuffer.cs
?? IUnsafeCommandBuffer.cs
?? Packages/
?? ProjectSettings/
?? README.md
?? RasterCommandBuffer.cs
?? UnsafeCommandBuffer.cs
?? ignore.conf
?? mono_crash.0.0.json
?? unity-meshell-interaction.log
?? unity-meshell-validation.log
```

## Prospective Commit Statistics

- Total prospective committed files: 13,582
- Total logical size of prospective committed files: 7,214,458,816 bytes, 6.72 GiB
- Estimated normal Git size: 215,252,998 bytes, 205.28 MiB, plus about 439 KiB of LFS pointer files
- Estimated Git LFS size: 6,999,205,818 bytes, 6.52 GiB
- Prospective Git LFS files: 3,208
- Ordinary Git files: 10,374
- Normal Git files larger than 25 MiB: 0
- Normal Git files larger than 50 MiB: 0
- Normal Git files larger than 100 MiB: 0

## Prospective LFS Statistics

`git lfs track` reports these tracked patterns:

```text
*.jpeg, *.m4v, *.mp3, *.glb, *.webm, *.bundle, *.fbx, *.tif, *.png,
*.gltf, *.mp4, *.mov, *.aiff, *.aif, *.tiff, *.ply, *.psd, *.blend,
*.stl, *.jpg, *.avi, *.wav, *.flac, *.obj, *.tga, *.exr, *.hdr,
*.unitypackage, *.assetbundle
```

Representative `git check-attr filter` results:

```text
Assets/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4: filter: lfs
Assets/BCaT_assets/9night/9night-soundscape.wav: filter: lfs
Assets/BCaT/Exhibits/BlackKitchen/Models/Reference/point_cloud_scan_REFERENCE_ONLY_DO_NOT_BUILD.ply: filter: lfs
Assets/SubstanceAssets/LittleGamesPack/Textures/ChessPieces/ChessPieces_Normal_OpenGL.png: filter: lfs
Assets/DevDen Arch Viz Scotland/Scenes/ArchViz/ReflectionProbe-3.exr: filter: lfs
```

## Largest Prospective Files

| Rank | Size | Handling | Path |
|---:|---:|---|---|
| 1 | 119.01 MiB | LFS | `Assets/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4` |
| 2 | 119.01 MiB | LFS | `Assets/BCaT_assets/Ri/and that is the truth - you know what I'm meaning_720p.mp4` |
| 3 | 95.36 MiB | LFS | `Assets/BCaT_assets/9night/9night-soundscape.wav` |
| 4 | 75.14 MiB | LFS | `Assets/StreamingAssets/in_my_sisters_room_xr.mp4` |
| 5 | 73.38 MiB | LFS | `Assets/BCaT/Exhibits/BlackKitchen/Models/Reference/point_cloud_scan_REFERENCE_ONLY_DO_NOT_BUILD.ply` |
| 6 | 66.45 MiB | LFS | `Assets/SubstanceAssets/LittleGamesPack/Textures/ChessPieces/ChessPieces_Normal_OpenGL.png` |
| 7 | 53.38 MiB | LFS | `Assets/BCaT_assets/LindaLeaks/Linda_Leaks_CHOF_720p_audio.wav` |
| 8 | 47.44 MiB | LFS | `Assets/DevDen Arch Viz Scotland/Scenes/ArchViz/ReflectionProbe-3.exr` |
| 9 | 44.54 MiB | LFS | `Assets/SubstanceAssets/LittleGamesPack/Textures/GamePieces/GamePieces_Normal_OpenGL.png` |
| 10 | 42.67 MiB | LFS | `Assets/DevDen Arch Viz Scotland/HDRI/HDRI.png` |
| 11 | 37.93 MiB | LFS | `Assets/DevDen Arch Viz Scotland/Models/Study Room/books.FBX` |
| 12 | 35.07 MiB | LFS | `Assets/My_Custom/japanese_red_bridge.glb` |
| 13 | 35.05 MiB | LFS | `Assets/BCaT_assets/SewingRoom/sewing-sounds.wav` |
| 14 | 34.94 MiB | LFS | `Assets/BCaT_assets/SewingRoom/in_my_sisters_room_audio.wav` |
| 15 | 34.46 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Heather_Normal.tif` |
| 16 | 34.44 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_C_Normal.tif` |
| 17 | 34.43 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_B_Normal.tif` |
| 18 | 34.42 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_A_Normal.tif` |
| 19 | 34.39 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Moss_Normal.tif` |
| 20 | 34.37 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Dry_Normal.tif` |
| 21 | 34.37 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_MaskMap.tif` |
| 22 | 34.33 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Soil_Normal.tif` |
| 23 | 34.23 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Muddy_Normal.tif` |
| 24 | 34.17 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_A_Normal.tif` |
| 25 | 34.07 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Black_Sand_Normal.tif` |
| 26 | 34.02 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_Normal.tif` |
| 27 | 33.87 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Moss_MaskMap.tif` |
| 28 | 33.66 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Black_Sand_MaskMap.tif` |
| 29 | 33.65 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_B_Normal.tif` |
| 30 | 33.62 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Sand_Normal.tif` |
| 31 | 33.60 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Heather_BaseColor.tif` |
| 32 | 33.41 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Tidal_Pools_Normal.tif` |
| 33 | 33.40 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Dry_BaseColor.tif` |
| 34 | 33.32 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Rock_Normal.tif` |
| 35 | 33.25 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Rock_MaskMap.tif` |
| 36 | 33.08 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Snow_Normal.tif` |
| 37 | 32.90 MiB | LFS | `Assets/BCaT/Exhibits/BlackKitchen/Models/BlackKitchen_ScannedEnvironment.glb` |
| 38 | 32.78 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_BaseColor.tif` |
| 39 | 32.65 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_B_BaseColor.tif` |
| 40 | 32.61 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Moss_BaseColor.tif` |
| 41 | 32.61 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Rock_BaseColor.tif` |
| 42 | 32.56 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_A_BaseColor.tif` |
| 43 | 32.55 MiB | LFS | `Assets/StreamingAssets/Linda_Leaks_CHOF_720p.mp4` |
| 44 | 32.55 MiB | LFS | `Assets/BCaT_assets/LindaLeaks/Linda_Leaks_CHOF_720p.mp4` |
| 45 | 32.17 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_C_BaseColor.tif` |
| 46 | 32.16 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Terrain/Muddy_BaseColor.tif` |
| 47 | 32.02 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Heightmaps/Ridge05_Heightmap.tif` |
| 48 | 32.02 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Heightmaps/Ridge06_Heightmap.tif` |
| 49 | 32.02 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Heightmaps/Errosion02_Heightmap.tif` |
| 50 | 32.00 MiB | LFS | `Assets/TerrainSampleAssets/Textures/Heightmaps/Slope04_Heightmap.tiff` |

## Files Over 25 MiB and Their Handling

There are 68 prospective committed files over 25 MiB. All 68 have `filter=lfs` and should be stored in Git LFS. None were flagged as large normal-Git files. None were large files with neither `filter=lfs` nor an ignore rule.

| Size | Filter | LFS | Should Be Ignored? | Path |
|---:|---|---|---|---|
| 119.01 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4` |
| 119.01 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/BCaT_assets/Ri/and that is the truth - you know what I'm meaning_720p.mp4` |
| 95.36 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/BCaT_assets/9night/9night-soundscape.wav` |
| 75.14 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/StreamingAssets/in_my_sisters_room_xr.mp4` |
| 73.38 MiB | lfs | yes | possibly; file is marked reference-only/do-not-build and needs owner decision | `Assets/BCaT/Exhibits/BlackKitchen/Models/Reference/point_cloud_scan_REFERENCE_ONLY_DO_NOT_BUILD.ply` |
| 66.45 MiB | lfs | yes | no technical reason; license review required | `Assets/SubstanceAssets/LittleGamesPack/Textures/ChessPieces/ChessPieces_Normal_OpenGL.png` |
| 53.38 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/BCaT_assets/LindaLeaks/Linda_Leaks_CHOF_720p_audio.wav` |
| 47.44 MiB | lfs | yes | no technical reason; license review required | `Assets/DevDen Arch Viz Scotland/Scenes/ArchViz/ReflectionProbe-3.exr` |
| 44.54 MiB | lfs | yes | no technical reason; license review required | `Assets/SubstanceAssets/LittleGamesPack/Textures/GamePieces/GamePieces_Normal_OpenGL.png` |
| 42.67 MiB | lfs | yes | no technical reason; license review required | `Assets/DevDen Arch Viz Scotland/HDRI/HDRI.png` |
| 37.93 MiB | lfs | yes | no technical reason; license review required | `Assets/DevDen Arch Viz Scotland/Models/Study Room/books.FBX` |
| 35.07 MiB | lfs | yes | no technical reason; asset provenance review required | `Assets/My_Custom/japanese_red_bridge.glb` |
| 35.05 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/BCaT_assets/SewingRoom/sewing-sounds.wav` |
| 34.94 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/BCaT_assets/SewingRoom/in_my_sisters_room_audio.wav` |
| 34.46 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Heather_Normal.tif` |
| 34.44 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_C_Normal.tif` |
| 34.43 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_B_Normal.tif` |
| 34.42 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_A_Normal.tif` |
| 34.39 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Moss_Normal.tif` |
| 34.37 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Dry_Normal.tif` |
| 34.37 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_MaskMap.tif` |
| 34.33 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Soil_Normal.tif` |
| 34.23 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Muddy_Normal.tif` |
| 34.17 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_A_Normal.tif` |
| 34.07 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Black_Sand_Normal.tif` |
| 34.02 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_Normal.tif` |
| 33.87 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Moss_MaskMap.tif` |
| 33.66 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Black_Sand_MaskMap.tif` |
| 33.65 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_B_Normal.tif` |
| 33.62 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Sand_Normal.tif` |
| 33.60 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Heather_BaseColor.tif` |
| 33.41 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Tidal_Pools_Normal.tif` |
| 33.40 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Dry_BaseColor.tif` |
| 33.32 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Rock_Normal.tif` |
| 33.25 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Rock_MaskMap.tif` |
| 33.08 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Snow_Normal.tif` |
| 32.90 MiB | lfs | yes | no technical reason; scanned/provenance review required | `Assets/BCaT/Exhibits/BlackKitchen/Models/BlackKitchen_ScannedEnvironment.glb` |
| 32.78 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_BaseColor.tif` |
| 32.65 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_B_BaseColor.tif` |
| 32.61 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Moss_BaseColor.tif` |
| 32.61 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Rock_BaseColor.tif` |
| 32.56 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_A_BaseColor.tif` |
| 32.55 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/StreamingAssets/Linda_Leaks_CHOF_720p.mp4` |
| 32.55 MiB | lfs | yes | no technical reason; licensing/privacy review required | `Assets/BCaT_assets/LindaLeaks/Linda_Leaks_CHOF_720p.mp4` |
| 32.17 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_C_BaseColor.tif` |
| 32.16 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Muddy_BaseColor.tif` |
| 32.02 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Ridge05_Heightmap.tif` |
| 32.02 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Ridge06_Heightmap.tif` |
| 32.02 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Errosion02_Heightmap.tif` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Slope04_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Slope03_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Slope02_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Slope01_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Mountain11_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Mountain10_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Mountain09_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Hills02_Heightmap.tiff` |
| 32.00 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Hills01_Heightmap.tiff` |
| 31.87 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Grass_Soil_BaseColor.tif` |
| 31.79 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Black_Sand_BaseColor.tif` |
| 31.47 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_A_BaseColor.tif` |
| 30.61 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_B_BaseColor.tif` |
| 30.45 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Tidal_Pools_BaseColor.tif` |
| 28.23 MiB | lfs | yes | no technical reason; provenance review required | `Assets/BCaT_assets/BTMMP_Workstation_Assembly/metal_table_asset.glb` |
| 28.11 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Terrain/Snow_BaseColor.tif` |
| 27.77 MiB | lfs | yes | no technical reason; asset provenance review required | `Assets/My_Custom/rug.glb` |
| 25.18 MiB | lfs | yes | no technical reason; license review required | `Assets/RealBlend/Art/Materials/Textures/rough_plaster_brick_04_diff_2k.png` |
| 25.02 MiB | lfs | yes | no technical reason; license review required | `Assets/TerrainSampleAssets/Textures/Heightmaps/Volcano02_Heightmap.tif` |

## Ignore-Rule Validation

No ignored generated folder would still be included in the prospective commit set.

Prospective-file checks:

```text
Library/: 0 prospective files
Temp/: 0 prospective files
Logs/: 0 prospective files
UserSettings/: 0 prospective files
build/: 0 prospective files
build.app/: 0 prospective files
webgl/: 0 prospective files
app-build.apk: 0 prospective files
quest.apk: 0 prospective files
app-build-0.1.0-v1-IL2CPP.symbols.zip: 0 prospective files
app-build_BackUpThisFolder_ButDontShipItWithYourGame/: 0 prospective files
quest_BackUpThisFolder_ButDontShipItWithYourGame/: 0 prospective files
BCAT_BurstDebugInformation_DoNotShip/: 0 prospective files
Black Homeplaces The XR House_BurstDebugInformation_DoNotShip/: 0 prospective files
```

Representative `git check-ignore -v` results:

```text
.gitignore:5:[Ll]ibrary/	Library
.gitignore:6:[Tt]emp/	Temp/file.tmp
.gitignore:10:[Ll]ogs/	Logs
.gitignore:11:[Uu]ser[Ss]ettings/	UserSettings
.gitignore:54:.vscode/	.vscode
.gitignore:58:.plastic/	.plastic
.gitignore:57:.utmp/	.utmp
.gitignore:61:/build/	build
.gitignore:62:/build.app/	build.app
.gitignore:63:/webgl/	webgl
.gitignore:64:/app-build.apk	app-build.apk
.gitignore:65:/quest.apk	quest.apk
.gitignore:26:*.symbols.zip	app-build-0.1.0-v1-IL2CPP.symbols.zip
.gitignore:72:/*_BackUpThisFolder_ButDontShipItWithYourGame/	app-build_BackUpThisFolder_ButDontShipItWithYourGame
.gitignore:72:/*_BackUpThisFolder_ButDontShipItWithYourGame/	quest_BackUpThisFolder_ButDontShipItWithYourGame
.gitignore:73:/BCAT_BurstDebugInformation_DoNotShip/	BCAT_BurstDebugInformation_DoNotShip
.gitignore:74:/Black Homeplaces The XR House_BurstDebugInformation_DoNotShip/	Black Homeplaces The XR House_BurstDebugInformation_DoNotShip
```

Root APK, WebGL build, symbol ZIP, Unity `Library` file, and backup-build folder inclusion:

- Root APK included: no
- WebGL build included: no
- Symbol ZIP included: no
- Unity `Library` file included: no
- Backup-build folder included: no

## Unity .meta Integrity

- Committed real asset files missing corresponding `.meta` files: 0
- Committed `.meta` files whose real asset is missing: 0
- Committed `.meta` files whose asset is ignored: 0

The following six generated-artifact `.meta` files are intentionally ignored by exact path:

```text
Assets/Animated Tropical Vegetation/HDRP Materials.unitypackage.meta
Assets/RealBlend/URP_Demo_Import_Source.unitypackage.meta
Assets/SimpleNaturePack/SimpleNaturePack_2020.3_HDRP_v1.24.unitypackage.meta
Assets/SimpleNaturePack/SimpleNaturePack_2020.3_URP_v1.24.unitypackage.meta
Assets/YughuesFreeCobbleMaterials/YughuesFreeCobbleMaterials_URP.unitypackage.meta
Packages/org.khronos.unitygltf/Runtime/Plugins/GLTFSerialization/Legacy~/GLTFSerialization.csproj.meta
```

Empty directories Git will not preserve: 58

```text
ProfilerCaptures
Packages/org.khronos.unitygltf/Runtime/Plugins/UnityGLTF/Assets/UnityGLTF/Runtime/Plugins/net35
Assets/XR 5
Assets/XR 2
Assets/XR 3
Assets/Patio Furniture/Scenes
Assets/XR 4
Assets/Scenes
Assets/Materials
Assets/Plugins/WebGL
Assets/XR/Settings 1
Assets/XR/Settings 2
Assets/XR/Settings 5
Assets/XR/Settings 4
Assets/XR/Settings 3
Assets/WebGLTemplates
Assets/XR 1
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/TunnelingVignette
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoSceneAssets/Settings
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoSceneAssets/Prefabs/UI
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoSceneAssets/Prefabs/Climb
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoSceneAssets/Prefabs/Teleport
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoSceneAssets/AffordanceThemes
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoSceneAssets/Audio
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoSceneAssets/Scripts
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Presets
Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Editor/Scripts
Assets/LumiStudio/Painting Tools/Textures
Assets/LumiStudio/Painting Tools/Scenes
Assets/LumiStudio/Painting Tools/Materials
Assets/LumiStudio/Painting Tools/Prefabs
Assets/LumiStudio/Painting Tools/Meshes
Assets/BCaT_assets/BTMMP_Workstation_Assembly/blue-oil-barrel-with-lots-of-spray-paint-streaks/textures
Assets/BCaT_assets/LindaLeaks/Textures
Assets/BCaT_assets/LindaLeaks/Documentation
Assets/StarterAssets/FirstPersonController/Scenes/Playground
Assets/StarterAssets/Mobile/UI
Assets/StarterAssets/Mobile/Prefabs/EventSystem
Assets/StarterAssets/Mobile/Scripts/CanvasInputs
Assets/StarterAssets/Mobile/Scripts/Utilities
Assets/StarterAssets/Environment/Prefabs
Assets/StarterAssets/Environment/Art/Textures
Assets/StarterAssets/Environment/Art/Models
Assets/Scripts/Diagnostics
Assets/BCaT/Exhibits/BlackKitchen/Textures
Assets/BCaT/Exhibits/BlackKitchen/Prefabs
Assets/BCaT/Exhibits/PrivacyLawExhibit/UI
Assets/BCaT/Exhibits/PrivacyLawExhibit/Documentation
Assets/YughuesFreePavementsMaterials/Preview/Sc_Preview
Assets/AssetsStore/Garage_props/Demo
Assets/AssetsStore/Garage_props/Assets/Textures
Assets/AssetsStore/Garage_props/Assets/Materials
Assets/AssetsStore/Garage_props/Assets/Prefabs/Barrels_Red
Assets/AssetsStore/Garage_props/Assets/Prefabs/Barrels_Purple
Assets/AssetsStore/Garage_props/Assets/Prefabs/Barrels_Green
Assets/AssetsStore/Garage_props/Assets/Prefabs/Barrels_Old
Assets/AssetsStore/Garage_props/Assets/Prefabs/Barrels_Blue
Assets/AssetsStore/Garage_props/Assets/Meshes
```

Directories that appear to rely on an empty folder existing:

- 57 of the empty directories have folder `.meta` files in the prospective commit set.
- Git will preserve the `.meta` files but not the empty folders themselves.
- This is an accepted non-blocker for the initial backup; empty-folder behavior will be validated during the clean-clone test.

## Recovery-Scene Findings

- `Assets/_Recovery` prospective files: 50
- Total size: 97,803,606 bytes, 93.27 MiB
- Scene files: 25
- Separate non-scene assets: none detected; contents are recovery scenes and their `.meta` files.
- Stale `file:///Users/terrence/...` paths would be committed: yes, in seven recovery scenes.
- External production dependency check: no GUID references from outside `Assets/_Recovery` to recovery scene GUIDs were found. Based on GUID references, excluding this directory later would not remove a production dependency. This does not prove there is no name/path-based runtime dependency.

Largest recovery files:

| Size | Path |
|---:|---|
| 6.39 MiB | `Assets/_Recovery/0 (17).unity` |
| 6.03 MiB | `Assets/_Recovery/0 (16).unity` |
| 6.03 MiB | `Assets/_Recovery/0 (15).unity` |
| 6.03 MiB | `Assets/_Recovery/0 (14).unity` |
| 5.87 MiB | `Assets/_Recovery/0 (23).unity` |
| 5.76 MiB | `Assets/_Recovery/0 (22).unity` |
| 5.76 MiB | `Assets/_Recovery/0 (21).unity` |
| 5.69 MiB | `Assets/_Recovery/0 (18).unity` |
| 5.02 MiB | `Assets/_Recovery/0 (13).unity` |
| 5.01 MiB | `Assets/_Recovery/0 (11).unity` |

Recovery files containing stale local `file:///Users/terrence/...` paths:

```text
Assets/_Recovery/0 (1).unity:25431
Assets/_Recovery/0 (2).unity:25909
Assets/_Recovery/0 (3).unity:25644
Assets/_Recovery/0 (5).unity:27171
Assets/_Recovery/0 (6).unity:25272
Assets/_Recovery/0 (7).unity:25297
Assets/_Recovery/0.unity:24374
```

## Secret-Scan Findings

Likely-secret scan over prospective committed text files found zero matches.

Patterns checked included API keys, bearer tokens, passwords, private keys, OAuth client secrets, access tokens, GitHub tokens, AWS credentials, Firebase service-account private keys, service-account JSON indicators, Unity license indicators, and credentials embedded in URLs.

No discovered secret values were printed.

## Licensing/Privacy Findings

Technical readiness and licensing/privacy readiness are separate.

Likely private or license-sensitive categories in the prospective commit set:

| Category | Count | Size | Examples |
|---|---:|---:|---|
| Contributor/interview media | 148 | 1.02 GiB | `Assets/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4`; `Assets/BCaT_assets/LindaLeaks/Linda_Leaks_CHOF_720p_audio.wav`; `Assets/BCaT_assets/Meshell_Sturgis/My Grandma's Garden.png` |
| Personal/archival/family material | 360 | 691.87 MiB | `Assets/BCaT_assets/Ri/...`; `Assets/BCaT_assets/BlackFamilyMuseumArchive/...`; `Assets/BCaT_assets/HOMED/...`; `Assets/BCaT_assets/LindaLeaks/...` |
| Third-party Unity Asset Store content | 9,775 | 5.36 GiB | `Assets/SubstanceAssets/...`; `Assets/DevDen Arch Viz Scotland/...`; `Assets/TerrainSampleAssets/...`; `Assets/TextMesh Pro/...` |
| Original source videos/audio | 25 | 674.09 MiB | `Assets/StreamingAssets/*.mp4`; `Assets/BCaT_assets/Ri/*.mp4`; `Assets/BCaT_assets/*/*.wav`; `Assets/BCaT/Exhibits/BlackKitchen/Audio/*.mp3` |
| Point-cloud/scanned source assets | 4 | 106.28 MiB | `Assets/BCaT/Exhibits/BlackKitchen/Models/Reference/point_cloud_scan_REFERENCE_ONLY_DO_NOT_BUILD.ply`; `Assets/BCaT/Exhibits/BlackKitchen/Models/BlackKitchen_ScannedEnvironment.glb` |
| Licensed fonts | 14 | 2.51 MiB | `Assets/TextMesh Pro/Fonts/LiberationSans.ttf`; `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`; generated TextMesh Pro font assets |
| Client-only documents | 5 | 61.21 KiB | `BCAT_IMPLEMENTATION_AUDIT.md`; `BCAT_PROJECT_DEPENDENCY_AUDIT.md`; `BCAT_GITHUB_MIGRATION_MANIFEST.txt`; exhibit implementation notes |
| Credentials | 0 | 0 | No likely credentials found by the text scan |

These findings do not require removing files automatically. For the initial private backup, licensing and privacy risk is controlled by keeping the GitHub repository private with no collaborators. This is not a determination of redistribution rights.

## Remaining Blockers

No technical Git blockers remain for the initial private backup.

Accepted non-blockers for this initial backup:

- `Assets/_Recovery` remains eligible for commit and contains accepted legacy recovery-scene content.
- Empty directories are not preserved by Git.
- Existing folder `.meta` files remain untouched.
- Licensing/privacy review is deferred to repository-access control and later redistribution decisions.

## Exact Next Commands

Do not run these in this dry-run task.

Suggested validation commands:

```sh
cd "/Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST"
git status --short
git remote -v
git lfs status
git ls-files -z --cached --others --exclude-standard | tr '\\0' '\\n' | wc -l
git check-ignore -v Library/file Temp/file Logs/file UserSettings/file build/file build.app/file webgl/file app-build.apk quest.apk
git check-attr filter -- "Assets/StreamingAssets/and that is the truth - you know what I'm meaning_720p.mp4"
```

Suggested staging command only after blockers are resolved:

```sh
cd "/Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST"
git add .gitattributes .gitignore README.md GITHUB_MIGRATION_AND_ASSET_NOTICE.md BCAT_GITHUB_PREP_REPORT.md Assets Packages ProjectSettings
git status --short
git lfs status
```

READY TO STAGE
