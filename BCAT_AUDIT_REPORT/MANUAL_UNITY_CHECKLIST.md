# Manual Unity Checklist

1. Open Build Report Inspector or rebuild WebGL with `webGLAnalyzeBuildSize` enabled and export packed asset data.
2. For every P0 video row, inspect all `VideoPlayer` components and scripts using that filename.
3. In Unity Project view, run dependency checks for top `GHOST_ASSET_CANDIDATES.csv` entries before moving/deleting anything.
4. Inspect each enabled build scene for inactive/backup/test objects that hold large references.
5. Verify Resources folder contents and remove only after runtime code search confirms no `Resources.Load` dependency.
6. Inspect texture import settings for top `TEXTURE_AUDIT.csv` rows: max size, mipmaps, alpha, compression, WebGL override, Read/Write.
7. Inspect audio import settings for top `AUDIO_AUDIT.csv` rows: load type, preload, compression, mono/stereo.
8. Inspect BlackKitchen GLB/model importer for read/write, mesh compression, embedded textures, cameras/lights, material count.
9. Run WebGL Play/Build smoke tests for all media exhibits after any optimization.
10. Compare browser memory and network waterfall before/after each phase.
