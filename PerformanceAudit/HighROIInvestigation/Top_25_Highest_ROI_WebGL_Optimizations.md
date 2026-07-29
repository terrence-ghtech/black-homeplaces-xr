# Top 25 Highest-ROI WebGL Optimizations

## Executive Conclusion

1. The three largest WebGL constraints now are shadow/draw-call pressure, texture memory, and high-poly artifacts/vegetation/terrain geometry.
2. Implement first: remote media payload validation, safe shadow disabling, LOD/decimation for the top five meshes, flower/vegetation renderer reduction or instancing, and WebGL texture overrides.
3. Shadow work that can be safely removed: 1,447 of 1,939 casters, covering 1,185,178 of 2,726,764 shadow-caster triangles (43.5%).
4. Materials can realistically be consolidated from 307 focused-scene materials to about 250-270 after exact/near cleanup, or 210-240 after selected atlases.
5. Renderer-count reduction is realistically from 2,046 to about 1,350-1,600 without intrusive scene restructuring.
6. LOD Groups first: `metal_table_asset.glb`, `LL_PhotoAlbum.glb`, `glass_fish.glb`, `japanese_red_bridge.glb`, `drum.glb`, dense flower groups, and BlackKitchen scanned environment.
7. The five meshes accounting for ~1.5M triangles are `metal_table_asset.glb`, `LL_PhotoAlbum.glb`, `glass_fish.glb`, `japanese_red_bridge.glb`, and `drum.glb`.
8. Terrain is a top-five bottleneck candidate on lower-powered browsers and a high-value tuning target.
9. Noticeable FPS improvements are most likely from shadow reduction, renderer/instancing work, terrain tuning, and LOD/decimation of the top meshes.
10. Unlikely-to-be-noticeable changes: wholesale wall remodelling, single-use material cleanup without batching benefit, atlasing objects rarely visible together, and optimizing disabled sample scenes.

## Ranked Changes

