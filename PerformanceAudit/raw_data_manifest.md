# Raw Data Manifest

Unity batch mode generated the CSV files in this folder. The temporary Editor script used to generate them was removed after the successful audit run.

- `scene_totals.csv`: per-scene counts for renderers, triangles, colliders, lights, terrain, canvases, cameras, media components, static flags.
- `scene_renderers.csv`: per-renderer mesh/material/collider/static/shadow data and architecture replacement heuristics.
- `scene_texture_references.csv`: textures referenced by scene materials with runtime memory estimate.
- `scene_media_references.csv`: AudioSource and VideoPlayer scene references.
- `models.csv`: imported model mesh totals and importer flags.
- `textures.csv`: imported texture dimensions/import compression/runtime memory estimate.
- `media.csv`: audio and video asset inventory.
- `materials.csv`: material shader/instancing/main texture inventory.
- `prefabs.csv`: prefab mesh/collider/LOD/shadow totals.
- `architecture_candidates.csv`: name/path-based architectural asset candidates.
