# URP & Quality Profile Changes

Strategy: **one shared project, platform differences expressed only through Unity Quality levels + URP pipeline assets + texture platform overrides.** No per-platform scenes, prefabs, or code paths were added.

## Quality tier layout (after this pass)

| Tier (Quality level) | Index | Platforms (default) | URP asset |
|---|---|---|---|
| PC (High/Desktop) | 1 | Standalone, consoles | `Assets/Settings/PC_RPAsset.asset` (unchanged) |
| Mobile (WebGL / Balanced) | 0 | WebGL, iOS, WSA | `Assets/Settings/Mobile_RPAsset.asset` (tuned) |
| Quest (Standalone XR) | 2 | **Android** (Quest) | `Assets/Settings/Quest_RPAsset.asset` (**new**) |

The scene itself carries the shared "Balanced" baseline (terrain distances, LOD transitions, shadow flags); the tiers override only distances/AA/shadow budget.

## Exact changes

### Mobile_RPAsset.asset (WebGL tier)
| Setting | Before | After | Why |
|---|---|---|---|
| MSAA | 4x | 2x | 4x MSAA is a large GPU/bandwidth cost in browsers; 2x keeps edge quality at half the resolve cost |
| Shadow distance | 50 | 30 | Pairs with the shadow-caster cleanup; casters beyond 30 m contribute nothing visible at WebGL resolutions |

Kept: HDR on (visual tone preservation), SRP Batcher on, main-light shadows on (1024 map, 1 cascade), additional-light shadows off, soft shadows off, depth/opaque texture off.

### Quest_RPAsset.asset (new, cloned from Mobile)
| Setting | Value | Why |
|---|---|---|
| MSAA | 4x | MSAA is near-free on tiled mobile GPUs and essential for VR legibility |
| Shadow distance | 20 | Quest GPU budget; interiors read via receive-shadows + probes |
| Everything else | same as Mobile tier | shared maintenance surface |

### ProjectSettings/QualitySettings.asset
- **Mobile level** (WebGL default): `antiAliasing 4→2`, `shadowDistance 40→30`, `lodBias 2→1.2` (makes the new LOD Groups actually engage at their authored screen heights), and Unity 6 **terrain quality overrides** corrected — these were *worse* than the scene values before (they silently overrode the scene's terrain tuning on WebGL):
  - terrainPixelError 3 → **6**
  - terrainBasemapDistance 300 → **80**
  - terrainDetailDistance 40 → **14**
  - terrainTreeDistance 180 → **75**
  - terrainBillboardStart 80 → **35**
- **Quest level** (new, index 2, Android default): clone of tuned Mobile with `antiAliasing 4`, `shadowDistance 20`, `lodBias 1.2`, terrain overrides pixelError **7** / detail **12** / tree **65** / billboard **30**, pipeline → `Quest_RPAsset`.
- **PC level**: untouched (terrain overrides disabled there, so desktop uses the scene baseline: pixelError 4, detail 18, tree 90, billboard 45).
- `m_PerPlatformDefaultQuality`: `Android: 0 → 2` (Quest tier). WebGL stays 0, Standalone stays 1.

## Deliberately not changed
- HDR stayed on in all tiers — turning it off would alter the project's bright tropical daylight tone and post-processing response.
- Main-light realtime shadows stay enabled in every tier — the shadow win comes from removing 1,400+ unnecessary casters, not from deleting the feature.
- No render-scale reduction; text/archival readability is a project priority.
- PC tier untouched, including its depth/opaque textures (used by water/refraction-style effects).
- No baked-lighting conversion in this pass: the main scene has no lightmap bake setup today, and a blind rebake risks the memorial atmosphere. Recommended as a follow-up with art review (see Remaining_Risks_and_Recommendations.md).
