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
    for name in ("progpu_native.dll", "progpu_native_dawn.dll", "progpu_native_direct2d.dll") if rid.startswith("win-") else ("libprogpu_native.dylib", "libprogpu_native_dawn.dylib"):
        if not (native / name).is_file() or (native / name).stat().st_size == 0:
            errors.append(f"{rid}: missing native payload {name}")
    for name in ("progpu-native-font-dependency.json", "progpu_native_freetype.lib" if rid.startswith("win-") else "libprogpu_native_freetype.a"):
        if not (native / "sdk" / name).is_file():
            errors.append(f"{rid}: missing FreeType SDK input {name}")
    if not (native / "licenses" / "freetype" / "NOTICE.txt").is_file():
        errors.append(f"{rid}: missing FreeType notices")
    if rid.startswith("win-"):
        for name in ("wgpu_native.dll", "dxcompiler.dll", "dxil.dll", "progpu-dx12-runtime.json"):
            if not (dx12 / name).is_file() or (dx12 / name).stat().st_size == 0:
                errors.append(f"{rid}: missing DX12 input {name}")

if errors:
    print("Native package preflight found all blocking inputs:")
    print("\n".join(f" - {item}" for item in errors))
    raise SystemExit(1)
print("Native package preflight passed for: " + ", ".join(args.rids))
