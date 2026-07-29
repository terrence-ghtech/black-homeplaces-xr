# Material Consolidation Summary

Current focused-scene material assets inspected: 307. Current focused-scene material slots: 2,162.

Detected merge opportunities:

- Exact duplicate groups: 12
- Near-duplicate same shader/texture groups: 8
- Single-use materials that may belong in atlases or shared material sets: 150

Realistic safe target: 250-270 unique materials after exact/near cleanup; 210-240 after selected atlases. Draw-call benefit depends on renderer grouping, shader compatibility, static batching, and co-visibility.

Use `duplicate_material_groups.csv` for group-level review. Do not merge materials that encode meaningful exhibit state, intentional color variation, transparency behavior, or interaction feedback; use `MaterialPropertyBlock` where only per-object color/scalar variation differs.