| Rank | System / object / asset | Current measured problem | Proposed action | FPS | CPU | GPU | Memory | Download | Draw calls | Effort | Visual risk | Regression risk | WebGL | Quest | Validation | Required |
|---:|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Remote media delivery and payload validation | Large videos/audio; seven VideoPlayers | Keep videos remote/on demand and verify CDN paths | Low | Low | Low | High | Very high | Low | Medium | Medium | Critical | High | Build report + Network tab | Yes |
| 2 | Disable safe nonessential shadow casters | 1,939 casters; safe candidates cover 43.5% shadow-triangle work | Disable cast shadows on small props, flat decorations, distant vegetation | Medium-High | Medium | High | Low | None | High | Low | Low | Critical | High | Frame Debugger shadow caster draws | Yes |
| 3 | LOD/decimate metal_table_asset.glb | 370,088 triangles across 3 renderers | Create LODs/decimated mesh | Medium | Low | High | Medium | Medium | Medium | Medium | Medium | Critical | Medium | Visible tris + screenshots | Yes |
| 4 | LOD/decimate LL_PhotoAlbum.glb | 359,488 triangles across 4 renderers | Create close LOD0 and lower LODs | Medium | Low | High | Medium | Medium | Medium | Medium | Medium | Critical | Medium | Close screenshots | Yes |
| 5 | LOD/decimate glass_fish.glb | 300,000 triangles across 4 renderers | LOD and material/shadow review | Medium | Low | High | Medium | Medium | Medium | Medium | Medium | Critical | Medium | RI screenshots | Yes |
| 6 | LOD/decimate japanese_red_bridge.glb | 275,502 triangles across 9 renderers | Decimate hidden/detail parts and add LODs | Medium | Low | High | Medium | Medium | Medium | Medium | Medium | High | High | Backyard GPU frame | Yes |
| 7 | LOD/decimate drum.glb | 190,100 triangles across 3 renderers | LOD and shadow simplification | Medium | Low | Medium-High | Medium | Medium | Low | Medium | Medium | High | Medium | 9Night visible tris | Yes |
| 8 | Flower renderer reduction/instancing | 741 daisy renderers plus rose groups | Enable instancing or replace clusters with combined patches | Medium-High | High | Medium | Low | Low | Very high | Medium | Low | Critical | High | Porch batches/SetPass | Yes |
| 9 | Jungle bush instancing/shadow cleanup | Instancing enabled, many foliage shadows remain | Keep instancing, disable distant foliage shadows | Medium | Medium | Medium | Low | None | Medium | Low | Low | Critical | High | Porch/backyard frame capture | Yes |
| 10 | WebGL texture import overrides | ~568 MiB / 596 MB scene-referenced texture runtime memory | Cap secondary prop textures and compress normals/masks | Medium | Low | Medium | Very high | Medium | Low | Medium | Low | Critical | High | Memory Profiler | Yes |
| 11 | Terrain WebGL tuning | 513 heightmap, 524k full-res triangles, shadows on | Increase pixel error; reduce detail/tree distance | Medium | Low | Medium-High | Low | None | Low | Low | Medium | High | High | Backyard/porch GPU frame | Yes |
| 12 | Merge exact duplicate materials | 12 exact duplicate groups | Replace duplicates with canonical materials | Low-Medium | Medium | Low | Low | Low | Medium | Low | Low | High | Medium | Unique materials/SetPass | No |
| 13 | Near-duplicate material cleanup | 8 near-duplicate groups | Unify or use MaterialPropertyBlock | Low-Medium | Medium | Low | Low | Low | Medium | Medium | Medium | High | Medium | Material switches | No |
| 14 | Selective texture atlases | 17 atlas groups | Atlas co-visible small props/decor only | Medium | Medium | Low | Medium | Medium | Medium | High | Medium | High | Medium | SetPass and memory comparison | No |
| 15 | Italian Town wall/pillar batching | 147 wall_base and 32 pillar renderers | Static batch/merge by spatial section and material | Low-Medium | Medium | Low | Low | None | Medium | Medium | Medium | High | High | Renderer count screenshots | No |
| 16 | Porch fence instancing/combining | Repeated fence segments share material | Instance or combine porch fence groups | Low-Medium | Medium | Low | Low | None | Medium | Low | Low | High | High | Porch batches | No |
| 17 | Drone renderer reduction | 77 drone renderers; 83,712 tris | Merge static parts/LOD | Medium | Medium | Medium | Low | Low | High | Medium | Medium | High | Medium | Renderer count check | No |
| 18 | Spray can instancing | 9 repeated spray cans, 67,968 tris | Enable instancing/LOD or combine static cans | Low-Medium | Medium | Medium | Low | Low | Medium | Low | Low | Medium | Medium | BTMMP batches | No |
| 19 | Notebook LOD/decimation | 101,558 tris across 4 renderers | Decimate pages/spirals, add LOD | Low-Medium | Low | Medium | Low | Low | Low | Medium | Medium | Medium | Medium | Visible tris | No |
| 20 | Reduce MeshCollider cost | 388 MeshColliders and 466k collider triangles | Replace visual mesh colliders with primitives/low-poly colliders | Low FPS, high stutter prevention | Medium | Low | Low | None | None | Medium | Medium | High | High | Physics profiler | Yes for routes |
| 21 | Canvas/UI rebuild audit | 39 canvases in main scene | Ensure world-space UI updates only on state changes | Low-Medium | Medium | Low | Low | None | Low | Medium | Low | Medium | Medium | UI profiler markers | No |
| 22 | BlackKitchen scanned mesh LOD | Addressable scene has 659,560-triangle scanned environment | Add LOD/texture cap if memory spike is high | Medium for exhibit | Low | High | Medium | Medium | Low | High | Medium | High | Medium | BlackKitchen peak memory | Yes for exhibit |
| 23 | Remove unused/demo assets from build references | Large sample packages present | Verify no Resources/Addressables/build references include demo assets | Low FPS, high download risk | Low | Low | Medium | High | Low | Low | Low | Critical | Medium | Build size analyzer | Yes |
| 24 | URP mobile/WebGL renderer profile review | WebGL uses Mobile quality; MSAA 4; shadows enabled | Confirm WebGL-specific quality and shadow distances | Medium | Low | Medium | Low | None | Low | Low | Medium | High | High | A/B build frame time | Yes |
| 25 | Bake lighting for stable architecture | No baked lights recorded in main scene | Bake static contribution and disable runtime shadows selectively | Medium | Medium | Medium | Low | Medium | Medium | High | Medium | High | High | Shadow draws + screenshots | No/after first pass |

Impact ranges are estimates from static project inspection. Confirm with `benchmark_test_matrix.md` before and after each implementation batch.
