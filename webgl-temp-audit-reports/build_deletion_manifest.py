#!/usr/bin/env python3
"""Stage 7: build the final deletion manifest from classification + cross-refs
+ retention policy, and compute maximal Trash units (whole folders where every
contained non-meta file is being deleted, else individual files).

Outputs (webgl-temp-audit-reports/baseline/):
  deletion_manifest.tsv      every file to delete, with evidence
  retained_reclassified.tsv  candidates retained by cross-ref/policy, with reason
  trash_units.txt            what will actually be moved to Trash (folders/files)
"""
from __future__ import annotations
import os, subprocess, collections

ROOT = "/Volumes/T9(Samsung)/UnityProjects/BCAT_6000_4_TEST"
BASE = os.path.join(ROOT, "webgl-temp-audit-reports", "baseline")

# R1: exact candidates referenced by kept files (crossref_hits.tsv is read below)
# R2: folder prefixes referenced as path strings by scripts (production tooling)
TOOLING_PREFIXES = (
    "Assets/BCaT/Exhibits/BlackKitchen/",
    "Assets/BCaT/Exhibits/PrivacyLawExhibit/",
    "Assets/BCaT_assets/LindaLeaks/",
    "Assets/BCaT_assets/Meshell_Sturgis/articles/Pages/",
    "Assets/BakedVertexPaintMeshes/",
    "Assets/RealBlend/VertexColorPalettes/",
)
# R3: policy — retain all project-authored BCaT content except exact-name
# source copies of videos that are served from StreamingAssets in production.
BCAT_PREFIXES = ("Assets/BCaT/", "Assets/BCaT_assets/")
BCAT_VIDEO_DUP_ALLOWLIST = {
    "Assets/BCaT_assets/Ri/and that is the truth - you know what I'm meaning_720p.mp4",
    "Assets/BCaT_assets/Ri/you don't know about style my darling_720p.mp4",
    "Assets/BCaT_assets/Ri/such lovely gravy_720p.mp4",
    "Assets/BCaT_assets/Meshell_Sturgis/subjected_to_recognition_720p.mp4",
}

def main():
    crossref_hits = set()
    with open(os.path.join(BASE, "crossref_hits.tsv"), encoding="utf-8") as f:
        f.readline()
        for line in f:
            crossref_hits.add(line.split("\t")[0])

    delete, retained = [], []
    with open(os.path.join(BASE, "classification.tsv"), encoding="utf-8") as f:
        header = f.readline().rstrip("\n").split("\t")
        idx = {k: i for i, k in enumerate(header)}
        for line in f:
            p = line.rstrip("\n").split("\t")
            path, size, cat = p[idx["path"]], int(p[idx["sizeBytes"]]), p[idx["category"]]
            if p[idx["deletable"]] != "yes":
                continue
            if path in crossref_hits:
                retained.append((path, "referenced by kept file (crossref_hits.tsv)"))
                continue
            if path.startswith(TOOLING_PREFIXES):
                retained.append((path, "under folder path referenced by production tooling scripts"))
                continue
            if path.startswith(BCAT_PREFIXES) and path not in BCAT_VIDEO_DUP_ALLOWLIST:
                retained.append((path, "policy: project-authored BCaT content retained (insufficient evidence of abandonment)"))
                continue
            delete.append((path, size, cat))

    tracked = set(subprocess.run(["git", "-C", ROOT, "ls-files", "-z", "Assets"],
                                 capture_output=True).stdout.decode().split("\0"))

    with open(os.path.join(BASE, "deletion_manifest.tsv"), "w", encoding="utf-8") as f:
        f.write("path\tsizeBytes\tcategory\tgitTracked\tevidence\n")
        for path, size, cat in sorted(delete):
            ev = ("Category C: unreachable from all production dependency roots (Unity AssetDatabase closure); "
                  "absent from BuildReport packedAssets; no GUID reference from any kept serialized asset, kept .meta, "
                  "ProjectSettings, or embedded package; no path-string reference from any script; "
                  "not in Resources/StreamingAssets/settings roots; recoverable via Trash"
                  + (" and git" if path in tracked else ""))
            if cat == "B":
                ev = ev.replace("Category C: unreachable", "Category B: packed but unreachable")
            f.write(f"{path}\t{size}\t{cat}\t{'yes' if path in tracked else 'no'}\t{ev}\n")

    with open(os.path.join(BASE, "retained_reclassified.tsv"), "w", encoding="utf-8") as f:
        f.write("path\tnewCategory\treason\n")
        for path, reason in sorted(retained):
            f.write(f"{path}\tD\t{reason}\n")

    # ---- maximal trash units
    delete_set = {p for p, _, _ in delete}
    dir_all_deleted = {}

    def walk(d):
        """returns True if every non-meta file under d is in delete_set (and d has content)"""
        ok, has_any = True, False
        try:
            entries = sorted(os.listdir(os.path.join(ROOT, d)))
        except OSError:
            return False
        for e in entries:
            rel = d + "/" + e
            full = os.path.join(ROOT, rel)
            if os.path.isdir(full):
                sub_ok = walk(rel)
                sub_has = dir_all_deleted.get(rel, (False, False))[1]
                if sub_has:
                    has_any = True
                    ok = ok and sub_ok
                # empty subdir: ignore for ok, but it will ride along with parent
            elif not e.endswith(".meta"):
                has_any = True
                if rel not in delete_set:
                    ok = False
        dir_all_deleted[d] = (ok, has_any)
        return ok

    walk("Assets")
    units = []
    def collect(d):
        ok, has_any = dir_all_deleted.get(d, (False, False))
        if ok and has_any and d != "Assets":
            units.append(d)
            return
        try:
            entries = sorted(os.listdir(os.path.join(ROOT, d)))
        except OSError:
            return
        for e in entries:
            rel = d + "/" + e
            if os.path.isdir(os.path.join(ROOT, rel)):
                collect(rel)
            elif rel in delete_set:
                units.append(rel)
    collect("Assets")

    with open(os.path.join(BASE, "trash_units.txt"), "w", encoding="utf-8") as f:
        for u in units:
            f.write(u + "\n")

    total = sum(s for _, s, _ in delete)
    print(f"delete files: {len(delete)} ({total/1e6:.1f} MB source)")
    print(f"retained (reclassified D): {len(retained)}")
    print(f"trash units: {len(units)} ({sum(1 for u in units if os.path.isdir(os.path.join(ROOT,u)))} folders)")

if __name__ == "__main__":
    main()
