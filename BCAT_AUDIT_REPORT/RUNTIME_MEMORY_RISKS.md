# Runtime Memory Risks

- Large `webgl.data.br` plus **248.3 MB** StreamingAssets video payload increases browser cache/download pressure.
- Long audio clips with preload/imported compressed settings may allocate at scene startup; see `AUDIO_AUDIT.csv`.
- Full-page PNG/document textures may decompress to much larger runtime memory than source size; see `TEXTURE_AUDIT.csv` estimated runtime memory.
- Read/Write-enabled textures/meshes can duplicate CPU/GPU memory. Static flags are in texture/mesh CSVs, but Unity import inspector validation is required.
- Video scripts should ensure `VideoPlayer.targetTexture`, audio outputs, and clips/URLs are released/cleared when modals close.
- Disabled scene objects still retain serialized references and can force dependencies into the build. See `SCENE_AUDIT.csv` and ghost candidates.
