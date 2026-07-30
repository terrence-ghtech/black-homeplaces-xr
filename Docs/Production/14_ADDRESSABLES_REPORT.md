# Addressables Lifecycle Report

## Groups & profiles
- Profile: **Default** (only profile). Local paths = player-packaged
  (`Library/com.unity.addressables/aa/<target>` → StreamingAssets); the
  WebGL-era Remote values (Unity CCD badge URL / `ServerData/[BuildTarget]`)
  remain defined for reference/fallback but are no longer used by any group.
- **Default Local Group**: local paths (unchanged).
- **BlackKitchen_Remote** (name kept for asset-GUID stability): build/load
  paths changed **Remote → Local.BuildPath/Local.LoadPath** this pass.
  Rationale: institutional desktop/Quest must run offline; CCD never had
  content for the three native targets; the optimized-mesh change forced a
  content rebuild regardless. LZ4, PackTogether, IncludeInBuild preserved.

## Ownership model
| Content | Owner | Load point | Release point |
|---|---|---|---|
| BlackKitchen_MemoryScene (+ its mesh/audio dependencies, packed in its scene bundle) | `AddressableSceneHandleStore` (named owner reported to the registry) | `LoadingSceneController.LoadAddressableScene` when the portal transition targets the kitchen | `AddressableSceneHandleStore.ReleaseIfHeld` — invoked by `LoadingSceneController` after the next built-in scene finishes loading (single-mode load has already destroyed the scene objects; the release frees the bundle) |

All creates/completions/releases flow through `AddressablesHandleRegistry`
(duplicate-load, release-without-ownership, and leaked-handle detection;
active-count exposure) and `AddressablesLifecycleLog` (verbose in dev builds,
`-bcatAddressablesLog` in release). Error handling and repeat-entry behavior
preserved from the validated LoadingSceneController (failed download → visitor
message → automatic return to the house; walking back into the portal
retries).

## Black Kitchen rebuild status
Rebuilt for all three native targets in this pass — Addressables content is
built at the start of every player build (`ProductionBuildPipeline`), so the
bundles always match the optimized meshes. Baseline (pre-change) builds had
produced remote-path bundles into `ServerData/StandaloneOSX|StandaloneWindows64`;
final builds package the bundle inside the player instead. `ServerData/`
retains only historical WebGL-era output.

## Desktop repeat-entry validation
Automated via the player smoke test (`-bcatSmokeTest n`): repeated
house→kitchen→house cycles through the real portal transition flow, asserting
after every return: exactly 1 loaded scene, no duplicate CharacterController
rigs, `AddressablesHandleRegistry.ActiveCount ≤ 1`, and stable managed/
reserved memory; final sweep runs UnloadUnusedAssets + one GC and dumps any
surviving handles. Results are recorded in the final report (§ Validation
Evidence) with the generated report file.

## Remote & offline behavior
Native targets: fully local — no catalog or bundle network requests at
runtime; offline entry/exit of the Black Kitchen works by construction and is
exercised by the smoke test (machine-level offline runs are in checklist §G).
Remote catalog remains disabled (`m_BuildRemoteCatalog: 0`).

## Quest validation steps awaiting physical testing
`04_DEFERRED_TESTS_QUEST.md` §E: repeated on-device kitchen cycles, load
times, memory via `dumpsys meminfo`, offline entry with Wi-Fi disabled.
