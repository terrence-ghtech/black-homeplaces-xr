# BCaT Unity WebGL Architectural and Asset Performance Audit

Generated from Unity batch-mode Editor inspection and filesystem analysis on 2026-07-28. No production scenes, prefabs, import settings, or assets were saved or modified. A temporary Editor audit runner was used and then removed.

## Definitive conclusion

Architectural geometry is a real optimization target, but it is not the dominant WebGL bottleneck in the current project data.

The build-enabled main scene has 2,728,090 renderer triangles, 2,046 renderers, 2,140 material slots, 305 unique materials, 1,939 shadow-casting renderers, 388 MeshColliders, and an estimated 524,288 terrain triangles. By comparison, all active `Idyllic Italian Coast Town` references in the main scene total about 133,742 triangles, or 4.9% of renderer triangles. The structural subset most relevant to walls/floors/roofs/pillars/street pieces is about 108,568 triangles, or 4.0%.

Replacing every current wall, floor, ceiling, and structural asset with new primitive or modular geometry would not be the highest-return WebGL fix. The bigger measured constraints are draw calls/renderer count, realtime shadow work, texture memory, terrain, and a few very high-poly exhibit/artifact meshes.

## Answers to the primary questions

1. Are walls, floors, ceilings, and structural assets materially hurting WebGL performance? Yes, but mainly through renderer/material/shadow overhead and repeated modular pieces, not raw triangle count. They are not the largest measured geometry cost.
2. Would lower-poly modular assets produce a major improvement? Selectively, yes. A wholesale rebuild is unlikely to be major because Italian/structural architecture is a small share of total triangles. Merging, batching, atlasing, and shadow disabling should come first.
3. Would sprites, planes, decals, or baked textures make a meaningful difference? Yes for flat decorative details, distant fences/vegetation/backgrounds, murals, framed graphics, window views, and noninteractive distant clutter. No for nearby structural walls, floors, stairs, porches, doors, and windows that need parallax, collision, lighting, and XR depth.
4. Which replacements significantly improve the project? LOD/decimation for the top artifact meshes, terrain/detail simplification, texture overrides, shadow reductions, media delivery, and selective Italian Town module batching/simplification.
5. Which replacements are low return? Rebuilding 300-triangle wall modules, converting close structural architecture to sprites, and optimizing disabled sample scenes.
6. Is architectural geometry top constraint? No. The top constraints are draw-call/shadow count, texture memory, terrain, media delivery/decoding, and a few high-poly artifacts. Architecture is secondary.
7. Required for reliable WebGL: remote/on-demand media, texture memory reduction, shadow/draw-call reduction, Addressables validation, and real WebGL build profiling.
8. Optional polish: deeper architectural remodelling, decorative decal baking, distant impostors, and visual-only LOD refinements after the required pass.

## Evidence summary

### Build Settings

| Enabled | Scene |
|---|---|
| yes | `Assets/BH_XR_MainScene.unity` |
| no | `Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity` |
| yes | `Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity` |

`BlackKitchen_MemoryScene` is not enabled in Build Settings, but Addressables contains a `BlackKitchen_Remote` group with address `BlackKitchen_MemoryScene`. The loading controller uses `SceneManager` for built-in scenes and Addressables for scenes not built into the player.

### Main scene totals

| Metric | Value |
|---|---:|
| GameObjects | 2,966 |
| Renderers | 2,046 |
| Renderer triangles | 2,728,090 |
| Renderer vertices | 2,189,964 |
| Material slots | 2,140 |
| Unique materials | 305 |
| Shadow-casting renderers | 1,939 |
| MeshColliders | 388 |
| MeshCollider triangles | 466,240 |
| Terrain estimate | 524,288 triangles |
| LODGroups | 144 |
| Static/occlusion-static renderers | 81 / 81 |
| VideoPlayers / AudioSources | 7 / 10 |
| Canvases | 39 |

### BlackKitchen memory scene

