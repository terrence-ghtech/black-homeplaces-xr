# Build Settings Audit

| Setting | Value | Size/memory note |
|---|---:|---|
| `webGLMemorySize` | `32` |  |
| `webGLExceptionSupport` | `1` | Exceptions increase wasm/runtime cost. |
| `webGLDataCaching` | `1` |  |
| `webGLDebugSymbols` | `0` |  |
| `webGLCompressionFormat` | `0` | Likely Brotli; verify in Unity UI. |
| `webGLDecompressionFallback` | `0` |  |
| `webGLInitialMemorySize` | `532` | Large initial heap can affect browser memory. |
| `webGLMaximumMemorySize` | `2048` | High maximum heap permits larger memory growth. |
| `webGLMemoryGrowthMode` | `2` |  |
| `webGLAnalyzeBuildSize` | `0` | Enable for future exact build composition reports. |
| `webGLThreadsSupport` | `0` |  |
| `webGLWebAssemblyBigInt` | `0` |  |
| `webGLTemplate` | `APPLICATION:Default` |  |
| `stripEngineCode` | `1` |  |
| `activeInputHandler` | `1` |  |
| `usePlayerLog` | `1` |  |

Enabled build scenes:

- `Assets/BH_XR_MainScene.unity`
- `Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity`
- `Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity`

Package concern: HDRP is present alongside URP; it may not ship if unused but increases project/package surface.
