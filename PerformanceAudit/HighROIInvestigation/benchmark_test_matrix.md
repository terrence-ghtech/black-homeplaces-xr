# Benchmark Test Matrix

Run each test in a clean WebGL build, same browser, same resolution, cleared cache, with the browser devtools network cache disabled for cold-load tests.

| Area | Camera/player location | Captures | Success criteria |
|---|---|---|---|
| Main scene initial load | Default spawn / entry | Download size, time to first frame, WASM heap, browser peak memory, Unity memory | No memory failure; stable first frame under target budget |
| Front home / porch | Looking at house front and flowerbeds | FPS, main-thread, render-thread, GPU frame time, batches, SetPass, shadow caster draws, visible triangles | Shadow/flower optimizations reduce draws without obvious loss |
| Backyard / pond | Looking across pond and bridge | Terrain render time, visible triangles, shadows, batches, GPU frame time | Terrain/bridge LOD changes reduce GPU cost |
| Sewing Room | Quilt/bed/sewing-machine area | Texture memory, shadow draws, visible triangles, audio start latency | No audio/video stutter; furniture shadows acceptable |
| RI exhibit | Fish, TV, domino, photo album | Visible triangles, batches, video start latency, decoder memory | LOD/high-poly reductions preserve artifact look |
| LindaLeaks exhibit | Photo album and camera preview | Visible triangles, texture memory, video load | Photo album optimization does not damage close view |
| 9Night exhibit | Drum and soundscape | Visible triangles, audio memory/streaming, shadow draws | Drum LOD preserves silhouette; soundscape streams reliably |
| Black Kitchen | Addressable load and first interaction | Remote bundle size, scene load time, peak memory, FPS, audio interactions | Remote scene loads without main-scene memory crash |
| Privacy Law exhibit | Hologram/blueprint view | Transparent overdraw, UI/canvas rebuilds, batches | UI/effects do not dominate frame time |

## Metrics to record

Baseline FPS, main-thread frame time, render-thread frame time, GPU frame time, batches, SetPass calls, shadow caster count, visible triangles, texture memory, total memory, scene load time, browser memory peak, and network transferred bytes.
