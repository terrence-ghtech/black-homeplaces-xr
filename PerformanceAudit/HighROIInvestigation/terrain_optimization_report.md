# Terrain Optimization Report

## Current Terrain

| Setting | Value |
|---|---:|
| Scene | `Assets/BH_XR_MainScene.unity` |
| Object | `_Rendering/Terrain/Terrain_main` |
| TerrainData | `Assets/New Terrain.asset` |
| Size | `(200.000, 600.000, 200.000)` |
| Heightmap resolution | `513` |
| Alphamap resolution | `512` |
| Basemap resolution | `1024` |
| Detail resolution | `1024` |
| Draw instanced | `True` |
| Pixel error | `3.000` |
| Basemap distance | `100.000` |
| Detail distance / density | `20.000` / `1.000` |
| Tree distance | `100.000` |
| Billboard start | `50.000` |
| Max full-LOD trees | `50` |
| Terrain material | `Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat` |
| Shadow casting | `On` |
| Terrain layers | `7` |
| Tree prototypes | `2` |
| Detail prototypes | `8` |
| Estimated full-resolution triangles | `524,288` |

Terrain is a top-five bottleneck candidate for lower-powered WebGL browsers because it adds a large persistent draw surface, realtime terrain shadows are enabled, and vegetation/details are visible around the house. It is less dominant than shadow/render-count/texture memory, but it is high enough to tune before architectural remodelling.

## Recommended WebGL settings

| Change | Recommendation | Risk |
|---|---|---|
| Pixel error | Test `5-8` instead of `3` | Low to medium |
| Detail distance | Test `10-15` instead of `20` | Low near house if flowerbeds remain hand-placed meshes |
| Tree distance | Test `60-80` instead of `100` | Low to medium |
| Billboard start | Test `25-40` instead of `50` | Low if tree billboards are acceptable |
| Terrain shadows | Disable casting or bake terrain contribution where possible | Medium |
| Terrain layers | Keep visually important layers; compress/cap normal/mask maps for WebGL | Low |
| Distant terrain | Consider simplified mesh or billboard horizon only for unreachable/distant views | Medium |

## Quest settings

Quest can tolerate similar or slightly higher terrain distances if the native build memory budget allows it, but realtime terrain shadows should still be treated as expensive. Keep instanced terrain drawing on for both WebGL and Quest.

## Validation

Benchmark from front porch, backyard/pond, and second-floor exterior view. Capture terrain render time, visible triangles, shadow caster draws, GPU frame time, and screenshots for texture popping or billboard popping.
