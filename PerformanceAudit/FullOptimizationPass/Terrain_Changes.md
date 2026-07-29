# Terrain Changes

Object: `_Rendering/Terrain/Terrain_main` in `Assets/BH_XR_MainScene.unity` (TerrainData: `Assets/New Terrain.asset`).
The existing Unity Terrain system was kept — no mesh replacement, no flattening. Heightmap (513), alphamaps, 7 layers, 2 tree prototypes, and 8 detail prototypes are untouched. All hand-placed vegetation (Big Mama's Garden, the well, bridge/creek composition, memorial flowerbeds) is scene geometry, not terrain details, and was not moved.

## How platform differences are expressed

Unity 6 **Quality-level terrain overrides** (`terrainQualityOverrides`) were already enabled on the Mobile quality level — and were *hurting* WebGL: the override values (pixelError 3, detail 40, tree 180, billboard 80) silently replaced the scene's tuned values. This pass fixes the override values instead of fighting the system.

| Setting | Scene baseline (PC/Editor) | Mobile / WebGL override | Quest override (new tier) |
|---|---:|---:|---:|
| Pixel error | 6 → **4** | 3 → **6** | **7** |
| Basemap distance | 100 → **90** | 300 → **80** | **80** |
| Detail distance | 20 → **18** | 40 → **14** | **12** |
| Detail density scale | 1 | 1 | 1 |
| Tree distance | 100 → **90** | 180 → **75** | **65** |
| Billboard start | 50 → **45** | 80 → **35** | **30** |
| Tree cross-fade length | 5 → **8** | (scene value) | (scene value) |
| Draw instanced | On (kept) | On (kept) | On (kept) |
| Terrain cast shadows | On (kept) | On (kept — see note) | On (kept) |

Estimated effect on WebGL: terrain tessellation roughly halves (pixel error 3 → 6), tree full-LOD range drops 180 → 75 with billboards starting at 35, and grass detail draw range drops 40 → 14. The house route, porch, and backyard remain within full-detail range.

## Deliberately not changed
- **Terrain shadow casting stays on.** The terrain has a 600-unit height range; disabling casting could visibly change the creek/backyard composition. The realtime shadow cost is already bounded by the reduced URP shadow distance (30 on WebGL, 20 on Quest). If further GPU headroom is needed on Quest, disabling terrain `shadowCastingMode` is the next candidate — flag for visual review.
- Heightmap resolution kept at 513 — resampling would shift the walkable surface and hand-authored compositions.
- No terrain layer was removed; layer textures already get WebGL/Android 1024 caps via texture import overrides.
- Detail prototypes and tree prototypes unchanged; billboards were already authored (AFS palm billboard materials exist).

## Validation notes
Benchmark from front porch, backyard/pond, and second-floor exterior. Watch for: billboard pop at 35 m (WebGL), grass draw-in at 14 m. If grass pop-in near the flowerbeds is noticeable, the beds themselves are mesh flowers (combined/instanced in this pass), not terrain details, so the memorial reading is unaffected.
