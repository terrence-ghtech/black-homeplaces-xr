# BCAT Implementation Audit

## Scope

- Audit target: current main playable build path only.
- Build settings contain one enabled scene: `Assets/BH_XR_MainScene.unity`.
- `_Recovery` scenes were not treated as part of the playable build path.
- `Assets/StreamingAssets` contains one contributor media file: `in_my_sisters_room_xr.mp4`.
- No contributor HTML files or Addressables implementation were found.
- External URLs were identified from serialized scene/prefab data, but external destinations were not validated.

## High-Level Findings

- Clearly implemented in the main scene: `HOMED`, `Black Family Museum & Archive`, `Black Parlors`, `Breonna Taylor mural archive`, `Sewing Room`, `Nine Night and Good Mourning`.
- Implemented interactions in the main scene are limited to URL launchers and one quilt video popup.
- A missing script GUID is referenced across multiple contributor prompts:
  - `Assembly-CSharp::BillboardToCamera`
  - Missing from repo, but still serialized in `Assets/BH_XR_MainScene.unity`, `Assets/BCaT_assets/HOMED/Prefabs/HOMED.prefab`, `Assets/BCaT_assets/BlackParlors/Prefabs/Black_Parlors.prefab`, and `Assets/BCaT_assets/BlackFamilyMuseumArchive/Prefabs/BFM_Chest_OnChair_W_Text.prefab`.
- Several requested contributor projects have no contributor-specific implementation evidence in the main scene. In a few cases, there is only a generic location shell such as `KitchenAssets` or `HomeFrontStructure`.

## Main Scene Evidence

- Build scene: [EditorBuildSettings.asset](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/ProjectSettings/EditorBuildSettings.asset:7>)
- Main scene: [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:1>)
- Runtime scripts present in project:
  - [InteractableLinkLauncher.cs](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/Scripts/InteractableLinkLauncher.cs:1>)
  - [QuiltVideoPopUp.cs](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/Scripts/QuiltVideoPopUp.cs:1>)

## Per-Project Audit

### Montia Daniels — Homed: Recipes for Survival

- Found in Unity: `YES`
- Scene/location: `Assets/BH_XR_MainScene.unity` under `_SceneContent/BCaT_Assets/HOMED`
- GameObjects involved: `HOMED`, `PromptText`, recipe-box mesh child, candle mesh child, world-space canvas
- Scripts/components attached:
  - `InteractableLinkLauncher`
  - `XRSimpleInteractable`
  - `BoxCollider`
  - `TMP_Text`
  - `Canvas`
  - missing `BillboardToCamera`
- Media/URLs used:
  - `Assets/BCaT_assets/HOMED/Models/Recipe-Box.glb`
  - `Assets/BCaT_assets/HOMED/Models/Candle.glb`
  - `Assets/BCaT_assets/HOMED/Box_Img.mat`
  - `Assets/BCaT_assets/HOMED/Letters.mat`
  - `Assets/BCaT_assets/HOMED/Rice.mat`
  - `Assets/BCaT_assets/HOMED/Recipe_box_img.jpeg`
  - `Assets/BCaT_assets/HOMED/Recipe_Box_letters.png`
  - `Assets/BCaT_assets/HOMED/Recipe_Box_Rice.jpeg`
  - URL: `https://arcg.is/0e8zi1`
- Interaction wiring:
  - Wired.
  - `XRSimpleInteractable.m_SelectEntered` calls `InteractableLinkLauncher.OpenLink()`.
  - `InteractableLinkLauncher` also supports keyboard `E` raycast interaction.
- Status: `COMPLETE`
- Issues:
  - Missing `BillboardToCamera` script reference on prompt objects.
- Assets found in project but not used in main scene:
  - None beyond non-runtime metadata.
- Recommended next action:
  - Restore or replace `BillboardToCamera`, then verify the prompt still faces the player and the external link opens from the built target.
- Evidence:
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:49264>)
  - [HOMED.prefab](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BCaT_assets/HOMED/Prefabs/HOMED.prefab:542>)
  - [HOMED.prefab](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BCaT_assets/HOMED/Prefabs/HOMED.prefab:561>)

### Fatou Sow + collaborators — Black Family Museum & Archive

