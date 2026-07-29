# Regression Test Results

Honest scope statement: everything below was executed in **Unity 6000.4.5f1 batch mode (edit mode)** on this machine. **No WebGL build, no Android/Quest build, and no Play Mode FPS benchmark was run in this pass** — those are listed as required follow-ups, and no runtime improvement claim is made from them.

## Executed checks — all PASS

### Compile / project health (3 independent batch launches after changes)
| Check | Result |
|---|---|
| Script compilation (Editor + tooling) | PASS — 3 batch runs, zero `error CS` |
| Project opens with 6000.4.5f1 | PASS |
| All 3 build-settings scenes open without exceptions | PASS |
| Asset import after 551 texture importer changes | PASS — no import errors in logs |

### Scene integrity (`scene_validation.txt`)
| Check | Main scene | Black Kitchen | Loading |
|---|---|---|---|
| Missing scripts | 0 | 0 | 0 |
| Null material slots | 5 (pre-existing MemoryRoom "Pen" objects — identical list documented 2026-07-23, not introduced by this pass) | 0 | 0 |
| Missing meshes | 0 | 0 | 0 |
| Duplicate LODGroup components | 0 | 0 | 0 |
| LOD triangle ordering (each level ≤ previous) | 0 errors | 0 errors | — |
| LOD renderer material count vs submesh count | 0 mismatches | 0 mismatches | — |
| LOD0 accidentally using an optimized mesh | 0 | 0 | — |
| Empty MeshColliders | 0 | 0 | 0 |
| Active EventSystems | 1 | 1 | 0 |
| Active AudioListeners | 1 | 1 | 0 |
| Addressables settings + groups | PASS (2 groups; BK entry intact) | | |

### Generated mesh integrity (`Optimized_Mesh_Validation.csv`)
All **73/73 PASS**: vertices > 0, triangles > 0, valid non-NaN bounds, zero NaN vertices, normals and UV0 present, submesh counts intact.

### QEM simplification failures
**Zero.** No mesh was skipped or substituted; every requested LOD/collision mesh was produced (grep `[QEM]` in `unity_full_pass.log` → 0 warnings).

### Top-asset spot validation
| Asset | LOD0 (orig) | LOD1 | LOD2 | Interaction surface |
|---|---:|---:|---:|---|
| metal_table_asset | 370,088 | 185,041 | 74,018 | no colliders existed; none added |
| LL_PhotoAlbum ×2 uses | 179,744 each | 107,847 | 44,936 | album interaction scripts/colliders untouched; LOD renderers are visual children only |
| glass_fish | 300,000 | 149,999 | 60,000 | 4 renderers incl. inner body kept in one LODGroup; submesh/material counts verified 1:1; transparent materials shared with LOD0 (same sorting behavior) |
| japanese_red_bridge | 275,502 | 137,632 | 49,546 | deck/steps MeshColliders **unchanged** (full res); 6 rail colliders simplified ~12%, still concave MeshColliders |
| drum | 190,100 | 104,555 | 41,822 | 9Night interaction untouched |
| BK scanned environment | 659,560 | 329,779 | 131,912 | BK oven/pot/audio objects untouched; scene save verified |

### Media / remote delivery (live HTTP)
All 6 videos: 200, `video/mp4`, byte-range 206, CORS on both hops. BK Addressables bundle: 206. See Media_Validation.md.

### Functional areas statically verified as untouched
Movement rigs, spawn points, portals, scene-transition scripts, audio coordinator, video controllers, interaction prompts, photo-album scripts, oven/pot interactions, exit modal, Privacy Law UI objects: **no component on any of these objects was modified** — the pass only touched Renderer flags, LODGroups, MeshCollider mesh references (never on interactable-context or trigger objects — triggers were explicitly skipped), and disabled plain flower renderers.

## NOT executed (required before release claims)
1. **Play Mode functional walkthrough** — movement, portals, album, oven/pot, exit modal, Privacy Law UI (needs interactive session; batch mode cannot).
2. **WebGL build + Chrome benchmark** — FPS, frame time, batches, SetPass, memory peak (benchmark_test_matrix.md has the route list).
3. **Addressables rebuild + UCD upload** — BK scene changed, so the bundle hash changes at next build.
4. **Quest/Android build** — no Android SDK build was produced; Quest readiness is configured (quality tier, texture overrides) but **not demonstrated**.
5. Visual QA items listed in All_Production_Changes.md (LOD pops, glass fish, bridge rails, flowerbeds, terrain billboards).
