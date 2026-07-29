# Renderer Count Investigation Summary

Main scene renderer count is 2,046. The realistic target is 1,350-1,600 renderers without collapsing whole rooms or harming interaction/occlusion.

## Renderer Count By Room / Exhibit

| Room or exhibit | Renderers |
|---|---:|
| `_SceneContent/Environment` | 1144 |
| `FirstFloor/FirstFloorWalls` | 134 |
| `Exhibit:BTMMP_Workstation_Assembly` | 130 |
| `Exhibit:RI` | 61 |
| `Exhibit:Meshell_Sturgis` | 50 |
| `Exterior/Porch` | 42 |
| `Exterior/Pond` | 41 |
| `SecondFloor/SecondRoom` | 41 |
| `SecondFloor/MemoryRoom` | 40 |
| `_SceneContent/Home` | 39 |
| `Exterior/Backyard` | 37 |
| `SecondFloor/Temp_SecondFloor_Planning` | 33 |
| `FirstFloor/Pillars` | 32 |
| `Exterior/Bushes` | 32 |
| `FirstFloor/Kitchen` | 30 |
| `FirstFloor/LivingRoom` | 25 |
| `BuildProfiles/XR` | 24 |
| `FirstFloor/Hallway` | 18 |
| `Exhibit:PrivacyLaw` | 18 |
| `FirstFloor/SewingRoom` | 15 |
| `FirstFloor/FirstFloor` | 13 |
| `FirstFloor/DiningRoom` | 13 |
| `Exhibit:LindaLeaks_Exhibit` | 8 |
| `Exhibit:HOMED` | 6 |
| `SecondFloor/Roof` | 6 |
| `SecondFloor/FrontSecondFloor` | 5 |
| `Exhibit:9Night` | 3 |
| `Exhibit:BFM_Chest_OnChair_W_Text` | 2 |
| `Exhibit:Black_Parlors` | 2 |
| `Exterior/SM_boat_pylon` | 1 |
| `BuildProfiles/Web` | 1 |

## Top Source Assets By Renderer Count

| Source asset | Renderers |
|---|---:|
| `Assets/Emilulz_Assets/DEMOLowPolyFlowers/Assets/SM_Daisy_Single.fbx` | 741 |
| `Assets/Idyllic Italian Coast Town/Meshes/Modular/SM_wall_base.fbx` | 147 |
| `Assets/Animated Tropical Vegetation/Models/Jungle Bushes/jungle_bush_1.fbx` | 129 |
| `Assets/Emilulz_Assets/DEMOLowPolyFlowers/Assets/SM_Rose_Red_Open.fbx` | 126 |
| `Assets/Animated Tropical Vegetation/Models/Jungle Bushes/jungle_bush_3.fbx` | 115 |
| `Assets/BCaT_assets/BTMMP_Workstation_Assembly/drone.glb` | 77 |
| `Assets/SimpleNaturePack/Models/Flowers_01.fbx` | 42 |
| `` | 42 |
| `Assets/Emilulz_Assets/DEMOLowPolyFlowers/Assets/SM_Rose_Black_Open.fbx` | 33 |
| `Assets/Idyllic Italian Coast Town/Meshes/Modular/SM_wall_pillar.fbx` | 32 |
| `Assets/SimpleNaturePack/Models/Bush_03.fbx` | 32 |
| `Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Models/Env_GrassPlant_05.fbx` | 31 |
| `Library/unity default resources` | 29 |
| `Assets/DevDen Arch Viz Scotland/Models/Study Room/books.FBX` | 23 |
| `Assets/BrokenVector/LowPolyFencePack/Models/Fence Type2 03.dae` | 22 |
| `Assets/UnityTechnologies/Basic Asset Pack Interior/Models/Walls/WallWindow2m.FBX` | 22 |
| `Assets/BCaT_assets/BTMMP_Workstation_Assembly/generic_tulip_flower.glb` | 20 |
| `Assets/SimpleNaturePack/Models/Grass_01.fbx` | 20 |
| `Assets/My_Custom/modern_scandinavian_kitchen_island.glb` | 19 |
| `Assets/My_Custom/morten-s.glb` | 18 |
| `Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Models/UniversalController.fbx` | 18 |
| `Assets/UnityTechnologies/Basic Asset Pack Interior/Models/Walls/WallWindowTall4m.FBX` | 15 |
| `Assets/Idyllic Italian Coast Town/Meshes/Buildings/SM_building_modular_floor_2x1_03.fbx` | 12 |
| `Assets/My_Custom/zedah_prevalent_52_inch_luxury_fan.glb` | 11 |
| `Assets/danthaigames/DS 80s Television/FBX/TV.fbx` | 11 |

Use `renderer_reduction_candidates.csv` for spatially sensible combine/instance candidates. Do not combine entire rooms or the whole house; prioritize flowerbeds, porch fence segments, repeated Italian wall modules by section/material, repeated exhibit props, and static decorative clusters.