- Found in Unity: `YES`
- Scene/location: `Assets/BH_XR_MainScene.unity` under `_SceneContent/BCaT_Assets/BFM_Chest_OnChair_W_Text`
- GameObjects involved: `BFM_Chest_OnChair_W_Text`, `chest`, `chest_chair`, custom prompt object
- Scripts/components attached:
  - serialized as `Assembly-CSharp::ChestMuseumLauncher` on the `InteractableLinkLauncher` script GUID
  - `XRSimpleInteractable`
  - `BoxCollider`
  - `Rigidbody`
  - `TMP_Text`
  - missing `BillboardToCamera`
- Media/URLs used:
  - `Assets/BCaT_assets/BlackFamilyMuseumArchive/Models/chest.glb`
  - `Assets/BCaT_assets/BlackFamilyMuseumArchive/Models/chest_chair.glb`
  - URL: `https://www.artsteps.com/view/686a8082dc1f3854ff83dc73`
- Interaction wiring:
  - Wired.
  - `XRSimpleInteractable.m_SelectEntered` calls `OpenLink()` on the launcher.
  - Scene override sets `promptText` and `playerCamera`.
- Status: `COMPLETE`
- Issues:
  - Missing `BillboardToCamera` script reference on the prompt.
  - The simpler prefab `Assets/BCaT_assets/BlackFamilyMuseumArchive/Prefabs/BFM_Chest_OnChair.prefab` exists but is not instantiated in the main scene.
- Assets found in project but not used in main scene:
  - `Assets/BCaT_assets/BlackFamilyMuseumArchive/Prefabs/BFM_Chest_OnChair.prefab`
- Recommended next action:
  - Restore or replace `BillboardToCamera` and remove or explicitly archive the unused alternate prefab if it is no longer needed.
- Evidence:
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:49095>)
  - [BFM_Chest_OnChair_W_Text.prefab](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BCaT_assets/BlackFamilyMuseumArchive/Prefabs/BFM_Chest_OnChair_W_Text.prefab:505>)
  - [BFM_Chest_OnChair_W_Text.prefab](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BCaT_assets/BlackFamilyMuseumArchive/Prefabs/BFM_Chest_OnChair_W_Text.prefab:394>)

### Psyche Williams-Forson, Cheryl Hicks, Carla McGinnis — Black Parlors / Boardinghouses Web Exhibition

- Found in Unity: `YES`, mapped by folder/prefab names `BlackParlors` and `Black_Parlors`
- Scene/location: `Assets/BH_XR_MainScene.unity` under `_SceneContent/BCaT_Assets/Black_Parlors`
- GameObjects involved: `Black_Parlors`, nested `pictureframe`, prompt canvas
- Scripts/components attached:
  - `InteractableLinkLauncher`
  - `XRSimpleInteractable`
  - `TMP_Text`
  - `Canvas`
  - missing `BillboardToCamera`
- Media/URLs used:
  - `Assets/BCaT_assets/BlackParlors/Assets/pictureframe.prefab`
  - `Assets/BCaT_assets/BlackParlors/Assets/Photo_Parlor.mat`
  - `Assets/BCaT_assets/BlackParlors/Assets/Parlor_img.jpg`
  - URL: `https://storymaps.arcgis.com/stories/5adbdcc2e9624b1ca952e9639d2850cb`
- Interaction wiring:
  - Wired.
  - `XRSimpleInteractable.m_SelectEntered` calls `InteractableLinkLauncher.OpenLink()`.
  - `InteractableLinkLauncher` also supports keyboard `E` raycast interaction.
- Status: `COMPLETE`
- Issues:
  - Missing `BillboardToCamera` script reference on prompt objects.
  - The serialized `projectName` is `When Black Women Adorn the Parlor`; the exact requested title is not serialized anywhere else.
- Assets found in project but not used in main scene:
  - Local duplicate source art not used by the scene path:
    - `Assets/BCaT_assets/BlackParlors/Assets/picture-frame/source/pictureframe.fbx`
    - `Assets/BCaT_assets/BlackParlors/Assets/picture-frame/textures/pictureframe.png`
