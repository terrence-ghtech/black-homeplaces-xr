# Remaining Risks and Recommendations

## Risks introduced by this pass (ranked, with mitigations)

1. **QEM LOD visual quality (medium).** LOD1/LOD2 meshes are machine-generated. Normals were recalculated, so smooth-shaded assets (glass fish, drum) may shade slightly differently at LOD1+. CrossFade hides transitions, and LOD0 is always the untouched original at close range. *Mitigation:* the 6 top assets need one screenshot pass at mid distance; transition heights are data on the LODGroup and trivially adjustable.
2. **Bridge rail collision fidelity (low-medium).** Rails use ~12% collision meshes. Deck/steps are untouched, so fall-through is not possible on the walking surface; worst case is a hand/controller passing slightly into a rail. *Mitigation:* one traversal test.
3. **Combined flowerbeds (low).** Geometry, material, and placement identical; risk is limited to anything that referenced individual flower renderers (none found) and to LOD cross-fade shader variance (none — no LODGroup on chunks). Originals remain in-scene disabled for instant rollback.
4. **Static-flag side effects (low).** 261 architecture objects became Batching/Occluder/Occludee static. Static batching raises memory slightly (vertex duplication at build). If any flagged object is secretly moved at runtime by a script, it would stop moving visually — none found referencing these subtrees.
5. **Texture 1024 caps on Android/Quest (low).** Same caps already shipped on WebGL since July 23 without issue; archival/exhibit content excluded.
6. **BK bundle hash drift (operational).** The deployed WebGL player still points at the old (valid) bundle. The next Addressables build must be uploaded to UCD before or with the next player deploy.

## Remaining bottlenecks (in expected impact order)

1. **Close-range triangle load is intentionally unchanged** (~2.43M effective LOD0 in the full scene). If close-up GPU load is still too high on low-end WebGL after benchmarking, the next lever is lowering LOD0→LOD1 transition heights (data-only change), starting with metal_table and bridge.
2. **No baked lighting.** The scene is fully realtime-lit. A selective bake (static architecture + light probes for movables) would cut the remaining main-light shadow cost substantially. Needs art supervision to preserve the memorial tone — do after the WebGL benchmark, not before.
3. **Canvas update behavior** was only statically audited (39 canvases, all world-space prompt-scale, no duplicate EventSystems, no full-screen transparent overlays found). If profiling shows `Canvas.BuildBatch` spikes, split any frequently-updated text onto its own small canvas.
4. **Terrain shadows remain on** — next Quest-specific lever if GPU-bound (flagged in Terrain_Changes.md).
5. **Embedded GLB duplicate materials** (drone 77×, morten-s) cannot be consolidated as assets; SRP Batcher mostly absorbs this. A GLB re-export would be needed to truly merge them — not worth it now.

## Recommended next steps, in order
1. Rebuild Addressables (WebGL target) → upload `ServerData/WebGL` to UCD → verify BK entry loads.
2. Produce a WebGL build with `webGLAnalyzeBuildSize`, run the benchmark matrix (`PerformanceAudit/HighROIInvestigation/benchmark_test_matrix.md`) in Chrome: porch, flowerbeds, backyard/pond, bridge, Sewing Room, RI, LindaLeaks, 9Night, Black Kitchen, Privacy Law. Record FPS/frame times/batches/SetPass/memory against `Before_After_Performance_Metrics.csv`.
3. 15-minute Editor Play Mode functional sweep (movement, portals, album, oven/pot, exit modal, Privacy Law UI, video playback).
4. Visual QA the six LOD assets + flowerbeds + terrain billboards (checklist in All_Production_Changes.md).
5. Android/Quest: switch target, build once, verify the Quest quality tier engages (`QualitySettings.GetQualityLevel()==2`) and ASTC texture sizes; only then claim Quest readiness.
6. Optional follow-ups: selective lightmap bake; SecondFloor books atlas only if SetPass-bound; delete demo packs from the repo if hygiene matters.

## Explicit non-changes (by design — do not "fix" these)
- LOD0 of every culturally significant artifact is the original mesh.
- No terrain flattening/replacement; no heightmap resample.
- No HDR/post/tone changes; bright tropical daylight and exhibit mood preserved.
- Duplicate .mat files kept on disk (unreferenced) rather than deleted.
- Original flower GameObjects kept (renderers disabled) for authoring/rollback.
- No atlases (documented in Atlas_Changes.csv). No baked lighting yet.
