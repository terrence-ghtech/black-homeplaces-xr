# Quality Profile Report

Structure after this pass: **Desktop Low (0) · Desktop Standard (1, Standalone
default) · Desktop High (2) · Quest (3, Android default)**. Mapping from the
previous tiers (all previous values documented in `00_BASELINE_STATE.md`
before any change): Mobile→Desktop Low, PC→Desktop Standard, Quest→Quest
(unchanged), Desktop High is new. No phone/tablet/WebGL/other-headset tier was
added; WebGL remnant default points at Desktop Low (closest successor of its
previous Mobile default).

⚠ Baseline defect fixed: the previous `ProjectSettings/QualitySettings.asset`
was malformed YAML — the Quest entry had no list-item marker (its keys
continued the PC entry) and a dangling empty entry sat at the end, which is
what the Android default index actually pointed at. The rewrite normalized
the file; Quest now deterministically gets the Quest tier + Quest_RPAsset.

## Unity quality-tier values (configured)

| Field | Desktop Low | Desktop Standard | Desktop High | Quest |
|---|---|---|---|---|
| URP asset | Mobile_RPAsset | PC_RPAsset | **DesktopHigh_RPAsset (new)** | Quest_RPAsset |
| shadowDistance (legacy field) | 25 | 40 | 45 | 20 |
| shadowmaskMode | 0 | 1 | 1 | 0 |
| skinWeights | 2 | 4 | 4 | 2 |
| anisotropicTextures | ForceEnable | ForceEnable | ForceEnable | ForceEnable |
| lodBias | 1.2 | 2.0 | 2.5 | 1.2 |
| maximumLODLevel | 0 | 0 | 0 | 0 |
| textureMipmapLimit | 0 | 0 | 0 | 0 |
| LOD cross-fade | on | on | on | on |
| vSyncCount | 0 (user setting governs) | 0 | 0 | 0 |
| streaming mipmaps | off | off | off | off |
| terrain overrides | on (basemap 80, detail 14, tree 75, billboard 35, pixelError 6) | off (scene values) | off (scene values) | on (basemap 80, detail 12, tree 65, billboard 30, pixelError 7) |
| excluded platforms | Android, iPhone | Android, iPhone | Android, iPhone | Standalone |

## URP pipeline-asset values (configured)

| Field | Mobile_RPAsset (Low) | PC_RPAsset (Standard) | DesktopHigh_RPAsset (High) | Quest_RPAsset |
|---|---|---|---|---|
| Render scale | 1.0 | 1.0 | 1.0 | 1.0 |
| MSAA | 2× | off | 2× | 4× |
| HDR | on | on | on | on |
| Main-light shadows | on | on | on | on |
| Shadowmap resolution | 1024 | 1024 | **2048** | 1024 |
| Shadow distance | 30 | 25 | **45** | 20 |
| Cascades | 1 | 1 | **2** | 1 |
| Soft shadows | off | on (quality 1) | on (**quality 2**) | off |
| Additional lights | per-vertex, limit 4, no shadows | disabled | disabled | per-vertex, limit 4, no shadows |
| Reflection probes (realtime) | off | off | off (unvalidated cost avoided by design) | off |
| SRP batcher / LOD cross-fade | on / on | on / on | on / on | on / on |

User-facing graphics settings apply as **deltas over the active tier's
captured baseline** (render scale ±, shadow distance 0.5–1.5×, MSAA override,
texture mipmap limit, post-processing toggle, terrain/vegetation distance
0.5–1.5×), so tiers stay meaningful and reset-to-default is exact. Options
Unity cannot safely change at runtime (shadowmap resolution, cascades) are
deliberately tier-bound rather than exposed as dead controls.

Design intents honored: Desktop Low keeps every optimized mesh/vegetation
asset (nothing high-poly restored); Desktop High extends distances/LOD ranges
on the same optimized assets instead of swapping in expensive geometry; Quest
stays independent and untouched in look.

## Validation status

- **Values configured**: everything above.
- **Tested on available desktop hardware (Apple M4, macOS)**: tier switching
  at runtime, per-setting deltas, persistence across relaunch — see final
  report evidence; FPS on this machine is not representative of institutional
  hardware and is reported only as smoke-test telemetry.
- **Awaiting lower-end institutional testing (owner)**: the 30 FPS Desktop Low
  target, Standard 30–60 FPS range — `05_DEFERRED_TESTS_LOWEND_HARDWARE.md`.
- **Awaiting physical Quest testing (owner)**: 72 FPS/thermal/memory budget —
  `04_DEFERRED_TESTS_QUEST.md`. No Quest performance claim is made.
