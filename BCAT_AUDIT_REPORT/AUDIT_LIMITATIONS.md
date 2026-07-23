# Audit Limitations

- `/mnt/data/LastBuild.buildreport` was not present, so exact packed-size attribution, shader variant sizes, serialized file sizes, and build-report dependency tables could not be extracted.
- Unity Editor was not launched; exact imported texture formats, mesh vertex counts, triangle counts, material slot counts, and scene missing-reference validation require Unity APIs.
- Static YAML/GUID scanning can miss runtime string construction, reflection, package internals, and code-generated references.
- `webgl.data.br` is compressed packed data; without build report or decompression/Unity object table, source-to-packed attribution is estimated.
- Deletion safety is intentionally conservative; no asset is declared safe to remove without Unity dependency verification.
