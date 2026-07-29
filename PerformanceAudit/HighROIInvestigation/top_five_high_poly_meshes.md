# Top Five High-Poly Mesh Sources

These are the five non-architecture mesh sources contributing approximately 1.5 million main-scene renderer triangles.

## 1. `Assets/BCaT_assets/BTMMP_Workstation_Assembly/metal_table_asset.glb`

| Field | Value |
|---|---:|
| Scene usage | `Assets/BH_XR_MainScene.unity` |
| Source format | `.glb` |
| Total triangles in scene | 370,088 |
| Total vertices in scene | 196,207 |
| Renderer count / instances | 3 / 3 |
| Material slots | 3 |
| Shadow-casting renderers | 3 |
| MeshCollider renderers / collider triangles | 0 / 0 |
| Import Read/Write | `unknown` |
| Mesh compression | `unknown` |
| Imported mesh assets | `unknown` |

Sample object paths:

- `_SceneContent/ImplementedContributorInstallations/BTMMP_Workstation_Assembly/static/metal_table_asset/Object_4`
- `_SceneContent/ImplementedContributorInstallations/BTMMP_Workstation_Assembly/static/metal_table_asset/Object_2`
- `_SceneContent/ImplementedContributorInstallations/BTMMP_Workstation_Assembly/static/metal_table_asset/Object_3`

Recommendation: create LODs and decimated visual meshes. Safe target is roughly 166,539 total triangles across visible instances; aggressive target is roughly 74,017. Preserve normals where the surface relies on smooth specular shape. Tangents are only needed where normal maps materially contribute; verify material use before stripping. Do not use these visual meshes for collision if simpler primitives or low-poly collision can stand in.

Expected WebGL value: Very high for visible triangle and shadow-pass reduction, medium for memory/download. Visual risk is medium because these are visible cultural/exhibit or scene-setting objects.

## 2. `Assets/BCaT_assets/LindaLeaks/Models/LL_PhotoAlbum.glb`

| Field | Value |
|---|---:|
| Scene usage | `Assets/BH_XR_MainScene.unity` |
| Source format | `.glb` |
| Total triangles in scene | 359,488 |
| Total vertices in scene | 210,280 |
| Renderer count / instances | 4 / 4 |
| Material slots | 4 |
| Shadow-casting renderers | 4 |
| MeshCollider renderers / collider triangles | 0 / 0 |
| Import Read/Write | `unknown` |
| Mesh compression | `unknown` |
| Imported mesh assets | `unknown` |

Sample object paths:

- `_SceneContent/ImplementedContributorInstallations/RI/Photo_Asset/Photo-Album/RW_PhotoAlbum_Model/Object_2`
- `_SceneContent/ImplementedContributorInstallations/LindaLeaks_Exhibit/PhotoAlbum_Preview/Artifact_PhotoAlbum/LL_PhotoAlbum_Model/Object_2`
- `_SceneContent/ImplementedContributorInstallations/RI/Photo_Asset/Photo-Album/RW_PhotoAlbum_Model/Object_3`
- `_SceneContent/ImplementedContributorInstallations/LindaLeaks_Exhibit/PhotoAlbum_Preview/Artifact_PhotoAlbum/LL_PhotoAlbum_Model/Object_3`

Recommendation: create LODs and decimated visual meshes. Safe target is roughly 161,769 total triangles across visible instances; aggressive target is roughly 71,897. Preserve normals where the surface relies on smooth specular shape. Tangents are only needed where normal maps materially contribute; verify material use before stripping. Do not use these visual meshes for collision if simpler primitives or low-poly collision can stand in.

Expected WebGL value: Very high for visible triangle and shadow-pass reduction, medium for memory/download. Visual risk is medium because these are visible cultural/exhibit or scene-setting objects.

## 3. `Assets/BCaT_assets/Ri/glass_fish.glb`

| Field | Value |
|---|---:|
| Scene usage | `Assets/BH_XR_MainScene.unity` |
| Source format | `.glb` |
| Total triangles in scene | 300,000 |
| Total vertices in scene | 187,307 |
| Renderer count / instances | 4 / 4 |
| Material slots | 4 |
| Shadow-casting renderers | 4 |
| MeshCollider renderers / collider triangles | 0 / 0 |
| Import Read/Write | `unknown` |
| Mesh compression | `unknown` |
| Imported mesh assets | `unknown` |

