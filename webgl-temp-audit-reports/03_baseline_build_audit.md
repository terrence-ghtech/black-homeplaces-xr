# Stage 2/3 — Baseline Build & Audit
Date: 2026-07-23
Build output: webgl-temp-audit/ (fresh production WebGL build, batch mode, existing ProjectSettings)
BuildReport dumps: webgl-temp-audit-reports/baseline/

## Build execution (measured)
- Result: **Succeeded** — 0 errors, 9 warnings (all benign shader `pow(f,e)` warnings in UnityGLTF/PBRGraph and TerrainGrass shadergraphs)
- Duration: 1,694.8 s (28.2 min)
- BuildReport GUID: 69a5c11880d1465a90545e4610050a79
- Invocation: Unity 6000.4.5f1 `-batchmode -nographics -buildTarget WebGL -executeMethod BCATWebGLAuditTool.BuildToAuditFolder` with `BuildOptions.DetailedBuildReport`, scenes from EditorBuildSettings (3 enabled), Brotli from PlayerSettings.

## Final compressed artifacts (measured, bytes)
| File | Size |
|---|---|
| Build/f4cd4962c62d3f52fd0d675a7e5bbe10.data.br | 705,833,122 (~706 MB) |
| Build/97bb6c951b33046c99ccda3a25c7f391.wasm.br | 9,481,388 (9.5 MB) |
| Build/1cf9e49c1a27a570ecb00483bb4605bc.framework.js.br | 85,745 |
| Build/ca10e9f6e1e2f32a022b15efaddafedb.loader.js | 26,982 |
| StreamingAssets/ (6 mp4) | 205,033,081 (~196 MiB) |
| TemplateData/ + index.html + GUID.txt + ProjectVersion.txt | ~62 KB |
| **Total build folder** | **894 MiB (du)** / Unity totalSize 945,776,711 B |

### Fidelity vs existing production build (webgl-phase9)
- .wasm.br **identical size** (9,481,388), framework.js.br identical (85,745), loader.js identical (26,982).
- .data.br 705,833,122 vs 705,798,466 (+34,656 B, +0.005%) — the baseline reproduces production. (The tiny data delta is consistent with the auto-generated `Assets/Resources/PerformanceTestRun*.json` files re-created at editor start; see Stage 4/5.)

## Unity packed payload (from BuildReport packedAssets — uncompressed sizes inside .data)
Total packed: **2,153.5 MB** across 7,997 packed-asset entries.

By pack file:
| Pack | MB | Meaning |
|---|---|---|
| sharedassets0.assets | 1,806.4 | BH_XR_MainScene content |
| sharedassets1.assets | 299.3 | BlackKitchen_MemoryScene content |
| sharedassets0.resource | 29.7 | streamed audio/video bytes (MainScene) |
| globalgamemanagers.assets | 8.0 | settings, always-included shaders |
| sharedassets1.resource | 7.9 | streamed audio (BlackKitchen) |
| resources.assets | 2.1 | Resources folders content |
| sharedassets2.assets | 0.04 | LoadingScene content |

By asset type (packed size):
| Type | MB |
|---|---|
| Texture2D | **1,960.7 (91%)** |
| Mesh | 135.7 |
| AudioClip | 37.5 |
| Shader | 15.5 |
| Material | 1.1 |
| everything else | < 2 |

By source top-level folder (packed size, top entries):
| Folder | MB packed |
|---|---|
| Assets/BCaT_assets | 772.4 |
| Assets/BCaT | 310.3 |
| Assets/My_Custom | 234.0 |
| Assets/Furniture Mega Pack | 217.8 |
| Assets/DevDen Arch Viz Scotland | 202.1 |
| Assets/TerrainSampleAssets | 113.2 |
| Assets/Idyllic Italian Coast Town | 95.7 |
| Assets/SubstanceAssets | 56.0 |
| Assets/Samples (XRI) | 28.7 |
| Assets/Shaded Spectrum | 28.0 |
| Assets/HyTeKGames | 21.7 |
| Packages/com.unity.render-pipelines.universal | 16.3 |
| Assets/BCAT_GLB_TextureRemediation | 14.0 |
| others | < 10 each |

Top individual assets (packed):
| MB | Asset |
|---|---|
| 299.3 | Assets/BCaT/Exhibits/BlackKitchen/Models/BlackKitchen_ScannedEnvironment.glb |
| 172.8 | Assets/My_Custom/modern_scandinavian_kitchen_island.glb |
| 168.0 | Assets/BCaT_assets/SewingRoom/bed.glb |
| 146.0 | Assets/BCaT_assets/BTMMP_Workstation_Assembly/flowerbarrel_with_tulips.glb |
| 56.3 | Assets/BCaT_assets/SewingRoom/old_sewing_box.glb |
| 27.3 | Assets/My_Custom/japanese_red_bridge.glb |
| 24.8 | Assets/BCaT_assets/Ri/glass_fish.glb |
| 22.4 | Assets/SubstanceAssets/LittleGamesPack/Textures/GamePieces/GamePieces_Normal_OpenGL.png |
| 21.0 | Assets/BCaT_assets/9night/drum.glb |
| 19.8 | Assets/BCaT_assets/LindaLeaks/Models/LL_AntiqueCamera.glb |

(GLB entries aggregate the textures/meshes Unity imported from each GLB; their packed size is the uncompressed imported representation, not the source file size.)

## Size vocabulary used throughout this audit
- **Source asset size** = bytes of the file in Assets/ on disk.
- **Unity packed size** = bytes the asset occupies inside the uncompressed .data payload (BuildReport packedAssets).
- **Final compressed artifact size** = bytes of the Brotli-compressed files actually served (.data.br, .wasm.br, …).

## Assemblies & stripping
- IL2CPP, managed stripping level Medium (WebGL). strippingInfo captured to baseline/stripping_info.txt (engine modules retained: Animation, Audio, Core, Director, Video, Terrain, UI, Physics, ParticleSystem, etc., with inclusion reasons).
- Full step timings in baseline/build_steps.txt; output file roles in baseline/build_files.tsv; per-scene asset usage in baseline/scenes_using_assets.tsv (DetailedBuildReport).

## Measured vs not measured
- Measured: everything above from BuildReport + filesystem.
- Not exposed by Unity / not invented: per-asset *compressed* contribution to .data.br (Brotli compresses the whole payload as one stream; per-asset compressed sizes do not exist).