- Recommended next action:
  - Restore or replace `BillboardToCamera`, and decide whether the contributor title in Unity should be updated to match the requested exhibition naming.
- Evidence:
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:44087>)
  - [Black_Parlors.prefab](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BCaT_assets/BlackParlors/Prefabs/Black_Parlors.prefab:491>)
  - [Black_Parlors.prefab](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BCaT_assets/BlackParlors/Prefabs/Black_Parlors.prefab:405>)

### Alisa Hardy — Breonna Taylor / Say Her Name mural archive — Backyard

- Found in Unity: `YES`, but under `BTMMP_Workstation_Assembly` rather than an `AlisaHardy` or `Backyard` folder
- Scene/location: `Assets/BH_XR_MainScene.unity` under `_SceneContent/BCaT_Assets/BTMMP_Workstation_Assembly`
- GameObjects involved: `pictureframe`, `Photo` plane, `TheBreonnaTaylorMuralPrompt`
- Scripts/components attached:
  - `InteractableLinkLauncher`
  - `XRSimpleInteractable`
  - `BoxCollider`
  - standalone prompt `TMP_Text`
  - missing `BillboardToCamera`
- Media/URLs used:
  - `Assets/BCaT_assets/BlackParlors/Assets/pictureframe.prefab`
  - scene material override to `Assets/BCaT_assets/BlackParlors/Assets/BreonnaTaylorMural.mat`
  - `Assets/BCaT_assets/BlackParlors/Assets/breonna-taylor-mural-img.jpeg`
  - URL: `https://storymaps.arcgis.com/stories/de98c95d0ae94f34bcd4b53cdf10c6ed`
- Interaction wiring:
  - Wired on the frame itself.
  - `XRSimpleInteractable.m_SelectEntered` calls `OpenLink()`.
  - `InteractableLinkLauncher` keyboard `E` path is also present.
- Status: `PARTIAL`
- Issues:
  - The nearby `TheBreonnaTaylorMuralPrompt` exists but its `TMP_Text.m_text` is blank.
  - `InteractableLinkLauncher.promptText` is unassigned in the scene instance.
  - Prompt billboarding also references the missing `BillboardToCamera` script GUID.
  - Scene grouping is under `BTMMP_Workstation_Assembly`, which makes contributor ownership unclear inside the hierarchy.
- Assets found in project but not used in main scene:
  - Static `BTMMP_Workstation_Assembly` source textures:
    - `Assets/BCaT_assets/BTMMP_Workstation_Assembly/camera-canon-eos-400d/textures/Camara_Low_V2_lambert3_BaseColor.png`
    - `Assets/BCaT_assets/BTMMP_Workstation_Assembly/camera-canon-eos-400d/textures/Camara_Low_V2_lambert3_Normal.png`
    - `Assets/BCaT_assets/BTMMP_Workstation_Assembly/spray-paint-bottle-2/textures/diffuse.jpg`
    - `Assets/BCaT_assets/BTMMP_Workstation_Assembly/spray-paint-bottle-2/textures/normal.jpg`
- Recommended next action:
  - Bind or populate the prompt text, restore prompt billboarding, and rename/regroup this content so its ownership is explicit in the scene hierarchy.
- Evidence:
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:122>)
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:36431>)
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:31635>)

### Maurika Smutherman — Sewing Room

- Found in Unity: `YES`
- Scene/location: `Assets/BH_XR_MainScene.unity` under `_SceneContent/BCaT_Assets/Sewing_Room`
- GameObjects involved:
  - `Sewingmachine`
  - `Quilt`
  - `QuiltVideoPlayer`
  - `Video Player`
  - `QuiltVideoSpatialAudio`
  - `Canvas_Prompt/PromptText`
  - `Canvas/SewingPrompt`
- Scripts/components attached:
  - `QuiltVideoPopUp`
  - trigger `BoxCollider`
  - `Rigidbody`
  - `AudioSource` for sewing machine loop
  - `AudioSource` for quilt video audio
  - `VideoPlayer`
  - `TMP_Text`, `Canvas`, `RawImage`
