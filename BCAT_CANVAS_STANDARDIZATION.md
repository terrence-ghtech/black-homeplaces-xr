# BCaT Exhibit Canvas Standardization & Video Debug Report

Date: 2026-07-06 · Scene: `Assets/BH_XR_MainScene.unity`
Backups: `*.bak_20260706_172433` (scene, 3 LindaLeaks prefabs, HOMED prefab, 2 scripts)

## 1. Canvas audit (all World Space)

### a) Digital exhibit / readout canvases
| Canvas | Path | Contents | Interaction |
|---|---|---|---|
| BFM Chest | `_SceneContent/ImplementedContributorInstallations/BFM_Chest_OnChair_W_Text/Canvas` | BR_Image + LabelBodyText ("Project Title" placeholder) + PromptText (was empty) | none yet |
| Black Parlors | `.../Black_Parlors/Canvas` | BR_Image + LabelBodyText (placeholder) + PromptText (was empty) | `InteractableLinkLauncher` → ArcGIS storymap, on the picture-frame prefab instance, XR select already wired |
| BTMMP | `.../BTMMP_Workstation_Assembly/Canvas` | TheBreonnaTaylorMuralPrompt (was empty) | none yet |
| HOMED | inside `BCaT_assets/HOMED/Prefabs/HOMED.prefab` (+ scene-added LabelBodyText) | HomedPrompt (launcher-driven) | `InteractableLinkLauncher` → arcg.is/0e8zi1, XR select already wired |

### b) Sound / audio canvases (content preserved, untouched)
| Canvas | Path | Contents |
|---|---|---|
| Sewing machine | `.../Sewing_Room/Sewingmachine/Canvas` | "In My Sister's Room — Maurika Smutherman — Spatial Sound Installation" label; ambient AudioSource plays on awake |
| 9Night | `.../ImplementedContributorInstallations/9Night/Canvas` | "Nine Night and Good Mourning" label; ambient AudioSource |

### c) Media / video / photo-booth canvases
| Canvas | Path | Contents |
|---|---|---|
| Quilt prompt (now full readout) | `.../Sewing_Room/Quilt/Canvas_Prompt` | upgraded, see §5 |
| Quilt video popup | `.../Sewing_Room/Quilt/Canvas` | Panel + RawImage → `SewingRoom/VideoTexture.renderTexture` (unchanged) |
| Linda Leaks previews ×3 | `LindaLeaks_Review_FrontYard/LindaLeaks_Exhibit_{VintageCamera,PhotoAlbum,HousingMap}_Preview/Canvas` | plaque canvases; prefab-internal popup canvases (video panel, photo album) preserved |

## 2. Standardized structure

**New script `Assets/Scripts/PlatformInteractionPrompt.cs`** (GUID `068113101a9348c098c3c4e0616414d7`):
- `InteractionPromptText` static class = single source of truth: `Verb` returns **"Press E"** (WebGL/desktop) or **"Interact"** (XR). XR detection: `XRSettings.isDeviceActive` + `XRGeneralSettings.Instance.Manager.isInitializationComplete/activeLoader`, hard-forced to desktop under `UNITY_WEBGL`.
- `PlatformInteractionPrompt` MonoBehaviour: attach next to a TMP prompt text; writes `verb + textAfterVerb`, re-polls ~10 s so late XR init still flips the verb. `editorOverride` enum (Auto/Desktop/XR) for Editor testing.

**New prefabs in `Assets/BCaT_assets/ExhibitCanvases/`** — same layout (2×1 m world canvas → BG_Image (BCaT purple), LabelBodyText, PromptText+PlatformInteractionPrompt):
- `DigitalExhibitCanvas.prefab` — shared placeholder body "Project Title / Artist Name / Digital Exhibit".
- `SoundExhibitCanvas.prefab` — PromptText disabled by default (sound exhibits are ambient).
- `MediaExhibitCanvas.prefab` — prompt suffix " to play".

## 3. Exact changes made

Scripts:
- `InteractableLinkLauncher.cs` — prompt now `InteractionPromptText.Verb + " to open project."` (old `#if UNITY_WEBGL` block showed **"Interact"** on desktop builds/Editor — wrong).
- `LindaLeaksVideoPopUp.cs` — on WebGL forces `VideoSource.Url` from StreamingAssets + `VideoAudioOutputMode.Direct` (VideoClip assets and AudioSource video-audio are unsupported on WebGL).

