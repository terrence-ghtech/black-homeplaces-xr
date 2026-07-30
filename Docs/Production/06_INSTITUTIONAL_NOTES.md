# Institutional Deployment Notes — source material for release documentation

## Supported operating systems
- Windows 11 x64 (Windows 10 x64 expected to work; not a validated target)
- macOS on Apple Silicon (build targets ARM64; minimum macOS follows Unity
  6000.4 player defaults)
- Meta Quest (Android/OpenXR; physical validation pending — see deferred tests)

Explicitly unsupported: WebGL (legacy remnants remain but are not maintained
here), phones/tablets, PC VR, non-Quest headsets.

## Hardware requirements
**Pending.** Do not publish minimum specifications until the owner completes
`05_DEFERRED_TESTS_LOWEND_HARDWARE.md`. Design targets: Desktop Low ≈ 30 FPS
on older integrated-graphics laptops; Desktop Standard 30–60 FPS on modern
office machines; Desktop High for exhibition PCs with dedicated GPUs.

## Installation expectations
- Windows: copy the `BlackHomeplaces` folder; run `BlackHomeplaces.exe`. No
  installer. Unsigned (SmartScreen warning) until Authenticode signing is done.
- macOS: copy `BlackHomeplaces.app`; first launch of the unsigned dev build
  needs right-click → Open (or clear quarantine). Signing/notarization is a
  documented follow-up.
- Quest: sideload APK via adb (developer mode) until a store/App Lab release.

## Offline requirements & media hosting
The desktop and Quest editions run **fully offline**: all six exhibit videos
are packaged in StreamingAssets and the Black Kitchen loads from local
Addressables content. The Unity CCD remote base URL remains configured as a
fallback for any media not packaged; if CCD hosting is ever retired, only
`Assets/Resources/RemoteMediaConfig.asset` needs updating. External-link
exhibits (Kuula, ArcGIS StoryMaps, etc.) require internet and a system
browser; offline they fail gracefully in the browser, not in the app.

## Standard vs kiosk operation
- Standard: full menus, settings, quit — researchers/students/staff.
- Kiosk: fullscreen, fixed quality, restricted settings, hidden quit,
  inactivity reset. Enable via `-kiosk` launch argument or
  `mode.config.json` (`{"mode":"Kiosk"}`) in the data folder.

### Administrator controls (do not print in visitor-facing material)
| Action | Input |
|---|---|
| Quit application (kiosk) | Hold **Ctrl+Shift+Q** ~2 s |
| Admin settings panel (kiosk) | **Ctrl+Shift+F10** |
| Mode selection | `-kiosk`/`-standard` or `mode.config.json` |
| Kiosk timeout / fixed tier | `kiosk.config.json` or `-bcatKioskTimeout=` / `-bcatKioskQuality=` |

No passwords/credentials are used or stored.

## Log locations
See `02_BUILD_GUIDE.md` § Data locations (Player.log per OS, settings JSON,
smoke-test reports).

## Known limitations (this pass)
1. Windows runtime behavior validated by build + checklist only — no Windows
   machine was available in the implementation environment.
2. Quest runtime entirely deferred to physical testing (package + checklist
   provided).
3. No approved subtitle/transcript source content exists yet; the subtitle
   and transcript systems are functional but empty until content is provided.
4. Exhibit directory offers location descriptions, not a floor map (no
   approved map asset exists).
5. Windows build uses the Mono backend (IL2CPP requires building on Windows).
6. ~~`Assets/New Terrain.asset` "Unknown error" during builds~~ — **RESOLVED
   2026-07-29**: root cause was git CRLF normalization of the binary terrain
   asset (`*.asset text` in .gitattributes); a nulled Terrain-component
   reference in the working-tree scene was a downstream symptom (saving while
   the asset was unloadable). Both repaired and verified in-editor (see
   `15_TERRAIN_RECOVERY.md`). A post-repair native build could not be
   completed due to repeated external-drive disconnects during builds; the
   first successful build on stable storage should be visually spot-checked.
7. Desktop controller (gamepad) support was intentionally not added this pass
   (keyboard/mouse first, per plan).
8. Build-machine note: the project lives on an external USB SSD (T9) that has
   twice produced transient I/O dropouts under sustained load (2026-07-23 and
   during this pass's first Quest build attempt — hundreds of
   `CreateDirectory … failed` lines followed by an LMDB asset-database
   segfault in the editor). Builds succeed on retry, but long Android builds
   are safer run from an internal drive or after copying the project locally.

## Update procedure
1. Replace the application folder/.app/APK with the new build.
2. Settings and kiosk config persist per-machine in the user data folder and
   survive updates (schema-versioned, forward-migrating).
3. After changing exhibit media: re-run **BCaT → Production Setup → Run All**
   (repackages StreamingAssets + manifest) and rebuild all three targets so
   the Addressables content matches.