- Media/URLs used:
  - `Assets/BCaT_assets/SewingRoom/sewing_machine.glb`
  - `Assets/BCaT_assets/SewingRoom/bed.glb`
  - `Assets/BCaT_assets/SewingRoom/pillow__quilt.glb`
  - `Assets/BCaT_assets/SewingRoom/old_sewing_box.glb`
  - `Assets/BCaT_assets/SewingRoom/needle_and_thread.glb`
  - `Assets/BCaT_assets/SewingRoom/sewing-sounds.wav`
  - `Assets/BCaT_assets/SewingRoom/in_my_sisters_room_audio.wav`
  - `Assets/BCaT_assets/SewingRoom/VideoTexture.renderTexture`
  - `Assets/StreamingAssets/in_my_sisters_room_xr.mp4`
- Interaction wiring:
  - Wired.
  - `QuiltVideoPopUp` opens/closes a popup video panel.
  - Trigger path: player enters the quilt trigger collider, then presses keyboard `E`.
  - `OpenPopUp()` pauses sewing-machine ambience, shows the panel, plays the streaming video and separate video audio source.
- Status: `COMPLETE`
- Issues:
  - Prompt objects reference the missing `BillboardToCamera` script GUID.
  - The serialized `VideoPlayer.m_Url` is an absolute local file URL in the scene; `QuiltVideoPopUp.Start()` rewrites it to `Application.streamingAssetsPath`, so the scene data itself is not portable even though the runtime script attempts to correct it.
  - Interaction is keyboard/trigger based; no XR-select path was found for the quilt popup.
- Assets found in project but not used in main scene:
  - None beyond non-runtime metadata.
- Recommended next action:
  - Restore or replace `BillboardToCamera`, then test the quilt interaction in the actual target runtime to confirm the `Player` tag, trigger volume, and streaming video path all behave correctly.
- Evidence:
  - [QuiltVideoPopUp.cs](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/Scripts/QuiltVideoPopUp.cs:1>)
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:15320>)
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:31307>)

### Christin Washington — Nine Night and Good Mourning

- Found in Unity: `YES`
- Scene/location: `Assets/BH_XR_MainScene.unity` under `_SceneContent/BCaT_Assets/9Night`
- GameObjects involved: `9Night`, drum prefab instance, `Audio Source`, `Canvas/9NightPrompt`
- Scripts/components attached:
  - `AudioSource`
  - `TMP_Text`
  - `Canvas`
- Media/URLs used:
  - `Assets/BCaT_assets/9night/drum.glb`
  - `Assets/BCaT_assets/9night/9night-soundscape.wav`
- Interaction wiring:
  - No interaction wiring found.
  - The soundscape is configured as looping ambient audio on awake.
- Status: `PARTIAL`
- Issues:
  - No trigger, launcher, or XR interactable was found.
  - The contributor content currently behaves as static scene dressing plus ambient audio.
- Assets found in project but not used in main scene:
  - None beyond non-runtime metadata.
- Recommended next action:
  - Decide whether ambient-only playback is the intended final behavior. If not, add a trigger, prompt behavior, or link-out interaction.
- Evidence:
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:29683>)
  - [BH_XR_MainScene.unity](</Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST/Assets/BH_XR_MainScene.unity:5052>)

### Evan Starling-Davis — Backyard/Deck/Garage XR Dreamscape

- Found in Unity: `UNCLEAR`
- Scene/location:
  - generic static scene grouping `_SceneContent/BCaT_Assets/BTMMP_Workstation_Assembly/static`
  - generic garage asset pack folder `Assets/AssetsStore/Garage_props`
- GameObjects involved:
  - static workstation props: `metal_table_asset`, `drone`, `notebook`, `spray_can`, `spray_paint_bottle_2`, `flowerbarrel_with_tulips`, `oil_barrel_*`
- Scripts/components attached:
  - No contributor-specific scripts or interactables found.
- Media/URLs used:
  - No contributor-specific media or URLs found.
- Interaction wiring:
  - No interaction wiring found.
- Status: `UNCLEAR`
- Issues:
  - There are backyard/garage-adjacent static assets, but nothing in naming, scripts, or URLs ties them directly to Evan Starling-Davis in the current main scene.
- Assets found in project but not used in main scene:
  - `Assets/AssetsStore/Garage_props/**` appears present as a package, but no contributor-specific use was identified in the main scene audit.
