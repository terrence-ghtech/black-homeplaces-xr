# Implementation Plan

## Category A - Required for reliable WebGL

1. Lock down remote media delivery. Confirm every video filename in `Assets/Resources/RemoteMediaConfig.asset` resolves to a valid CDN URL, then build WebGL with videos excluded from the player payload unless a tiny fallback is intentional.
2. Reduce realtime shadow work. Start with small props, flowers, vegetation, duplicate exhibit props, and Italian Town modular pieces. Keep shadows only where they carry the scene composition.
3. Reduce draw calls. Batch/merge static repeated architecture and vegetation by material, atlas repeated materials, and mark appropriate home/envelope objects static for batching and occlusion.
4. Add WebGL texture import overrides. The main scene references about 568 MiB / 596 MB of texture runtime memory. Cap secondary props and repeated material maps, especially one-off 2048 textures.
5. Produce a real WebGL build report after the above. Enable `webGLAnalyzeBuildSize` for the measurement build, profile in Chrome, and capture Memory Profiler snapshots.

## Category B - Valuable optimizations

1. LOD or decimate the top geometry assets: `metal_table_asset.glb`, `LL_PhotoAlbum.glb`, `glass_fish.glb`, `japanese_red_bridge.glb`, and `drum.glb`.
2. Tune terrain rendering. The main terrain grid estimate is 524k triangles; increase WebGL terrain pixel error and reduce detail/tree distances until the house route still looks correct.
3. Optimize Italian Town architecture selectively. Merge/static-batch repeated wall/pillar/roof/floor modules and disable unnecessary shadows before replacing meshes.
4. Review canvases. Main scene has 39 canvases; ensure world-space UI is event-driven and not constantly rebuilding.

## Category C - Low return

1. Rebuilding all walls/floors/ceilings from primitives is not justified by the current data. The Italian structural subset is roughly 108.6k triangles out of 2.73M renderer triangles.
2. Converting nearby structural architecture to sprites is not recommended. It would create depth, parallax, lighting, and collision artifacts.
3. Remodelling 300-triangle wall_base pieces one by one is lower value than reducing renderer/material/shadow overhead.

## Category D - Do not change

1. Do not optimize disabled sample/demo scenes for WebGL unless they are added to Build Settings or Addressables.
2. Do not replace culturally important close-view artifacts with flat impostors. Use LOD/decimation with screenshot review instead.
3. Do not spend time on `LoadingScene` geometry; it has no renderers.

## Validation checklist

1. Compare a baseline WebGL build against an optimized build in the same browser, same resolution, cleared cache.
2. Capture: download size, initial memory, peak memory, frame time, batches, SetPass calls, visible triangles, shadow caster draws, and scene load time.
3. Use Chrome Performance/Memory plus Unity Profiler connected to WebGL where available.
4. Validate cultural/visual regressions with fixed screenshots at the porch, first floor, second floor roof/fence area, RI exhibit, LindaLeaks photo album, 9Night drum, Sewing Room, and BlackKitchen entry/exit.
