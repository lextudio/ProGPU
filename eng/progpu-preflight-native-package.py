#!/usr/bin/env python3
"""Aggregate native-package input diagnostics before an expensive package closure."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--root", required=True, type=Path)
parser.add_argument("--rids", nargs="+", required=True)
args = parser.parse_args()

errors = []
for rid in args.rids:
    native = args.root / "progpu-native" / "package" / "runtimes" / rid / "native"
    dx12 = args.root / "progpu-dx12" / "package" / rid / "native"
    if rid.startswith("win-"):
        native_libraries = ("progpu_native.dll", "progpu_native_dawn.dll", "progpu_native_direct2d.dll")
    elif rid.startswith("linux-"):
        native_libraries = ("libprogpu_native.so", "libprogpu_native_dawn.so")
    elif rid.startswith("osx-"):
        native_libraries = ("libprogpu_native.dylib", "libprogpu_native_dawn.dylib")
    else:
        errors.append(f"{rid}: unsupported native package RID")
        continue
    for name in native_libraries:
        if not (native / name).is_file() or (native / name).stat().st_size == 0:
            errors.append(f"{rid}: missing native payload {name}")
    for name in ("progpu-native-font-dependency.json", "progpu_native_freetype.lib" if rid.startswith("win-") else "libprogpu_native_freetype.a"):
        if not (native / "sdk" / name).is_file():
            errors.append(f"{rid}: missing FreeType SDK input {name}")
    if not (native / "licenses" / "freetype" / "NOTICE.txt").is_file():
        errors.append(f"{rid}: missing FreeType notices")
    icu_library = "progpu_native_edit_icu.lib" if rid.startswith("win-") else "libprogpu_native_edit_icu.a"
    for name in ("progpu-native-edit-word-dependency.json", icu_library):
        if not (native / "sdk" / name).is_file():
            errors.append(f"{rid}: missing private ICU SDK input {name}")
    if not (native / "licenses" / "edit-word-icu" / "LICENSE").is_file():
        errors.append(f"{rid}: missing private ICU notice")
    if rid.startswith("win-"):
        for name in ("wgpu_native.dll", "dxcompiler.dll", "dxil.dll", "progpu-dx12-runtime.json"):
            if not (dx12 / name).is_file() or (dx12 / name).stat().st_size == 0:
                errors.append(f"{rid}: missing DX12 input {name}")

if errors:
    print("Native package preflight found all blocking inputs:")
    print("\n".join(f" - {item}" for item in errors))
    raise SystemExit(1)
print("Native package preflight passed for: " + ", ".join(args.rids))
