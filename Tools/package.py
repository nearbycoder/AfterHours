#!/usr/bin/env python3
"""Packs the built players into release zips under Builds/release/.

  python3 Tools/package.py [linux] [macos] [windows]   (default: whichever builds exist)

- Linux:   Builds/Linux        -> AfterHours-v<ver>-linux-x86_64.zip, with the AfterHours.sh launcher
- macOS:   Builds/macOS/*.app  -> AfterHours-v<ver>-macos-universal.zip (unsigned)
- Windows: Builds/Windows      -> AfterHours-v<ver>-windows-x86_64.zip

Unix permissions are kept (the executables and the launcher stay executable after unzipping),
Unity's do-not-ship folders are left out, and each zip gets a README.txt. The version comes
from ProjectSettings (bundleVersion). Uses only the standard library; the machine has no `zip`.
"""
import os
import re
import stat
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILDS = os.path.join(ROOT, "Builds")
OUT = os.path.join(BUILDS, "release")
HERE = os.path.join(ROOT, "Tools", "release")
SKIP = ("_BackUpThisFolder_ButDontShipItWithYourGame", "_BurstDebugInformation_DoNotShip")


def version():
    text = open(os.path.join(ROOT, "ProjectSettings", "ProjectSettings.asset"), encoding="utf-8").read()
    return re.search(r"bundleVersion: (\S+)", text).group(1)


def add_file(z, src, arc, mode=None):
    st = os.stat(src)
    info = zipfile.ZipInfo(arc, date_time=(2026, 10, 6, 12, 0, 0))
    info.external_attr = ((mode if mode is not None else st.st_mode) & 0xFFFF) << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    with open(src, "rb") as f:
        z.writestr(info, f.read(), compresslevel=9)


def add_text(z, arc, text):
    info = zipfile.ZipInfo(arc, date_time=(2026, 10, 6, 12, 0, 0))
    info.external_attr = (stat.S_IFREG | 0o644) << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    z.writestr(info, text)


def add_tree(z, src_dir, arc_root):
    count = 0
    for dirpath, dirnames, filenames in os.walk(src_dir):
        dirnames[:] = sorted(d for d in dirnames if not d.endswith(SKIP))
        for name in sorted(filenames):
            src = os.path.join(dirpath, name)
            if os.path.islink(src):
                raise SystemExit(f"symlink in build, not supported: {src}")
            add_file(z, src, os.path.join(arc_root, os.path.relpath(src, src_dir)))
            count += 1
    return count


def readme(name, ver):
    return open(os.path.join(HERE, name), encoding="utf-8").read().replace("{version}", ver)


def pack(kind, ver):
    os.makedirs(OUT, exist_ok=True)
    if kind == "linux":
        src = os.path.join(BUILDS, "Linux")
        if not os.path.exists(os.path.join(src, "AfterHours.x86_64")):
            return None
        out = os.path.join(OUT, f"AfterHours-v{ver}-linux-x86_64.zip")
        with zipfile.ZipFile(out, "w") as z:
            n = add_tree(z, src, "AfterHours")
            add_file(z, os.path.join(HERE, "AfterHours.sh"), "AfterHours/AfterHours.sh", stat.S_IFREG | 0o755)
            add_text(z, "AfterHours/README.txt", readme("README-linux.txt", ver))
    elif kind == "macos":
        apps = [d for d in os.listdir(os.path.join(BUILDS, "macOS"))] if os.path.isdir(os.path.join(BUILDS, "macOS")) else []
        apps = [a for a in apps if a.endswith(".app")]
        if not apps:
            return None
        app = apps[0]
        out = os.path.join(OUT, f"AfterHours-v{ver}-macos-universal.zip")
        with zipfile.ZipFile(out, "w") as z:
            n = add_tree(z, os.path.join(BUILDS, "macOS", app), os.path.join("AfterHours", app))
            add_text(z, "AfterHours/README.txt", readme("README-macos.txt", ver))
    elif kind == "windows":
        src = os.path.join(BUILDS, "Windows")
        if not os.path.exists(os.path.join(src, "AfterHours.exe")):
            return None
        out = os.path.join(OUT, f"AfterHours-v{ver}-windows-x86_64.zip")
        with zipfile.ZipFile(out, "w") as z:
            n = add_tree(z, src, "AfterHours")
    else:
        raise SystemExit(f"unknown platform {kind}")
    print(f"wrote {os.path.relpath(out, ROOT)}: {n} files, {os.path.getsize(out) / 1e6:.0f} MB")
    return out


def main():
    ver = version()
    kinds = [a for a in sys.argv[1:] if not a.startswith("-")] or ["linux", "macos", "windows"]
    made = [k for k in kinds if pack(k, ver)]
    if not made:
        raise SystemExit("no builds found; run Tools/unity.sh build-linux / build-mac first")


if __name__ == "__main__":
    main()
