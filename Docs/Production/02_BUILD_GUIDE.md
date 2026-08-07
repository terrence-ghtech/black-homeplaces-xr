# BCaT Build Guide — Windows · macOS (Apple Silicon) · Meta Quest

## Requirements

- Unity **6000.4.5f1** with modules: Mac Build Support (built-in on macOS),
  **Windows Build Support (Mono)**, **Android Build Support** (bundled
  OpenJDK/SDK/NDK). All three are installed on the current build machine at
  `/Applications/Unity/Hub/Editor/6000.4.5f1`.
- Packages restore automatically from `Packages/manifest.json`
  (Addressables 2.9.1, Input System 1.19, URP 17.4, XRI 3.4.1, OpenXR 1.16.1).
- Note: Windows **IL2CPP** cannot be cross-compiled from macOS; Windows builds
  from this machine use the Mono scripting backend (fine for the institutional
  desktop edition; an IL2CPP Windows build would require building on Windows).

## One-time project setup (already applied by this pass)

Menu: **BCaT → Production Setup → Run All**, or batch:

```
Unity -projectPath <project> -batchmode -nographics -quit \
  -executeMethod BCaT.EditorTools.ProductionProjectSetup.RunAll -logFile setup.log
```

Creates/refreshes MainMenuScene, registers the build scene list
(MainMenuScene → BH_XR_MainScene → LoadingScene, Black Kitchen disabled/
Addressables), sets application identifiers (`org.bcatlab.blackhomeplaces`),
resizable window, Android minSdk 29, copies the six production videos into
`Assets/StreamingAssets`, and records them in
`RemoteMediaConfig.packagedFileNames`.

## Architecture gate (runs automatically)

Since 2026-08-07 every player build first runs `BCaTArchitectureValidator`
(`BCaTBuildValidationStep`, `IPreprocessBuildWithReport`). Any Error-severity
finding **aborts the build** with the rule id and the offending object path, and
the report is written to `Docs/Production/ARCHITECTURE_VALIDATION.md`.

**No manual hierarchy change is ever required before building.** Platform rig
selection, EventSystem input-module assignment and development-aid stripping all
happen automatically — `ScenePlatformBinding` at runtime and
`BCaTEditorOnlyStripper` at build time. If a scene would need hand-fixing, the
build fails instead of shipping.

Run it standalone at any time:

```
Unity -projectPath <project> -batchmode -nographics -quit \
  -executeMethod BCaT.EditorTools.BCaTArchitectureValidator.RunBatch -logFile v.log
```

Exit codes: 0 pass · 1 error-severity failure · 2 warnings only.
Escape hatch for a deliberate diagnostic build from a known-broken tree:
`-bcatSkipArchitectureValidation` (the resulting build is not validated).

## Addressables profile safety

The pipeline now **sets and asserts** the Addressables profile named by the
target's `BCaTPlatformProfile` before `BuildPlayerContent`, instead of only
logging whichever profile happened to be active. A Windows build made straight
after a Quest build can no longer pick up the wrong profile and ship a catalog
that does not match its bundles. Development builds additionally check
`Addressables.RuntimePath` against the expected bundle folder at startup
(`StandaloneOSX` / `StandaloneWindows64` / `Android`).

## Testing a platform in the Editor

`BCaT → Platform Test Mode`: **Auto** · **Desktop** · **Quest XR (Simulated)** ·
Quest XR (Device). Quest XR (Simulated) activates the Quest branch and the XR
Device Simulator without a headset — the mode to use for Quest prompt wording,
rig activation, interaction routing and canvas placement. Exit and re-enter Play
Mode after switching. CI equivalent: `-bcatPlatform=Desktop|Quest`.

Quest XR (Device) additionally requires a Standalone entry in
`Assets/XR/XRGeneralSettingsPerBuildTarget.asset` with
`InitManagerOnStart = false`. That entry is intentionally absent: with automatic
initialization it would flip `XRSettings.isDeviceActive` in ordinary desktop Play
Mode on any machine with an OpenXR runtime installed.

## Building

Each method builds **Addressables content first** (so the Black Kitchen bundle
always matches the optimized meshes), then the player. Development builds by
default; add `-bcatRelease` for non-development. Outputs land in `Builds/`
with a `BuildSummary_<target>.txt` beside them.

