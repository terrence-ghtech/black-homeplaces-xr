# Stage 2 — Texture Optimization: Actions & Measured Results
Date: 2026-07-23

## Root cause (measured)
UnityGLTF-imported GLB embedded textures shipped **uncompressed** (RGB24/RGBA32) because the GLTF importers had `_textureCompression: None(-50)`; several also kept CPU-side copies (`_texturesReadWriteEnabled: 1`). The single BlackKitchen scan texture was 8192×8192 RGB24 = 268 MB packed by itself. Texture2D was 91% (1,960.7 MB) of the baseline 2,153.5 MB payload.

## Actions (all reversible; originals preserved)
1. **GLB importer compression** — 20 in-build GLBs: `_textureCompression: None → Normal` (platform automatic format → DXT on WebGL), `_texturesReadWriteEnabled: 1 → 0`. (4 GLBs were already remediated with compression=Best previously — untouched; 2 legacy-format metas without these fields left as-is: chest.glb, chest_chair.glb, ~4 MB each.) Mipmaps verified retained post-compress.
2. **Embedded texture resize** (gltf-transform 4.4.1, Lanczos3; originals → `GLB_Originals_Backup/`, same path/GUID preserved so every scene/prefab reference is intact):
   - BlackKitchen_ScannedEnvironment.glb: 8192→4096 max (34.5→20.6 MB source). Documented deviation from the 2048 hero cap: this one atlas textures an entire room; 2048 would visibly soften every surface. 4096+DXT1 ≈ 11.2 MB packed (was 268 MB).
   - modern_scandinavian_kitchen_island.glb: 4096→2048 max (20.1→10.0 MB source).
3. **WebGL-only platform caps** — 147 third-party environment-pack textures >1024 capped at 1024 for WebGL only (Quest/desktop imports untouched): full list in `webgl_texture_overrides.tsv` (DevDen, Furniture Mega Pack, Idyllic, TerrainSampleAssets, SubstanceAssets/GamePieces 4096→1024, Shaded Spectrum, HyTeKGames, danthaigames, Animated Tropical Vegetation, Coconut Palm, picture-frame).
4. **Deliberately untouched**: BCaT documentary photographs and exhibit text/plaques (LindaLeaks images incl. 11.3 MB LL_map.png, Meshell photographs, Ri content) — prompt requires visual readability testing before reducing; not automatable here. This is the largest *remaining* texture pool (BCaT_assets 432.5 MB packed).

## Measured results (BuildReport, optimized build vs baseline build)
| Metric | Baseline | Optimized | Δ |
|---|---|---|---|
| .data.br (compressed) | 705,833,122 B | **282,469,010 B** | **−60.0%** |
| Uncompressed payload | 2,153.5 MB | 848.6 MB | −60.6% |
| Texture2D packed | 1,960.7 MB | 694.6 MB | −64.6% |
| Mesh packed | 135.7 MB | 104.9 MB | −22.7% (BK scan mesh now remote) |
| GLB inventory est. texture memory | ~1,180 MB | ~193 MB | −84% |
| Build duration | 1,694.8 s | 973.9 s | −43% |

Per-GLB (estimated GPU/packed texture MB, before → after): BlackKitchen scan 268.4→11.2 · bed 279.6→55.9 est/33.7 packed · kitchen island 356.4→51.0 est/29.5 packed · flowerbarrel 145.4→28.0 · old_sewing_box 55.9→11.2 · glass_fish 9.8→2.8 · drum 8.4→1.4 · LL_AntiqueCamera 18.2→3.5 · japanese_red_bridge 28.0→5.6.

## Validation after the pass (measured)
- 0 compile errors; scenes: MainScene 1,982 renderers / 0 missing meshes / 0 missing scripts; BlackKitchen 27 renderers clean; LoadingScene clean.
- 5 null material slots in MainScene: **pre-existing since the initial import** (DevDen Pen.FBX external material remap targets GUID 8e724108404e457439eafe60ebeb4acc which does not exist in any project state, including production today). Not caused or altered by this work.
- Runtime texture-pixel reads: none in project code (verified) — Read/Write off is safe.
- Visual appearance: automated pixel-level comparison was **not** possible in this environment (batch `-nographics`); risk concentrated on the two resized GLBs. DXT compression of formerly-uncompressed textures is the same conversion Unity applies to all standard imports and is already live on 4 previously-remediated GLBs in production.
