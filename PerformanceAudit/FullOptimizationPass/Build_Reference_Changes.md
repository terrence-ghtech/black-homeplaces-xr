# Build Reference Cleanup — Findings and Changes

Raw inventory: `build_reference_data.md` (generated in-editor during the pass).

## Verdict: the build reference surface is already clean. **No deletions were made.**

| Channel | State | Action |
|---|---|---|
| Build Settings scenes | `BH_XR_MainScene` (on), `LoadingScene` (on), `BlackKitchen_MemoryScene` (off — loaded via Addressables) | None needed; correct |
| Preloaded assets | Empty | None |
| `Assets/Resources/` | Only `RemoteMediaConfig.asset` (1 KB) and `UnityGLTFSettings.asset` (14 KB) | None; both required at runtime |
| StreamingAssets | Folder absent — no media ships in the payload | None |
| Shader variant collections | None | None |
| Addressables | 2 groups: `Default Local Group` (0 entries), `BlackKitchen_Remote` (1 entry: the BK scene) | None; matches the isolated-load design |
| Package Resources | `Packages/org.khronos.unitygltf/Runtime/Resources/Standard (Specular setup).mat` is always included by UnityGLTF; ~negligible size | Documented, not removed (package-owned) |

## Demo/sample content on disk (not in the build)
Large sample packs (`Animated Tropical Vegetation/DemoScene`, XR Interaction Toolkit samples, asset-pack demo scenes) exist in `Assets/` but are **not** referenced by build scenes, Resources, Addressables, or preloaded assets. Unity only ships referenced assets, so they cost editor import time and repo size — not build size. Per the preserve-source policy they were left in place; if repo hygiene matters later, deleting them is a separately reviewable change (they are captured in snapshot commit `3893523`).

## What was proven, and how
- Scene list and enabled flags read from `EditorBuildSettings`.
- Preloaded assets read from `PlayerSettings.GetPreloadedAssets()`.
- Resources/StreamingAssets enumerated on disk and via AssetDatabase.
- Addressables entries enumerated from `AddressableAssetSettings` groups.
- No `Resources.Load` of demo content: the only Resources consumers are `RemoteMediaConfig.Instance` and UnityGLTF settings.
