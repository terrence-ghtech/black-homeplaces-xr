# Stage 4/5 — Dependency Analysis & Asset Classification
Date: 2026-07-23
Data: webgl-temp-audit-reports/baseline/{dependency_roots.txt, asset_inventory.tsv, classification.tsv, classification_summary.txt, crossref_hits.tsv, scenes_using_assets.tsv}

## Method (measured, Unity APIs + static scans)
1. **Roots** (discovered in Stage 1, not assumed): the 3 enabled scenes; PC/Mobile URP pipeline assets; StarterAssets.inputactions (project-wide input actions per EditorBuildSettings config objects); Assets/XR settings tree; every file under any Resources folder; every file under StreamingAssets; preloaded assets (empty).
2. **Closure**: `AssetDatabase.GetDependencies(roots, recursive:true)` per root group; every asset tagged with the root groups that reach it (asset_inventory.tsv, 5,823 assets).
3. **Included set**: BuildReport `packedAssets` from the baseline build (7,997 entries) + StreamingAssets copy.
4. **Runtime string loading**: full code sweep — no Resources.Load, no UnityWebRequest, no AssetBundles; SceneManager string loads resolve to the 3 enabled scenes; VideoPlayer URLs resolve to the 6 StreamingAssets files; single Shader.Find in XRI sample resolves to package/pipeline shaders. No JSON/config-driven asset paths.
5. **Cross-reference for candidates** (crossref_candidates.py): GUID scan of every kept serialized asset, every kept .meta (importer remaps), ProjectSettings, embedded package org.khronos.unitygltf; plus `"Assets/…"` path-string scan of every .cs file. 3,498 files scanned.

## Classification result (5,823 inspected assets)
| Category | Definition | Count | Source MB |
|---|---|---|---|
| A | Required by current production build (reachable from roots / packed / always-packed roots) | 977 | 2,104.2 |
| B | Included in build but not required by discovered dependency graph | 29 | 0.1 |
| C | In project, excluded from build | 4,712 | 5,036.2 |
| D | Insufficient evidence (scripts not in graph, editor tooling, protected config) | 105 | 4.6 |

### Category B detail (all 29)
- 27 are **MonoScript stubs** (~68–192 bytes each): runtime scripts compiled into Assembly-CSharp and packed because their types are used at runtime, but never serialized on a scene object (e.g. SceneTransitionState.cs, RuntimeMediaPaths.cs, QuiltVideoPopUp.cs, XRI sample runtime scripts). **Required code — not deletable** (protected by the script rule).
- 2 settings assets: Assets/Settings/UniversalRenderPipelineGlobalSettings.asset and DefaultVolumeProfile.asset — packed via GraphicsSettings' SRP default-settings reference, which was not one of my root groups. **Evidence shows they are required by project settings → effectively Category A.** Protected path; retained.
- Net deletable Category B payload: **0 bytes**.

### Consistency checks (measured)
- Every packed asset path under Assets/ was reachable from the root closure except the 29 Category B entries above → Unity's dependency data and the BuildReport corroborate each other.
- Baseline .data.br (705,833,122 B) differs from the production phase-9 artifact (705,798,466 B) by +0.005%; wasm/framework/loader byte-identical → the audited build is the production build.

## Notable evidence-based findings
- `Assets/BH_XR_MainScene/LightingData.asset` is a **stale bake**: the scene's `m_LightingDataAsset` points to the null GUID (0000…f000…), not to this asset.
- `com.unity.test-framework.performance` auto-generates `Assets/Resources/PerformanceTestRunInfo.json` + `PerformanceTestRunSettings.json` at editor startup; being in Resources they are packed into every build (small). Deleting them is pointless (regenerated); classified A-by-location, flagged as a known contamination of Resources.
- All 6 StreamingAssets videos are referenced by production scenes/prefabs/code (Category A).
- The XRGeneralSettings tree contains Android-only loaders; it exists in the build config but contributes nothing to the WebGL runtime payload.

## Cross-reference hits (candidates → retained)
Only 2 of 4,712 Category C candidates were referenced by kept files:
1. Assets/RealBlend/Art/Shaders/VertexColorPreview.shader — path-string in RealBlend editor tooling.
2. Assets/TutorialInfo/Icons/Water Shader icon.PNG — referenced by a kept .meta (script icon).

Name/prefix-based tooling sweep additionally protects (see 07 report): BlackKitchen, PrivacyLawExhibit, LindaLeaks, Meshell articles/Pages, BakedVertexPaintMeshes, RealBlend/VertexColorPalettes folder trees.