Sample object paths:

- `_SceneContent/ImplementedContributorInstallations/RI/Fish_Asset/glass_fish/SM_Body/SM_Body_M_Body_0`
- `_SceneContent/ImplementedContributorInstallations/RI/Fish_Asset/glass_fish/SM_InnerBody/SM_InnerBody_M_InnerBody_0`
- `_SceneContent/ImplementedContributorInstallations/RI/Fish_Asset/glass_fish/SM_Body/SM_Body_M_Body_0`
- `_SceneContent/ImplementedContributorInstallations/RI/Fish_Asset/glass_fish/SM_Body/SM_Body_M_Body_0`

Recommendation: create LODs and decimated visual meshes. Safe target is roughly 135,000 total triangles across visible instances; aggressive target is roughly 60,000. Preserve normals where the surface relies on smooth specular shape. Tangents are only needed where normal maps materially contribute; verify material use before stripping. Do not use these visual meshes for collision if simpler primitives or low-poly collision can stand in.

Expected WebGL value: Very high for visible triangle and shadow-pass reduction, medium for memory/download. Visual risk is medium because these are visible cultural/exhibit or scene-setting objects.

## 4. `Assets/My_Custom/japanese_red_bridge.glb`

| Field | Value |
|---|---:|
| Scene usage | `Assets/BH_XR_MainScene.unity` |
| Source format | `.glb` |
| Total triangles in scene | 275,502 |
| Total vertices in scene | 356,166 |
| Renderer count / instances | 9 / 9 |
| Material slots | 9 |
| Shadow-casting renderers | 9 |
| MeshCollider renderers / collider triangles | 9 / 275,502 |
| Import Read/Write | `unknown` |
| Mesh compression | `unknown` |
| Imported mesh assets | `unknown` |

Sample object paths:

- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Handle Bars.001_4/Object_16`
- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Handle Bars_2/Object_8`
- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Handle Bars_2/Object_10`
- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Handle Bars_2/Object_9`
- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Handle Bars_2/Object_11`
- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Handle Bars.001_4/Object_17`
- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Handle Bars_2/Object_12`
- `_SceneContent/Home/Exterior/Pond/japanese_red_bridge/Bridge Steps_0/Object_4`

Recommendation: create LODs and decimated visual meshes. Safe target is roughly 123,975 total triangles across visible instances; aggressive target is roughly 55,100. Preserve normals where the surface relies on smooth specular shape. Tangents are only needed where normal maps materially contribute; verify material use before stripping. Do not use these visual meshes for collision if simpler primitives or low-poly collision can stand in.

Expected WebGL value: High for visible triangle and shadow-pass reduction, medium for memory/download. Visual risk is medium because these are visible cultural/exhibit or scene-setting objects.

## 5. `Assets/BCaT_assets/9night/drum.glb`

| Field | Value |
|---|---:|
| Scene usage | `Assets/BH_XR_MainScene.unity` |
| Source format | `.glb` |
| Total triangles in scene | 190,100 |
| Total vertices in scene | 179,321 |
| Renderer count / instances | 3 / 3 |
| Material slots | 3 |
| Shadow-casting renderers | 3 |
| MeshCollider renderers / collider triangles | 0 / 0 |
| Import Read/Write | `unknown` |
| Mesh compression | `unknown` |
| Imported mesh assets | `unknown` |

Sample object paths:

- `_SceneContent/ImplementedContributorInstallations/9Night/drum/Drum.001_Drum_0`
- `_SceneContent/ImplementedContributorInstallations/9Night/drum/Drum.001_Drum_1`
- `_SceneContent/ImplementedContributorInstallations/9Night/drum/Drum.001_Drum_2`

Recommendation: create LODs and decimated visual meshes. Safe target is roughly 85,545 total triangles across visible instances; aggressive target is roughly 38,020. Preserve normals where the surface relies on smooth specular shape. Tangents are only needed where normal maps materially contribute; verify material use before stripping. Do not use these visual meshes for collision if simpler primitives or low-poly collision can stand in.

Expected WebGL value: High for visible triangle and shadow-pass reduction, medium for memory/download. Visual risk is medium because these are visible cultural/exhibit or scene-setting objects.

## Optimization order

1. `metal_table_asset.glb`
2. `LL_PhotoAlbum.glb`
3. `glass_fish.glb`
4. `japanese_red_bridge.glb`
5. `drum.glb`
