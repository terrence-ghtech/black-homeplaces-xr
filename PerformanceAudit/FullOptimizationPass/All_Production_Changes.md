# All Production Changes

Every file changed by the full optimization pass, by category. Base state is preserved in snapshot commit `3893523` on branch `perf/full-optimization-pass`.

## Scenes changed (2)
| Scene | Changes |
|---|---|
| `Assets/BH_XR_MainScene.unity` | 1,447 renderers: shadow casting Off (36 also receive Off); 31 renderer slots remapped to 4 canonical materials; 86 new LODGroups (6 top-asset parents + 80 vegetation objects) with generated LOD1/LOD2 child renderers; 11 MeshColliders re-pointed to simplified collision meshes; 900 flower renderers disabled and replaced by 4 combined chunk renderers; 261 architecture objects flagged Batching/Occludee/Occluder-static; terrain component retuned (pixelError 4, basemap 90, detail 18, tree 90, billboard 45, crossfade 8) |
| `Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity` | 1 LODGroup on the scanned environment (659,560 → LOD1 329,779 / LOD2 131,912); no collider or audio/interaction objects touched; isolated-scene loading design unchanged |

## Prefabs changed
None. All scene changes are instance-level; no prefab asset was edited (LOD copies and combined meshes are added scene objects; renderer changes are instance overrides).

## Materials changed (16 .mat files)
- **Instancing enabled (14)**: M_DEMOAtlas_LowPolyFlowers, mat_Chair37, bathroom_wall_mat_1, M_attachments_stone, M_chairs, M_skybox, M_wall_plaster, M_wall_plaster 10, M_LowPolyLivingRoom_Floor, OldPathway, PandaMat, SimpleNaturePack_Texture_01, Glass (Basic Asset Pack), PrivacyLaw_HologramLine (see Instancing_Changes.csv for the full authoritative list)
- **Canonical duplicates kept, duplicates remapped in-scene**: `HallWood 1.mat` (absorbs HallWood 2/3/4), `OldPathway.mat` (absorbs OldStairway). Duplicate .mat files were **not deleted** (preserve-source policy); they are simply no longer referenced by the main scene.
- No transparent/emission/video/RenderTexture material was merged; embedded GLB materials untouched.

## Import settings changed (551 texture importers)
Third-party pack textures only (17 pack prefixes; see Texture_Import_Changes.csv): default Max Size capped at 2048, WebGL + Android platform overrides at 1024 for >1024 sources, Read/Write disabled, compression enforced. Archival photos, exhibit graphics, plaques, and BCaT_assets content excluded by design.

## Project settings changed
- `ProjectSettings/QualitySettings.asset` — Mobile level tuned (AA 2, shadow distance 30, lodBias 1.2, terrain overrides corrected), new Quest level (index 2), Android default → Quest.
- `Assets/Settings/Mobile_RPAsset.asset` — MSAA 4→2, shadow distance 50→30.
- `Assets/Settings/Quest_RPAsset.asset` — **new** URP asset (Mobile clone: MSAA 4, shadow distance 20, same renderer data).

## Generated assets (new, `Assets/BCaT/OptimizedMeshes/`, 73 meshes)
- LOD1/LOD2 meshes for: metal_table_asset, LL_PhotoAlbum (shared by RW + LindaLeaks preview), glass_fish, japanese_red_bridge, drum, BlackKitchen scanned environment.
- Simplified collision meshes (`*_COL`) — bridge rails + flower pot + roof segments in use; remainder generated alongside LODs and available for future collider swaps.
- 4 combined flower chunk meshes (`Combined_Flowers_*`).
- All 73 validated: vertices > 0, triangles > 0, valid bounds, zero NaN vertices, normals + UV0 present, submesh counts match renderer material counts (see Optimized_Mesh_Validation.csv).

## Scripts added or changed
- **Added (editor-only tooling, kept and documented)**:
  - `Assets/Editor/FullOptimizationPass/FullOptimizationPassTool.cs` — batch orchestrator (phases, CSV change logs, metrics, validation). Entry points: `RunBeforeMetrics`, `RunFullPass`, `RunValidationAndAfterMetrics`.
  - `Assets/Editor/FullOptimizationPass/QemMeshSimplifier.cs` — quadric-error-metric simplifier (subset placement, seam-safe, border-preserving). Reusable for future LOD work.
- **No runtime script was added or modified.** Platform behavior differences ride entirely on Quality settings.
- Pre-existing editor tools (`BCATOptimizationTool.cs` etc.) untouched.

## Temporary tools status
The two editor scripts above are retained deliberately as production-safe, editor-only, batch-invoked tools (they do nothing at runtime and are excluded from builds by being under `Editor/`). Nothing else temporary remains; no scratch files live in `Assets/`.

## Requires manual Unity Editor review (visual QA that batch mode cannot do)
1. LOD transition distances on the six top assets — walk up to each; verify no visible pop (CrossFade is enabled).
2. Glass fish LOD1/LOD2 — verify glass transparency and inner/outer body sorting at mid-distance.
3. Bridge rails collision — walk the bridge, lean into rails; deck/steps colliders are untouched originals.
4. Flowerbeds — combined chunks must look identical (geometry was combined in place; verify wind/shader behavior unchanged since material is identical).
5. WebGL build + browser benchmark; Addressables rebuild + UCD upload for the Black Kitchen bundle before next deploy.
6. Terrain: check billboard start at 35 (WebGL) from the porch.
