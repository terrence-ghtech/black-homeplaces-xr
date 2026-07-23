# Safe Cleanup Candidates

No asset is marked safe to delete solely from this static audit. Use `GHOST_ASSET_CANDIDATES.csv` with these labels:

- `Likely safe, verify in Unity`: no static GUID/text reference found, or filename suggests backup/copy/old/test. Run Unity dependency validation before deleting.
- `Referenced indirectly`: Resources or StreamingAssets content; not safe to delete without runtime path audit.
- `Required`: static GUID/text reference found.
- `Unknown/runtime-loaded`: may be loaded by string, reflection, Resources, StreamingAssets, or package code.
- `High-risk deletion`: scripts, shaders, referenced assets, imported models/materials.

Largest candidates should be reviewed first in `GHOST_ASSET_CANDIDATES.csv`.
