# Full Cross-Platform Optimization Pass — Executive Report

Date: 2026-07-28 · Unity 6000.4.5f1 · Branch `perf/full-optimization-pass` (snapshot base `3893523`)
Scope: Unity Editor, Desktop standalone, WebGL, Quest/Android XR — one shared project, platform differences only via Quality levels, URP assets, and texture platform overrides.

## What was done (17 requested areas)

| # | Area | Outcome |
|---|---|---|
| 1 | Shadow optimization | **1,447 renderers** stopped casting (audit safe list, visual_risk=Low only); 36 also stopped receiving. Active-set shadow triangles **−36.6%** (2,430,351 → 1,540,581). Architecture, characters, large furniture, interactives keep shadows. |
| 2 | Material cleanup | 4 exact-duplicate .mat files remapped to 2 canonicals across 31 slots (297 → 293 unique). Embedded-GLB duplicates and all near-duplicates deliberately untouched (risk > benefit; SRP Batcher absorbs same-shader cost). |
| 3 | GPU instancing | 14 materials newly enabled (17 already on; 9 embedded skipped). Applied only to shared-mesh/shared-material repeated objects per audit list. |
| 4 | Texture atlasing | **Deliberately not executed** — evaluated all 19 audit groups; decision + criteria in Atlas_Changes.csv. |
| 5 | Renderer reduction | 900 flower renderers → **4 combined chunks** (placement/color identical, originals kept disabled); 261 architecture objects static-flagged for batching/occlusion. |
| 6 | Terrain | Kept Unity terrain; scene baseline retuned; **fixed inverted Quality terrain overrides** (WebGL had been overriding to worse values); per-tier distances for WebGL/Quest. |
| 7–10 | LODs / top-5 meshes / Black Kitchen | 86 new LODGroups in main scene + 1 in BK. All five audit targets + BK scan decimated via in-house QEM simplifier (~50% / ~20% tiers, CrossFade): metal_table 370k→185k/74k · PhotoAlbum 359k→216k/90k (both uses) · glass_fish 300k→150k/60k · bridge 275k→138k/50k · drum 190k→105k/42k · BK scan 660k→330k/132k. **Originals preserved as LOD0; no GLB overwritten.** 80 vegetation LODGroups wired from LOD meshes already inside the FBXs. |
| 11 | Vegetation/flowers | Combine + instancing + shadows-off; memorial arrangements untouched in placement and color. |
| 12 | MeshColliders | Collider triangles **−58.4%** (466,240 → 193,740): bridge rails ~12% simplified meshes; deck/steps colliders untouched; zero triggers modified. |
| 13 | Texture memory | 551 importers: default cap 2048, WebGL **and** Android/Quest 1024 overrides, Read/Write off, compression on. Archival/exhibit imagery excluded. |
| 14 | Media delivery | All 6 remote videos + BK Addressables bundle validated live (200/206, video/mp4, byte ranges, CORS both hops). No local duplication. |
| 15 | URP/Quality | WebGL tier: MSAA 4→2, shadow distance 50→30, lodBias 1.2. **New Quest tier** (Android default): MSAA 4, shadow distance 20, own URP asset. PC untouched. HDR kept everywhere (visual tone). |
| 16 | Build references | Audited every channel — already clean; nothing needed deletion (Build_Reference_Changes.md). |
| 17 | UI/canvases | 39 canvases audited; 1 EventSystem/AudioListener per scene confirmed; no automated changes needed; watchlist documented. |

## Measured results (editor-static; see Before_After_Performance_Metrics.csv)
- Shadow casters (active set): 795 → 487; scene-wide 1,447 casters disabled.
- Shadow-caster triangles: −36.6%. Collider triangles: −58.4%.
- Flower renderers: 900 → 4 when beds are active. LODGroups: 144 → 230.
- Distant-view triangle budget for the six heaviest assets drops to ~50% (LOD1) and ~20% (LOD2) of 2.15M source triangles; close-view (LOD0) intentionally unchanged.
- 73/73 generated meshes pass integrity validation; 0 QEM failures; 0 new missing scripts/materials/meshes.

## What was deliberately not changed
Atlases; near-duplicate/embedded materials; baked lighting; terrain shadows & heightmap; HDR/post tone; archival texture resolution; any trigger, interaction script, prefab asset, or source mesh; duplicate .mat files kept on disk unreferenced; demo packs left in repo (not in builds).

## Honest limitations
- **No WebGL or Quest build was produced in this pass** — runtime FPS/memory improvements are *expected*, not demonstrated. The static reductions above are measured facts; the benchmark matrix remains to be run.
- Visual QA of generated LODs needs a human eye (checklist provided).
- BK Addressables bundle must be rebuilt + uploaded to UCD with the next deploy.

## Strategy assessment
The project now follows a maintainable Unity-6-native cross-platform pattern: one scene set, one prefab set, shared optimized meshes, platform deltas expressed only through Quality levels (PC / Mobile-WebGL / Quest), URP assets, Quality terrain overrides, and texture platform overrides — nothing fights Unity's batching, lighting, terrain, import, or rendering systems. **Ready for final WebGL and Quest validation builds**, pending the runtime benchmark and visual QA steps in Remaining_Risks_and_Recommendations.md.
