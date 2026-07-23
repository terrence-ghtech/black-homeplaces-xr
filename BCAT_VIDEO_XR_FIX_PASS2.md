# BCaT Pass 2 — Quest Video Fixes, Reusable MediaVideoExhibit System, Holographic Slideshow

Date: 2026-07-06 · Backups: `*.bak_20260706_180618_pass2`

## 1. Why Sewing Room played on WebGL but not on Quest

The WebGL path never depended on XR: the desktop rig walks into the quilt's trigger box and presses E, and the video streams by URL from StreamingAssets — all of that works in a browser. On Quest **every link in the chain was different and broken**:

1. **No select route to Play()**: opening the popup was keyboard-only (`Keyboard.current` is null on Quest). The XR select box added last pass (`Quilt_XRSelect`) is position-estimated and was wired to a script with zero logging, so if the ray never hit it there was no way to see why. The proximity prompt also depended on the XR rig's Player tag, which only exists in builds made after last pass.
2. **No UI raycaster for XR**: the popup canvases had only `GraphicRaycaster` (screen-space/mouse). XR ray interactors require `TrackedDeviceGraphicRaycaster` on each world canvas to press any UI. (Scene already has an EventSystem with `XRUIInputModule`, and the rig's interactors have UI interaction enabled, so the canvas-side raycaster was the only missing piece.)
3. Video file itself is fine: `in_my_sisters_room_xr.mp4` is H.264 High 1920×1080@30 + AAC (verified with ffprobe) — well within Quest hardware decode limits; `Application.streamingAssetsPath` URLs are supported by VideoPlayer on Android.

## 2. Why Linda Leaks camera video failed

- **WebGL**: the VideoPlayer source was an imported `VideoClip` — Unity cannot decode VideoClip assets on WebGL — and video audio was routed to an AudioSource, also unsupported on WebGL (fixed last pass in script, but the running build predates it; the mp4 is now in StreamingAssets).
- **Quest**: `SelectEntered` was unwired until last pass, and even wired, the Close button was un-pressable (no TrackedDeviceGraphicRaycaster). Interaction was otherwise healthy: the artifact has a non-trigger 0.41×0.23×0.32 BoxCollider on the Default layer with `XRSimpleInteractable` — XR rays can hit it.
- **Everywhere**: zero logging meant input failures and video failures were indistinguishable. That is now fixed — see logs below.

## 3. New reusable system (architecture correction)

**`Assets/Scripts/MediaVideoController.cs`** (GUID `249b73ef51b046e7be3121feefbb14af`) — one component drives every video exhibit:
- Desktop/WebGL: E key via `ProximityTrigger` (walk-up, like the quilt) or `LookRaycast` (aim, like the camera). Quest: wire `XRSimpleInteractable.SelectEntered → OnXRSelect()`.
- Source selection: hosted-URL override → StreamingAssets file name; `VideoClip` only as Editor preview. Audio: separate-soundtrack mode (quilt's spatial wav, all platforms), AudioSource routing (non-WebGL), Direct otherwise/on WebGL.
- Auto-creates a 1280×720 RenderTexture if the VideoPlayer/RawImage aren't pre-wired.
- Serialized project data: title, artist/creator, description, video file name/URL, prompt object, optional billboard TMP that is auto-filled.
- **Logs every step** with the object name: XR SelectEntered, desktop key press, resolved video URL, `prepareCompleted` (duration/size), `errorReceived` (message + URL), every `Play()` call, trigger enter/exit, open/close.

**`Assets/Scripts/HolographicSlideshow.cs`** (GUID `bba09c296aff45819a09ed93f13d3012`) — generic slideshow; field/method names mirror the retired `LindaLeaksPhotoAlbumController` so the 9 serialized photos and all Button wiring survived the swap. Adds ←/→ arrow-key navigation and Escape-to-close on desktop, `OnXRSelect()` toggle, and step-by-step logs.

**`Assets/Scripts/FaceCamera.cs`** (GUID `1aa5eb26b4ba49fbbef452f9f85399a6`) — optional Y-axis billboard used by the hologram variant.

**Prefab variants** in `Assets/BCaT_assets/ExhibitCanvases/`:
- `MediaVideoExhibit_Billboard.prefab` — root (VideoPlayer + MediaVideoController) → InteractableObject (BoxCollider + XRSimpleInteractable→OnXRSelect), BillboardCanvas (BG, LabelBodyText, PromptText + PlatformInteractionPrompt), VideoPopupCanvas (GlowFrame, dark backdrop, RawImage, glowing × close, GraphicRaycaster + TrackedDeviceGraphicRaycaster).
- `MediaVideoExhibit_Hologram.prefab` — same minus billboard; popup canvas has FaceCamera; floating PromptCanvas.
- `HolographicPhotoSlideshow.prefab` — root (HolographicSlideshow) → InteractableObject (collider + XRSimpleInteractable + desktop opener), SlideshowCanvas (GlowFrame, translucent purple backdrop, centered photo, ‹ › arrows, × close, TDGR).

## 4. Exact changes

Scripts:
- New: `MediaVideoController.cs`, `HolographicSlideshow.cs`, `FaceCamera.cs`.
- `LindaLeaksPanelOpener.cs`: fields retyped to `MediaVideoController` / `HolographicSlideshow`, logging added. (`QuiltVideoPopUp.cs`, `LindaLeaksVideoPopUp.cs`, `LindaLeaksPhotoAlbumController.cs` remain on disk but are no longer referenced.)

`LindaLeaks_Exhibit_VintageCamera.prefab`:
- `LindaLeaksVideoPopUp` component replaced in-place (same fileID) by `MediaVideoController` (URL `Linda_Leaks_CHOF_720p.mp4`, editor preview clip kept, AudioSource routing on device/desktop, LookRaycast).
- `SelectEntered` re-aimed at `OnXRSelect`; Close button onClick retargeted to `MediaVideoController.ClosePopUp`.
- Popup restyle: backdrop → translucent dark purple, cyan GlowFrame added, Close → 44×44 glowing `×` top-right.
- `TrackedDeviceGraphicRaycaster` added to the popup canvas.

`LindaLeaks_Exhibit_PhotoAlbum.prefab`:
- Controller script swapped in-place to `HolographicSlideshow` (photos/captions preserved — verified 9 entries).
- Backdrop → translucent dark purple + cyan GlowFrame; photo enlarged and centered (640×420); title (kept: per-photo titles + "Housing Co-op Archive" header text driven by the controller) recolored pale cyan; caption/description recentered, minimal.
- Previous/Next/Close rectangles **removed**: now 64×64 glowing `‹` / `›` hotspots mid-left/right and a 44×44 `×` top-right (glyphs verified present in the LiberationSans SDF atlas), with additive-glow hover states.
- `TrackedDeviceGraphicRaycaster` added to the album canvas.

Scene (`BH_XR_MainScene.unity`):
- Quilt `QuiltVideoPopUp` replaced in-place by `MediaVideoController` (ProximityTrigger mode, separate spatial wav preserved, sewing-machine ambient pause preserved, prompt canvas link preserved).
- Quilt `Quilt_XRSelect` interactable re-aimed at `MediaVideoController.OnXRSelect`.
- 6 stale `m_fontColor32` overrides removed from the two LindaLeaks preview instances so the new label colors apply.

## 5. Confirmations & what still needs a device test

- WebGL Sewing Room behavior is preserved: same trigger/E flow, same StreamingAssets URL, same deferred-prepare-then-play gesture flow, same separate-wav audio. Config-verified; **rebuild WebGL** to pick up pass-1+2 changes.
- The album UI no longer has rectangular Previous/Next/Close buttons — holographic controls confirmed in the prefab data.
- I cannot run Quest or WebGL builds from here, so on-device playback is **verified by configuration + instrumentation, not by execution**. After rebuilding both targets, watch logcat / browser console for `[MediaVideo:...]` lines: you'll see select → URL → prepareCompleted → Play(), or an explicit errorReceived telling you which link failed.
- If Quest select still doesn't fire on the quilt, nudge `Quilt_XRSelect` onto the quilt mesh (it logs when hit; position was estimated).

## 6. Suggested next steps
- Rebuild WebGL and the Quest APK (both mp4s must land in StreamingAssets inside the builds).
- Optional polish: swipe gestures on the slideshow (arrows-first shipped, as agreed); soft pulse animation on glow hotspots; per-exhibit RenderTextures if two videos ever play simultaneously.
