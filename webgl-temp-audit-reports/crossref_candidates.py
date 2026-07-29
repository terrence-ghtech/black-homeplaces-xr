#!/usr/bin/env python3
"""Stage 7 cross-reference: verify no kept serialized asset, kept .meta,
ProjectSettings file, embedded package, or script path-string references any
deletion candidate. Hits are retained and reclassified.

Outputs (webgl-temp-audit-reports/baseline/):
  crossref_hits.tsv      candidate assets referenced by kept files (RETAIN)
  crossref_stats.txt
"""
from __future__ import annotations
import os, re, sys, collections

ROOT = "<PROJECT_ROOT>"
BASE = os.path.join(ROOT, "webgl-temp-audit-reports", "baseline")

GUID_RE = re.compile(rb"guid: ([0-9a-f]{32})")
GUID_JSON_RE = re.compile(rb'"guid"\s*:\s*"([0-9a-f]{32})"')
PATH_RE = re.compile(rb'"(Assets/[^"\n]{2,200})"')

YAML_EXTS = {".unity", ".prefab", ".asset", ".mat", ".controller", ".anim",
             ".overridecontroller", ".physicmaterial", ".physicsmaterial2d",
             ".playable", ".terrainlayer", ".preset", ".spriteatlas",
             ".lighting", ".giparams", ".mixer", ".shadervariants", ".vfx",
             ".brush", ".mask", ".signal", ".flare", ".fontsettings",
             ".guiskin", ".cubemap", ".renderTexture".lower(), ".shadergraph",
             ".shadersubgraph", ".inputactions", ".index", ".asmdef"}

def main():
    # candidate paths + guids
    cand_paths, cand_guid_to_path = set(), {}
    with open(os.path.join(BASE, "classification.tsv"), encoding="utf-8") as f:
        header = f.readline().rstrip("\n").split("\t")
        idx = {k: i for i, k in enumerate(header)}
        for line in f:
            p = line.rstrip("\n").split("\t")
            if p[idx["deletable"]] == "yes":
                cand_paths.add(p[idx["path"]])
                cand_guid_to_path[p[idx["guid"]].encode()] = p[idx["path"]]

    referenced = collections.defaultdict(set)   # candidate path -> set of referrers
    scanned = 0

    def scan_file(fpath, rel, kind):
        nonlocal scanned
        try:
            with open(fpath, "rb") as fh:
                data = fh.read()
        except OSError:
            return
        scanned += 1
        for m in GUID_RE.finditer(data):
            g = m.group(1)
            if g in cand_guid_to_path:
                referenced[cand_guid_to_path[g]].add(rel + " (" + kind + ")")
        if kind in ("json", "script"):
            for m in GUID_JSON_RE.finditer(data):
                g = m.group(1)
                if g in cand_guid_to_path:
                    referenced[cand_guid_to_path[g]].add(rel + " (" + kind + ")")
        if kind == "script":
            for m in PATH_RE.finditer(data):
                p = m.group(1).decode(errors="replace")
                if p in cand_paths:
                    referenced[p].add(rel + " (path-string)")

    # 1. Kept serialized assets + ALL kept .meta files under Assets
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
        for fn in filenames:
            fpath = os.path.join(dirpath, fn)
            rel = os.path.relpath(fpath, ROOT).replace(os.sep, "/")
            if fn.endswith(".meta"):
                if rel[:-5] in cand_paths:
                    continue  # candidate's own meta
                scan_file(fpath, rel, "meta")
                continue
            if rel in cand_paths:
                continue      # candidate itself
            ext = os.path.splitext(fn)[1].lower()
            if ext in YAML_EXTS:
                scan_file(fpath, rel, "yaml")
            elif ext == ".cs":
                scan_file(fpath, rel, "script")

    # 2. ProjectSettings
    ps = os.path.join(ROOT, "ProjectSettings")
    for fn in os.listdir(ps):
        if fn.endswith(".asset") or fn.endswith(".json"):
            scan_file(os.path.join(ps, fn), "ProjectSettings/" + fn, "yaml")

    # 3. Embedded packages
    pkg = os.path.join(ROOT, "Packages", "org.khronos.unitygltf")
    for dirpath, dirnames, filenames in os.walk(pkg):
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            fpath = os.path.join(dirpath, fn)
            rel = os.path.relpath(fpath, ROOT).replace(os.sep, "/")
            if ext in YAML_EXTS or fn.endswith(".meta"):
                scan_file(fpath, rel, "yaml")
            elif ext == ".cs":
                scan_file(fpath, rel, "script")

    with open(os.path.join(BASE, "crossref_hits.tsv"), "w", encoding="utf-8") as f:
        f.write("candidatePath\treferencedBy\n")
        for p in sorted(referenced):
            f.write(p + "\t" + " ; ".join(sorted(referenced[p])) + "\n")
    with open(os.path.join(BASE, "crossref_stats.txt"), "w", encoding="utf-8") as f:
        f.write(f"files scanned: {scanned}\n"
                f"candidates: {len(cand_paths)}\n"
                f"candidates referenced by kept files (RETAIN): {len(referenced)}\n")
    print(f"scanned={scanned} candidates={len(cand_paths)} hits={len(referenced)}")

if __name__ == "__main__":
    main()