- Recommended next action:
  - Confirm whether `BTMMP_Workstation_Assembly` or `Garage_props` is intended to represent this contributor, then add explicit naming and interaction hooks if so.

### Jessica Rucker + collaborators — Linda Leaks Archive Project

- Found in Unity: `NO DIRECT EVIDENCE`
- Scene/location: none found
- GameObjects involved: none found
- Scripts/components attached: none found
- Media/URLs used: none found
- Interaction wiring: none found
- Status: `NOT STARTED`
- Issues:
  - No file-path, scene-name, prefab-name, script, or URL evidence matching `Linda`, `Leaks`, or `Archive Project` was found in the current main build path.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Add or identify the intended assets and scene entry point for this contributor.

### Yuhanxiao Maggie Ma — Black Kitchen

- Found in Unity: `UNCLEAR`
- Scene/location: generic location shell `_SceneContent/Home/KitchenAssets`
- GameObjects involved: `KitchenAssets`, `Cube`, `Cube (1)`, `Cube (2)`
- Scripts/components attached:
  - generic mesh/collider components only
- Media/URLs used:
  - no contributor-specific media or URLs found
- Interaction wiring:
  - none found
- Status: `UNCLEAR`
- Issues:
  - A kitchen location exists, but no contributor-specific folder, prefab, prompt, script, audio, video, image, or URL ties this area to `Black Kitchen`.
- Assets found in project but not used in main scene:
  - generic kitchen package content exists in `Assets/DevDen Arch Viz Scotland/Prefabs/Kitchen Room` and `Assets/Furniture Mega Pack/*`, but contributor ownership is not encoded.
- Recommended next action:
  - Identify which kitchen assets belong to this contributor and add explicit scene naming plus interaction/media hooks.

### Meshell Sturgis — Getting Home — Front Porch

- Found in Unity: `UNCLEAR`
- Scene/location: generic exterior shell under `_SceneContent/Home/HomeFrontStructure` and `_SceneContent/Home/PatioSofa`
- GameObjects involved:
  - generic exterior and patio objects only
- Scripts/components attached:
  - none contributor-specific
- Media/URLs used:
  - none found
- Interaction wiring:
  - none found
- Status: `UNCLEAR`
- Issues:
  - A front-of-house shell exists, but no contributor-specific naming, script, media, or URL identifies it as `Getting Home`.
- Assets found in project but not used in main scene:
  - no attributable project assets found by contributor/project name
- Recommended next action:
  - Confirm whether the front exterior is meant to host this project, then add explicit naming and implementation assets.

### Fabiana Gibim — Deja Vudu Sound Archive

- Found in Unity: `NO DIRECT EVIDENCE`
- Scene/location: none found
- GameObjects involved: none found
- Scripts/components attached: none found
- Media/URLs used: none found
- Interaction wiring: none found
- Status: `NOT STARTED`
- Issues:
  - No file-path, scene-name, prefab-name, script, audio, or URL evidence matching `Deja`, `Vudu`, or `Fabiana Gibim` was found.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Add or identify the intended assets, audio, and scene hookup.

### Felicity & Elizabeth — Adinkra Project

- Found in Unity: `NO DIRECT EVIDENCE`
- Scene/location: none found
- GameObjects involved: none found
- Scripts/components attached: none found
- Media/URLs used: none found
- Interaction wiring: none found
- Status: `NOT STARTED`
- Issues:
  - No file-path, scene-name, prefab-name, script, or URL evidence matching `Adinkra`, `Felicity`, or `Elizabeth` was found.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Add or identify the intended Adinkra assets and main-scene entry point.

### Nina-Simone Edwards — Black Homeplace as a Blueprint for Privacy Law — Front Porch

- Found in Unity: `UNCLEAR`
- Scene/location: generic exterior shell under `_SceneContent/Home/HomeFrontStructure`
- GameObjects involved:
  - generic house-front objects only
- Scripts/components attached:
  - none contributor-specific
- Media/URLs used:
  - none found
- Interaction wiring:
  - none found
- Status: `UNCLEAR`
- Issues:
  - The scene contains a house-front shell, but no contributor-specific assets, scripts, prompts, or URLs identify a privacy-law project.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Confirm whether this project is supposed to live on the front exterior, then add explicit naming and content.

