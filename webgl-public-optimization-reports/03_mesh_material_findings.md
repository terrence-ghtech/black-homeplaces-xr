# Stage 3 — Mesh & Material Findings (measured) and Actions
Date: 2026-07-23

## Measured mesh/material inventory (from Unity, post-import)
Mesh payload in the baseline build: **135.7 MB uncompressed of 2,153.5 MB (6.3%)**; after the Stage 2 texture pass, meshes become the second-largest category but remain small in absolute terms next to remaining textures/audio.

Highest-triangle in-build models (measured):
| Model | Triangles | Vertices | Meshes | Materials | Note |
|---|---|---|---|---|---|
| BlackKitchen_ScannedEnvironment.glb | 659,560 | 478,024 | 1 | 1 | photogrammetry room; moves to remote Addressables (out of initial payload) |
| metal_table_asset.glb | 370,088 | 196,207 | 3 | 1 | very high for a table |
| glass_fish.glb | 300,000 | 187,307 | 4 | 2 | exhibit hero |
| japanese_red_bridge.glb | 275,718 | 356,598 | 10 | 2 | garden decoration |
| drum.glb | 190,100 | 179,321 | 3 | 1 | exhibit |
| LL_PhotoAlbum.glb | 179,744 | 105,140 | 2 | 1 | interactive exhibit |
| notebook.glb | 101,558 | 92,549 | 4 | 3 | interactive (BoxCollider-based interaction) |
| drone.glb | 83,712 | 59,334 | **77** | **77** | material/mesh-slot bloat — 77 draw-call-heavy submeshes |

## Actions taken (zero visual risk, verified)
- **Mesh Read/Write**: measured `readable=False` on all inspected GLB meshes (importer `_readWriteEnabled: 0`) — no CPU-side mesh copies at runtime. No change needed.
- **Texture Read/Write**: disabled (`_texturesReadWriteEnabled: 1 → 0`) on 20 in-build GLBs (Stage 2) — this halves runtime texture memory for those assets (CPU copy eliminated). Code sweep confirmed nothing reads texture pixels at runtime.
- **Colliders**: measured `_generateColliders: 0` on GLB importers — mesh geometry changes would not affect collision (interactions use explicit Box/parent colliders), recorded for future work.

## Deliberate deferrals (measured justification)
- **Polygon decimation** (gltf-transform/meshoptimizer available and tested locally): deferred. Reasons: (a) meshes are 6.3% of the baseline payload — bounded benefit (~10–25 MB compressed); (b) the single largest mesh (BlackKitchen scan, 660k tris) leaves the initial payload entirely via Addressables; (c) this environment cannot render the scenes for the required before/after visual comparison (batch `-nographics`), so "no visible quality loss" could not be verified honestly. Candidates ranked for a follow-up pass with visual QA: metal_table (370k→~90k safe estimate), japanese_red_bridge, drum, LL_PhotoAlbum.
- **Material atlasing / drone.glb's 77 material slots**: documented, not changed — slot consolidation rewires the prefab/renderer mapping of an interactive exhibit and needs visual+interaction QA. Runtime cost is draw calls, not download size.
- **Mesh compression**: not enabled (can create visible seams/precision artifacts; benefit small relative to risk without visual QA).
