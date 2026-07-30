# Terrain Recovery Report — Assets/New Terrain.asset (2026-07-29)

Status: **repair complete and verified in-editor.** Post-repair native-build
confirmation was intentionally skipped (repeated external-drive disconnects
during builds — see § Skipped validation).

## Symptoms
Terrain, mountains, trees, and landscaping absent; `Terrain_main`'s Terrain
component showed *Terrain Asset Missing* and the TerrainCollider *Missing
(Terrain Data)*; every build/import logged
`Unknown error occurred while loading 'Assets/New Terrain.asset'`.

## Root cause — two independent defects

1. **Git newline-normalization mangled the binary asset at commit.**
   `.gitattributes` declared `*.asset text` (line 69). That rule is correct
   for Unity's YAML assets but `New Terrain.asset` (TerrainData) is binary.
   On checkin, git collapsed the file's single incidental `0D 0A` byte pair
   to `0A`, storing a blob **one byte shorter** than the file's own
   SerializedFile header declares (3,362,091 vs 3,362,092). The working copy
   remained intact through July 28 — the truncation lived only in the
   committed blob until the working tree was refreshed from git on
   **July 29 at 10:21**, which materialized the damaged file. The lost byte
   sat inside the `SplatAlpha 0` alphamap pixel block.
   A second victim with the same signature exists:
   `Assets/DevDen Arch Viz Scotland/Scenes/ArchViz/LightingData.asset`
   (−2 bytes, unused asset-store demo; intentionally not repaired).

2. **The scene's Terrain component reference was nulled in the working tree
   only** (correction, 2026-07-30: git archaeology during commit preparation
   proved the *committed* scene always held the correct GUID on both the
   Terrain and TerrainCollider components). The `m_TerrainData: {fileID: 0}`
   found on disk was created during the live July 29 editor session: the
   scene was saved while the truncated terrain asset was unloadable, so
   Unity serialized the dead reference as null. A downstream symptom of
   defect #1, not an independent scene defect.

## Recovery performed

1. **Byte-exact file repair** (validated in scratchpad before install):
   the deployed July 23 WebGL build's TerrainData was extracted and used as
   ground truth; a minimal-mismatch split search found **cost = 0** at exactly
   one position — head aligned + tail shifted-by-one match the build
   **byte-for-byte across all 1,398,099 pixel bytes** — and the build holds
   `0D 0A` at precisely that spot. Repair: insert `0x0D` at absolute offset
   **2,210,075**. Result validated by strict full type-tree deserialization,
   a throwaway-Unity-project load (LOADED OK, zero errors), rendered terrain
   proofs, and an independent hillshade reproducing the production geography
   (NW ridge ranges, eastern dunes, lagoon channel + circular pond, house-pad
   leveling imprints).
2. **Installed** over `Assets/New Terrain.asset` (approved 2026-07-29):
   **3,362,092 bytes**, SHA-256
   `b6ccb8f10059be138f5ad12e8a485b147d35aa53e33ee5d59db3eef9e00fd9b6`.
   `.meta` untouched (GUID `07909d56789714b9aa109b9b0674689f` preserved).
3. **One-line scene fix** (approved separately; scene backed up to the
   session scratchpad first). Line 162168:
   `m_TerrainData: {fileID: 0}` →
   `m_TerrainData: {fileID: 15600000, guid: 07909d56789714b9aa109b9b0674689f, type: 2}`
   — verified by diff to be the only changed line (+56 bytes). This restored
   exactly the committed value (see corrected root cause #2), so the scene
   carries **no net change** in the repair commit; residual field-order
   re-serialization noise from the same editor session was reverted during
   commit preparation.
4. **`.gitattributes` protection appended** (after the `*.asset text` rule,
   so it wins): `/Assets/New\ Terrain.asset -text` with an explanatory
   comment. **Do not commit the repaired asset before this attribute is in
   effect**, or checkin will re-collapse the CRLF and recreate the damage.

## Validation completed ✅

- Repaired file: header/actual size agree; strict type-tree parse of all
  three objects (TerrainData 513×513, 7 layers, 66 trees, 8 detail
  prototypes; 2× SplatAlpha 512×512, image sizes exact).
- SplatAlpha 0 pixel block byte-identical to the known-good deployed build.
- Editor reimport of the production project: **zero**
  `Unknown error occurred while loading` (import completes in 0.03 s).
- Scene verification (batch, scene NOT saved): Terrain **and** TerrainCollider
  both reference `New Terrain`; GUID matches; **7/7 terrain layers resolve**;
  trees/details present.
- Rendered verification shots (session scratchpad `terrain_repair/
  install_proof2/`): terrain surface, textured mountains, lagoon + circular
  pond + bridge, boat, house pad with both fenced yards, palm groves — all
  matching the July 28 reference captures.
- Production validation audit: **OVERALL PASS**.

## Validation intentionally skipped ⚠️

- **Post-repair native desktop build.** Attempted; the T9 external drive
  disconnected mid-build (third dropout that day: burst of
  `CreateDirectory … failed` then an LMDB segfault in the editor), and per
  owner instruction further retries were skipped. Marked **not completed due
  to repeated external drive disconnects during build**. Note: before the
  crash, the build had begun compiling `URP/Terrain/Lit` shader variants —
  itself evidence the repaired terrain now enters build content. The existing
  `Builds/` artifacts predate the terrain repair and still show the old
  behavior; the first successful build on stable storage will be the
  confirming artifact.

## Owner follow-ups

1. In-editor visual pass: open BH_XR_MainScene, confirm terrain/mountains/
   layers/trees/lagoon/pond/house pad against the July 28 screenshots.
2. Commit sequence (git is owner-side): verify
   `git check-attr text -- "Assets/New Terrain.asset"` → `text: unset`;
   stage `.gitattributes` + the asset together; confirm
   `git show ":Assets/New Terrain.asset" | shasum -a 256` equals the working
   file's hash (`b6ccb8f1…fd9b6`).
3. Rebuild all three targets from stable storage (internal disk recommended;
   the T9 has now produced three build-time dropouts: 2026-07-23 and twice on
   2026-07-29).
4. Optional: repair or re-bake the DevDen `LightingData.asset` (−2 bytes,
   same mechanism, unused demo scene); consider `-text` overrides for other
   binary `.asset` types (LightingData, NavMesh) before they are next edited.

## Production files modified for this recovery
- `Assets/New Terrain.asset` — replaced with the repaired file (only content change).
- `Assets/BH_XR_MainScene.unity` — one line restored to the committed value
  (162168); net-zero versus HEAD after commit preparation; backup of the
  damaged working copy in the session scratchpad.
- `.gitattributes` — comment + `"/Assets/New Terrain.asset" -text` appended.
- `Assets/Editor/BCaTProduction/TerrainInstallVerification.cs` — temporary
  verification tool, added then **removed**; zero residual footprint.