### Sophia Monegro + collaborators — Kingsley homeplace / water well / foundations

- Found in Unity: `NO DIRECT EVIDENCE`
- Scene/location: none found
- GameObjects involved: none found
- Scripts/components attached: none found
- Media/URLs used: none found
- Interaction wiring: none found
- Status: `NOT STARTED`
- Issues:
  - No file-path, scene-name, prefab-name, script, or URL evidence matching `Kingsley`, `water well`, `foundations`, or `Sophia Monegro` was found.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Add or identify the intended assets and scene placement.

### Mila Turner + Kristine Fleming — RV / Outdoors

- Found in Unity: `NO DIRECT EVIDENCE`
- Scene/location: none found
- GameObjects involved: none found
- Scripts/components attached: none found
- Media/URLs used: none found
- Interaction wiring: none found
- Status: `NOT STARTED`
- Issues:
  - No file-path, scene-name, prefab-name, script, or URL evidence matching `RV`, `Mila Turner`, or `Kristine Fleming` was found.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Add or identify the intended RV/outdoor assets and scene hookup.

### Dez Brown — Black Trans Archives Video Game / Twine

- Found in Unity: `NO DIRECT EVIDENCE`
- Scene/location: none found
- GameObjects involved: none found
- Scripts/components attached: none found
- Media/URLs used:
  - no `Twine`, `.html`, or project-specific URL evidence found
- Interaction wiring: none found
- Status: `NOT STARTED`
- Issues:
  - No file-path, scene-name, prefab-name, script, HTML, or URL evidence matching `Black Trans Archives`, `Twine`, or `Dez Brown` was found.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Add or identify the intended Twine/game content and its launch path.

### Diamond Beverly-Porter — Rhythm and Rope

- Found in Unity: `NO DIRECT EVIDENCE`
- Scene/location: none found
- GameObjects involved: none found
- Scripts/components attached: none found
- Media/URLs used: none found
- Interaction wiring: none found
- Status: `NOT STARTED`
- Issues:
  - No file-path, scene-name, prefab-name, script, or URL evidence matching `Rhythm and Rope` or `Diamond Beverly-Porter` was found.
- Assets found in project but not used in main scene:
  - none attributable to this contributor by name
- Recommended next action:
  - Add or identify the intended assets and main-scene entry point.

### Rianna Walcott personal objects

- Found in Unity: `UNCLEAR`
- Scene/location:
  - generic household assets appear in the main scene and packages, including chairs, a bathroom vanity, and TV-related prefabs
- GameObjects involved:
  - main-scene examples include `SM_chair_02`, `Chair09`, `BathroomVanity01`
- Scripts/components attached:
  - no contributor-specific scripts or grouping found
- Media/URLs used:
  - no contributor-specific media or URLs found
- Interaction wiring:
  - none found
- Status: `UNCLEAR`
- Issues:
  - Household objects exist, but there is no contributor-specific naming, grouping, prompt, or script evidence tying them to Rianna Walcott’s listed personal-object set.
  - No evidence was found for `dominoes`, `glass fish`, `monobloc chair`, or `photo album` by name.
- Assets found in project but not used in main scene:
  - generic package assets such as `Assets/DevDen Arch Viz Scotland/Prefabs/Living Room/TV.prefab` and many chair prefabs exist, but attribution is unclear.
- Recommended next action:
  - If these objects are meant to be Rianna Walcott’s set, rename/group them explicitly and add a contributor-specific root object or metadata.

## Global Issues To Resolve

- Missing script asset for `BillboardToCamera`:
  - referenced in scene and contributor prefabs
  - likely affects prompt-facing behavior across `HOMED`, `Black_Parlors`, `BFM_Chest_OnChair_W_Text`, `TheBreonnaTaylorMuralPrompt`, and sewing-room prompts
- Breonna mural prompt is blank and not wired to the launcher.
- `Nine Night and Good Mourning` has content in scene but no interaction wiring.
- `Sewing Room` video interaction uses a desktop-key trigger path and stores a non-portable absolute video URL in the scene serialization even though runtime code attempts to correct it.

