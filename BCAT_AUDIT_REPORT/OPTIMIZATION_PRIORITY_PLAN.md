# Optimization Priority Plan

| Priority | Recommendation | Conservative | Likely | Max plausible | Risk |
|---|---|---:|---:|---:|---|
| P0 | Prove and remove duplicate video inclusion between VideoClip imports and StreamingAssets/URL copies | 50 MB | 150-250 MB | 300+ MB | Runtime media path QA |
| P1 | Host largest WebGL videos remotely or keep only required StreamingAssets | 119 MB | 248 MB | 248+ MB | Network/runtime loading |
| P1 | Compress/downscale full-page PNG documents, article pages, plaques | 50 MB | 150-300 MB | 400+ MB | Visual QA |
| P1/P2 | Optimize Black Kitchen/scanned models and embedded textures | 25 MB | 100+ MB | 200+ MB | Visual/physics QA |
| P2 | Convert long WAVs to streaming compressed audio and remove duplicate extracted audio when video already contains it | 20 MB | 75+ MB | 150+ MB | Narrative/audio QA |
| P2 | Remove unused package samples/demo content from included scenes/dependencies | 10 MB | 50+ MB | 150+ MB | Dependency QA |
| P3 | Shader/material cleanup, strip unused variants/features | 5 MB | 20 MB | 50 MB | Rendering QA |
