# Stage 6 — Memory & Resource Lifecycle Audit
Date: 2026-07-23
Method: full code review of runtime scripts (measured = code facts; browser-measured numbers land in the Stage 8 report).

## Scene transitions (measured, code)
- All transitions funnel through LoadingScene → `LoadingSceneController`, which calls `Resources.UnloadUnusedAssets()` + `GC.Collect()` **once per transition** (a controlled point — not per-frame; requirement satisfied as-is).
- All scene loads are `LoadSceneMode.Single` → previous scene objects are destroyed by Unity on load (both directions Main House ⇄ Black Kitchen).
- **NEW**: remote Addressables scenes store their load handle in `AddressableSceneHandleStore`; the handle (and thus the downloaded bundle memory) is released right after the *next* scene finishes loading — returning from the Black Kitchen no longer retains its asset bundle. Duplicate loads prevented by the existing `SceneTransitionState.IsTransitionInProgress` guard + `loadStarted` flag.

## Video players (measured, code — includes Stage 4 changes)
- Prepared only on open (WebGL); trigger-proximity prefetch is non-WebGL only (Quest UX preserved).
- On close/trigger-exit/disable: `Stop()`, URL cleared (releases decoder + network stream), RenderTexture cleared; the one code-created RenderTexture (`MediaVideoController`, 1280×720) is `Release()`d and `Destroy()`ed on destroy.
- **NEW**: `loopPointReached` → `Stop()` on non-looping players (resources released when playback ends, popup stays open).
- **NEW**: `VideoExhibitCoordinator` — at most one video exhibit open/buffering at any time.
- AudioSources: `Stop()` + `UnloadAudioData()` where safely non-streaming (`StopAndUnloadIfSafe`, pre-existing, verified).

## Persistent objects (measured, code)
- **No `DontDestroyOnLoad` usage anywhere** in project runtime code — no persistent GameObjects, so no duplicate-manager accumulation is possible.
- Cross-scene state uses plain statics only: `SceneTransitionState` (cleared per transition), `BlackKitchenSessionState.ExitReflectionPlayed` (intentional one-shot flag), `BlackKitchenInteractionGate` (frame-scoped: keyed to `Time.frameCount`, self-invalidating), `RemoteMediaConfig.Instance` (a tiny config SO), `AddressableSceneHandleStore` (releases on transition). All verified sound for WebGL's no-domain-reload model.

## Measured at runtime (see 08 report for numbers)
Startup memory, steady-state, and post-transition browser memory are measured in Stage 8 via CDP. The following prompt-listed checkpoints could **not** be automated and were **not performed** (recorded as such): walking the full main house, entering the Black Kitchen through the portal in a real input session, playing several videos sequentially. These need a manual QA pass; the code paths that govern their memory behavior are the ones verified above.