Prefabs:
- `LindaLeaks_Exhibit_VintageCamera.prefab` — XRSimpleInteractable `SelectEntered` → `LindaLeaksVideoPopUp.TogglePopUp`.
- `LindaLeaks_Exhibit_PhotoAlbum.prefab` — `SelectEntered` → `LindaLeaksPanelOpener.Open`.
- `LindaLeaks_Exhibit_HousingMap.prefab` — `SelectEntered` → `InteractableLinkLauncher.OpenLink`.
- HOMED + Black Parlors picture frame were already wired (no change).

Scene (`BH_XR_MainScene.unity`):
- XR Origin (XR Rig) instance root now tag **Player** (was Untagged → `QuiltVideoPopUp.OnTriggerEnter` could never fire in XR; desktop rig was already tagged via PlayerCapsule prefab).
- `PlatformInteractionPrompt` added to: BFM Chest PromptText, BTMMP prompt, Quilt Canvas_Prompt PromptText (suffix `to watch "In My Sister's Room" A Short Film`).
- Black Parlors launcher `promptText` field now points at its canvas PromptText (was null → prompt stayed empty).
- New `Quilt_XRSelect` child (2 m box collider, non-trigger + XRSimpleInteractable `SelectEntered` → `QuiltVideoPopUp.TogglePopUp`) — XR rays ignore trigger colliders (`m_RaycastTriggerInteraction: Ignore`), so the existing proximity trigger box was unselectable in XR.
- Quilt `Canvas_Prompt` upgraded to the MediaExhibitCanvas layout: BG_Image (purple), LabelBodyText ("In My Sister's Room / Maurika Smutherman / Short Film · Video Exhibit"), PromptText restyled (white, bottom strip). Existing `promptText` show/hide link from QuiltVideoPopUp preserved.
- Quilt VideoPlayer stale serialized URL `file://<USER_HOME>/...` cleared (runtime sets it; old absolute path pointed at the pre-move project location).

Assets:
- `Linda_Leaks_CHOF_720p.mp4` copied to `Assets/StreamingAssets/` (matches the prefab's `videoFileName`).

## 4. Video playback — root causes found

**Linda Leaks (Hall of Fame film):**
1. WebGL: VideoPlayer used an imported `VideoClip` — **not supported on WebGL** → fixed (URL from StreamingAssets; file now present there).
2. WebGL: video audio routed to an AudioSource — not supported on WebGL → fixed (Direct).
3. Quest: interaction was keyboard-E raycast only; XRSimpleInteractable had **no select events wired** → fixed.
4. Desktop Editor: should have worked via clip + E-key raycast at ≤5 m aimed at the camera artifact. If it still fails in Editor, check Console for missing-reference errors on the `*_Preview` instance.

**Sewing Room ("In My Sister's Room"):**
1. Quest: XR rig untagged → proximity prompt/E never armed; no XR select path at all → both fixed (Player tag + Quilt_XRSelect).
2. Desktop/WebGL: wiring was actually complete (trigger box, URL → StreamingAssets, render texture, audio wav assigned). Requires walking into the large trigger volume around the bed, then E. If it still fails on desktop, check Console during Play; the file itself is H.264/AAC (verified via ffprobe) and present in StreamingAssets.
3. Both videos are H.264 High + AAC-LC `.mp4` — browser-compatible; playback starts from user input (autoplay policies satisfied).

## 5. Remaining issues / next steps
- **Unity must reimport**: open the project so the new script GUID, prefabs and StreamingAssets meta files compile/import; verify no console errors (all edits were done as YAML while Unity was closed).
- **Quilt_XRSelect box position is an estimate** (placed at the proximity-trigger center, 2 m cube, local (2.2, −2.9, 1) under `pillow__quilt`). Nudge it onto the quilt mesh in the Editor.
- **Canvas_Prompt layout** (label top / prompt bottom on purple) should be eyeballed in Editor; tweak sizes if text overflows.
- The Quilt video popup audio uses a **separate wav** (`VideoAudioOutputMode.None`) — can drift out of sync on WebGL; consider muxed audio via Direct mode later.
- 9Night and BTMMP have no interactable objects yet; their prompts now render the platform verb as placeholder (BTMMP) — hook real interactions later using the new prefabs.
- WebGL build folder `webgl/` predates these changes — rebuild required, and confirm both mp4s are deployed under `StreamingAssets/`.
- The two big mp4s (75 MB + 33 MB) inflate the WebGL payload; consider hosting them on a CDN URL instead of StreamingAssets if load times matter.
