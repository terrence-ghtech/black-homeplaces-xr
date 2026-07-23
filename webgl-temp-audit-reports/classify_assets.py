#!/usr/bin/env python3
"""Stage 5 asset classification for the BCAT WebGL audit.

Inputs (from webgl-temp-audit-reports/baseline/):
  asset_inventory.tsv  path/guid/type/sizeBytes/tags   (tags = dependency-root groups, '-' = unreachable)
  packed_assets.tsv    packFile/sourceAssetPath/guid/type/packedSizeBytes (from BuildReport)

Outputs (to webgl-temp-audit-reports/baseline/):
  classification.tsv   per-asset category + evidence
  classification_summary.txt
  deletion_candidates.tsv
"""
from __future__ import annotations
import collections
import os
import sys

BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "baseline")

SCRIPT_EXTS = {".cs", ".asmdef", ".asmref", ".dll", ".jslib", ".jspre", ".cginc", ".hlsl", ".shadergraph", ".shadersubgraph"}
# .shader handled separately (Shader.Find risk is checked externally; unreferenced
# shaders outside Resources cannot be loaded by string on WebGL builds, but keep
# them out of auto-deletion unless cleared).

# Paths never eligible for deletion (production tooling / settings / config /
# always-packed roots / audit outputs). Everything is compared case-sensitively
# against the repo's actual paths.
PROTECTED_PREFIXES = (
    "Assets/Editor/",
    "Assets/Settings/",
    "Assets/XR/",
    "Assets/XRI/",
    "Assets/StreamingAssets/",
    "Assets/TextMesh Pro/",
    "Assets/StarterAssets/",
    "Assets/Samples/",
    "Assets/WebGLTemplates/",
    "Assets/HDRPDefaultResources/",
    "Assets/BuildReports/",
    "Assets/BCAT_AuditReports/",
    "Assets/Resources/",
)

def is_protected(path: str) -> bool:
    if path.startswith(PROTECTED_PREFIXES):
        return True
    if "/Resources/" in path:      # any Resources folder is always packed
        return True
    if "/Editor/" in path:         # editor-only tooling anywhere
        return True
    return False

def read_tsv(name):
    with open(os.path.join(BASE, name), encoding="utf-8", errors="replace") as f:
        header = f.readline().rstrip("\n").split("\t")
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) >= len(header):
                yield dict(zip(header, parts))

def main():
    packed = collections.defaultdict(int)   # sourceAssetPath -> packed bytes (all packs)
    for row in read_tsv("packed_assets.tsv"):
        p = row["sourceAssetPath"]
        if p.startswith("Assets/"):
            packed[p] += int(row["packedSizeBytes"])

    rows = []
    for row in read_tsv("asset_inventory.tsv"):
        path = row["path"]
        tags = row["tags"]
        size = int(row["sizeBytes"])
        ext = os.path.splitext(path)[1].lower()
        in_dep = tags != "-"
        packed_bytes = packed.get(path, 0)
        in_build = packed_bytes > 0 or path.startswith("Assets/StreamingAssets/")
        is_script = ext in SCRIPT_EXTS
        protected = is_protected(path)

        if in_dep:
            cat, ev = "A", f"reachable from production roots ({tags})"
            if in_build:
                ev += f"; packed {packed_bytes} B"
        elif in_build:
            cat, ev = "B", f"packed into build ({packed_bytes} B or StreamingAssets) but unreachable from dependency roots"
        elif is_script:
            cat, ev = "D", "script/code file not in dependency graph; compiled-code inclusion and reflection use cannot be ruled out automatically"
        elif protected:
            cat, ev = "D", "not in build; under protected tooling/config path — retained by policy"
        elif ext == ".shader" or ext == ".mat" and False:
            cat, ev = "C", ""
        else:
            cat, ev = "C", "not reachable from production roots and absent from BuildReport packed assets"

        rows.append({
            "path": path, "guid": row["guid"], "type": row["type"],
            "sizeBytes": size, "packedBytes": packed_bytes, "tags": tags,
            "category": cat, "evidence": ev,
            "deletable": cat in ("B", "C") and not protected and not is_script,
            "ext": ext,
        })

    with open(os.path.join(BASE, "classification.tsv"), "w", encoding="utf-8") as f:
        f.write("path\tguid\ttype\tsizeBytes\tpackedBytes\ttags\tcategory\tdeletable\tevidence\n")
        for r in sorted(rows, key=lambda r: r["path"]):
            f.write("\t".join([r["path"], r["guid"], r["type"], str(r["sizeBytes"]),
                               str(r["packedBytes"]), r["tags"], r["category"],
                               "yes" if r["deletable"] else "no", r["evidence"]]) + "\n")

    with open(os.path.join(BASE, "deletion_candidates.tsv"), "w", encoding="utf-8") as f:
        f.write("path\tcategory\tsizeBytes\tpackedBytes\ttype\n")
        for r in sorted((r for r in rows if r["deletable"]),
                        key=lambda r: -r["sizeBytes"]):
            f.write("\t".join([r["path"], r["category"], str(r["sizeBytes"]),
                               str(r["packedBytes"]), r["type"]]) + "\n")

    by_cat = collections.Counter(r["category"] for r in rows)
    size_by_cat = collections.defaultdict(int)
    for r in rows:
        size_by_cat[r["category"]] += r["sizeBytes"]
    folders = collections.defaultdict(lambda: [0, 0])   # folder -> [count C-deletable, bytes]
    for r in rows:
        if r["deletable"]:
            top = "/".join(r["path"].split("/")[:2])
            folders[top][0] += 1
            folders[top][1] += r["sizeBytes"]

    with open(os.path.join(BASE, "classification_summary.txt"), "w", encoding="utf-8") as f:
        f.write("Category counts and source sizes:\n")
        for c in "ABCD":
            f.write(f"  {c}: {by_cat.get(c,0):5d} assets, {size_by_cat.get(c,0)/1e6:10.1f} MB source\n")
        del_rows = [r for r in rows if r["deletable"]]
        f.write(f"\nDeletion candidates: {len(del_rows)} assets, "
                f"{sum(r['sizeBytes'] for r in del_rows)/1e6:.1f} MB source, "
                f"{sum(r['packedBytes'] for r in del_rows)/1e6:.1f} MB packed (Category B only)\n")
        f.write("\nDeletion candidates by top-level folder (count, MB):\n")
        for top, (n, b) in sorted(folders.items(), key=lambda kv: -kv[1][1]):
            f.write(f"  {top}: {n} files, {b/1e6:.1f} MB\n")
    print("done")

if __name__ == "__main__":
    main()
