#!/usr/bin/env python3
"""Extract a .unitypackage (tar.gz of GUID folders) into the project: extract_unitypackage.py pkg [projectRoot]"""
import os, sys, tarfile
pkg, root = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else ".")
entries = {}
with tarfile.open(pkg, "r:gz") as tf:
    for m in tf.getmembers():
        parts = m.name.strip("./").split("/")
        if len(parts) != 2 or not m.isfile():
            continue
        guid, kind = parts
        entries.setdefault(guid, {})[kind] = tf.extractfile(m).read()
n = 0
for guid, e in entries.items():
    if "pathname" not in e:
        continue
    path = e["pathname"].decode().splitlines()[0].strip()
    dst = os.path.join(root, path)
    if "asset" in e:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        open(dst, "wb").write(e["asset"])
        n += 1
    else:
        os.makedirs(dst, exist_ok=True)
    if "asset.meta" in e:
        open(dst + ".meta", "wb").write(e["asset.meta"])
print(f"extracted {n} assets")