macOS (Apple Silicon only):
```
Unity -projectPath <project> -batchmode -nographics -quit -buildTarget OSXUniversal \
  -executeMethod BCaT.EditorTools.ProductionBuildPipeline.BuildMacOS -logFile build_mac.log
→ Builds/macOS/BlackHomeplaces.app
```

Windows 11 x64:
```
Unity -projectPath <project> -batchmode -nographics -quit -buildTarget Win64 \
  -executeMethod BCaT.EditorTools.ProductionBuildPipeline.BuildWindows -logFile build_win.log
→ Builds/Windows64/BlackHomeplaces/BlackHomeplaces.exe   (zip the folder to distribute)
```

Meta Quest (APK, IL2CPP/ARM64, OpenXR + Meta Quest feature):
```
Unity -projectPath <project> -batchmode -nographics -quit -buildTarget Android \
  -executeMethod BCaT.EditorTools.ProductionBuildPipeline.BuildQuest -logFile build_quest.log
→ Builds/Quest/BlackHomeplaces-Quest.apk
```

Menu equivalents: **BCaT → Production Builds → …**

### Black Kitchen bundle rebuild only

Addressables rebuild happens inside every player build. To rebuild content
alone: open **Window → Asset Management → Addressables → Groups → Build → New
Build → Default Build Script** with the desired platform active. The
BlackKitchen group now uses **local** build/load paths
(`Library/com.unity.addressables/aa/<target>` packaged into the player), so
there is no CDN upload step for desktop/Quest. The old WebGL-era remote
CCD profile values remain in the Default profile for reference; only the
group's path selection changed.

## Runtime flags and modes

| Flag | Effect |
|---|---|
| `-kiosk` / `-standard` | Select application mode (overrides mode.config.json) |
| `-bcatKioskTimeout=300` | Kiosk inactivity seconds |
| `-bcatKioskQuality="Desktop Low"` | Kiosk fixed quality tier |
| `-bcatSmokeTest [n]` | Automated Black Kitchen enter/exit lifecycle test (n cycles), writes report + screenshots to persistentDataPath/BCaT, exits 0/2 |
| `-bcatAddressablesLog` | Verbose Addressables lifecycle logging in release builds |
| `-bcatRelease` (build time) | Non-development player build |

## Data locations

| Item | Path |
|---|---|
| Settings | `<persistentDataPath>/BCaT/settings.json` |
| Mode / kiosk config | `<persistentDataPath>/BCaT/mode.config.json`, `kiosk.config.json` |
| Smoke test reports | `<persistentDataPath>/BCaT/smoketest_*.txt`, `smoke_screens/` |
| Player log (macOS) | `~/Library/Logs/BCaT Lab/Black Homeplaces_ The XR House/Player.log` |
| Player log (Windows) | `%USERPROFILE%\AppData\LocalLow\BCaT Lab\Black Homeplaces_ The XR House\Player.log` |
| Crash dumps (Windows) | same LocalLow folder (`Crashes/`) |

persistentDataPath: macOS `~/Library/Application Support/BCaT Lab/Black
Homeplaces_ The XR House`; Windows the LocalLow folder above; Quest
`/sdcard/Android/data/org.bcatlab.blackhomeplaces/files`.

## Quest install for physical testing (project owner)

```
adb install -r Builds/Quest/BlackHomeplaces-Quest.apk
adb logcat -s Unity            # runtime log while testing
```
Enable developer mode on the headset first. Full test protocol:
`Docs/Production/04_DEFERRED_TESTS_QUEST.md`.

## Future signing (documented, intentionally not performed)

- **macOS**: Developer ID signing + notarization (`codesign --deep --options
  runtime`, `notarytool submit`) before public distribution; unsigned dev
  builds require right-click→Open or `xattr -dr com.apple.quarantine`.
- **Windows**: Authenticode signing of the .exe to avoid SmartScreen warnings.
- **Quest**: the APK is debug-signed by Unity; store distribution would need a
  Meta developer account + App Lab/store signing.
- No signing credentials exist in or should ever be added to this project.