`BlackKitchen_MemoryScene` has 27 renderers, 676,436 renderer triangles, 7 unique materials, 0 MeshColliders, 6 audio sources, and no terrain. Because it is configured as an Addressable remote scene, its optimization affects exhibit entry memory/download spikes rather than initial main-scene cost, assuming Addressables are built and deployed correctly.

## Largest main-scene mesh sources

| Triangles | Renderers | Source asset |
|---:|---:|---|
| 370,088 | 3 | `Assets/BCaT_assets/BTMMP_Workstation_Assembly/metal_table_asset.glb` |
| 359,488 | 4 | `Assets/BCaT_assets/LindaLeaks/Models/LL_PhotoAlbum.glb` |
| 300,000 | 4 | `Assets/BCaT_assets/Ri/glass_fish.glb` |
| 275,502 | 9 | `Assets/My_Custom/japanese_red_bridge.glb` |
| 190,100 | 3 | `Assets/BCaT_assets/9night/drum.glb` |
| 168,948 | 741 | `Assets/Emilulz_Assets/DEMOLowPolyFlowers/Assets/SM_Daisy_Single.fbx` |
| 101,558 | 4 | `Assets/BCaT_assets/BTMMP_Workstation_Assembly/notebook.glb` |
| 83,712 | 77 | `Assets/BCaT_assets/BTMMP_Workstation_Assembly/drone.glb` |
| 67,968 | 9 | `Assets/BCaT_assets/BTMMP_Workstation_Assembly/spray_can.glb` |
| 57,156 | 5 | `Assets/Furniture Mega Pack/FBX/Bed/Bed28.fbx` |
| 56,216 | 1 | `Assets/Furniture Mega Pack/FBX/Sofa/Sofa13.fbx` |
| 46,640 | 1 | `Assets/BCaT_assets/SewingRoom/pillow__quilt.glb` |
| 44,100 | 147 | `Assets/Idyllic Italian Coast Town/Meshes/Modular/SM_wall_base.fbx` |
| 42,588 | 126 | `Assets/Emilulz_Assets/DEMOLowPolyFlowers/Assets/SM_Rose_Red_Open.fbx` |
| 40,119 | 129 | `Assets/Animated Tropical Vegetation/Models/Jungle Bushes/jungle_bush_1.fbx` |


The top five source assets alone contribute about 1,495,178 triangles. These are not walls or floors.

## Italian Town / modular architecture findings

| Triangles | Renderers | Source asset |
|---:|---:|---|
| 44,100 | 147 | `Assets/Idyllic Italian Coast Town/Meshes/Modular/SM_wall_base.fbx` |
| 30,272 | 32 | `Assets/Idyllic Italian Coast Town/Meshes/Modular/SM_wall_pillar.fbx` |
| 20,440 | 4 | `Assets/Idyllic Italian Coast Town/Meshes/Buildings/SM_building_modular_roof_2x1_01.fbx` |
| 10,296 | 12 | `Assets/Idyllic Italian Coast Town/Meshes/Buildings/SM_building_modular_floor_2x1_03.fbx` |
| 10,200 | 3 | `Assets/Idyllic Italian Coast Town/Meshes/Props/SM_fence_03.fbx` |
| 6,600 | 6 | `Assets/Idyllic Italian Coast Town/Meshes/Props/SM_chair_02.fbx` |
| 3,460 | 2 | `Assets/Idyllic Italian Coast Town/Meshes/Modular/SM_street_01.fbx` |
| 3,300 | 1 | `Assets/Idyllic Italian Coast Town/Meshes/Props/SM_fence_rounded.fbx` |
| 3,096 | 6 | `Assets/Idyllic Italian Coast Town/Meshes/Props/SM_fence_02.fbx` |
| 1,260 | 1 | `Assets/Idyllic Italian Coast Town/Meshes/Props/SM_boat_pylon.fbx` |
| 602 | 1 | `Assets/Idyllic Italian Coast Town/Meshes/Props/SM_flowerpot_04.fbx` |
| 116 | 1 | `Assets/Idyllic Italian Coast Town/Meshes/Props/SM_table_cafe_square.fbx` |


Interpretation: `SM_wall_base` is repeated 147 times but only 300 triangles per instance. That is not a mesh-complexity crisis; it is a renderer/batching/shadow-management issue. `SM_wall_pillar` and roof modules are more meaningful, but still secondary compared with artifact meshes and texture/shadow cost.

## Texture and media findings

The build-enabled scenes reference about 568.1 MB of unique texture runtime memory across 268 textures, before engine overhead, WASM heap, render targets, browser decoder memory, and runtime allocations. Texture streaming is disabled in Quality Settings. The entire project contains about 6079.3 MB of texture runtime memory if loaded, including large unused/demo reflection probes and terrain textures.

Referenced main-scene audio clips total about 241.9 MB by source file size. The largest referenced audio item is `Assets/BCaT_assets/9night/9night-soundscape.wav` at about 95 MB on disk, imported as Streaming Vorbis. Large videos exist in the project, and runtime scripts resolve video URLs through `RemoteMediaConfig`/StreamingAssets; the scene VideoPlayer components do not hold direct clip references in the audit output.

## Bottleneck classification

| Constraint | Ranking | Evidence |
|---|---|---|
| Draw-call / renderer / SetPass pressure | Top | 2,046 renderers, 2,140 material slots, 305 unique materials, only 81 static/occlusion-static renderers. |
| Shadow rendering | Top | 1,939 renderers cast shadows in the main scene with realtime lights and no baked lights recorded. |
| Texture memory | Top | ~568 MiB / 596 MB scene-referenced texture runtime memory before WebGL/browser overhead. |
| Terrain / vegetation | High | Terrain estimate ~524k triangles plus vegetation/flower repetition; many small vegetation renderers cast shadows. |
| High-poly artifacts | High | Top five non-architecture assets contribute ~1.5M triangles. |
| Media delivery / decoding | High | Seven VideoPlayers, large videos in Assets, remote URL resolver required for WebGL reliability. |
| Architecture geometry | Medium | Italian Town active references are ~4.9% of renderer triangles; structural subset ~4.0%. Renderer/shadow overhead is more important than mesh shape. |
| CPU scripting / GC | Unknown to medium | Static inspection found scene loading and media controllers, but no runtime profiler capture was taken. Validate in WebGL build. |
| Fill-rate / transparent overdraw | Unknown to medium | Vegetation/UI/video surfaces may contribute; requires frame capture. |
| Download size | High risk | No fresh WebGL build was generated in this audit, but asset/media sizes and Addressables setup indicate download management is critical. |

Desktop WebGL is most likely draw-call/shadow/texture-memory constrained. Lower-powered browsers are likely memory-bound first, then draw-call/shadow and terrain/vegetation bound. Media decoding can become the dominant spike during exhibit playback.

## 2D replacement guidance

Use planes/decals/sprites for distant mountains or horizon elements, distant vegetation, background buildings, flat exhibit graphics, murals, framed photos, window views, distant fences, and flat noninteractive details. Use billboards/impostors for distant vegetation and far clutter when the visitor cannot inspect side/rear views.

Do not convert nearby walls, floors, ceilings, stairs, porches, interactive doors/windows, or walkable/collidable structures to sprites. The camera movement and XR use will expose missing parallax, incorrect lighting, shadow mismatch, and collision mismatch.

## Required deliverables generated

Raw data and reports are in `PerformanceAudit`:

- `00_initial_findings.md`
- `BCaT_WebGL_Performance_Audit.md`
- `implementation_plan.md`
- `architecture_replacement_decisions.csv`
- `scene_totals.csv`
- `scene_renderers.csv`
- `scene_texture_references.csv`
- `scene_media_references.csv`
- `models.csv`
- `textures.csv`
- `materials.csv`
- `media.csv`
- `prefabs.csv`
- `architecture_candidates.csv`
- `unity_project_overview.md`
- `unity_batch_audit.log`
