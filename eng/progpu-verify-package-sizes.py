#!/usr/bin/env python3
"""Reject incomplete, oversized or ambiguous packages before any NuGet push."""

import argparse
from pathlib import Path
import sys
import zipfile


# NuGet.org accepts a 250 MiB request, including multipart overhead.
# https://github.com/NuGet/NuGetGallery/blob/main/src/NuGetGallery/Web.config
MAX_PACKAGE_BYTES = 250 * 1024 * 1024 - 64 * 1024


def verify_sizes(directories):
    errors = []
    inventory = []
    names = set()
    for directory in directories:
        packages = sorted(directory.glob("*.nupkg"))
        symbols = sorted(directory.glob("*.snupkg"))
        if not packages:
            errors.append(f"No NuGet packages found in {directory}")
        for path in packages + symbols:
            if path.is_symlink() or not path.is_file():
                errors.append(f"Package must be a regular file: {path}")
                continue
            if path.name.casefold() in names:
                errors.append(f"Duplicate package in upload inventory: {path.name}")
            names.add(path.name.casefold())
            size = path.stat().st_size
            inventory.append((path, size))
            if not 0 < size <= MAX_PACKAGE_BYTES:
                errors.append(f"Package size {size:,} exceeds the admitted range "
                              f"1..{MAX_PACKAGE_BYTES:,} bytes: {path}")
    if errors:
        raise ValueError("\n".join(errors))
    return inventory


def verify_archive_paths(inventory):
    errors = []
    for path, _ in inventory:
        try:
            with zipfile.ZipFile(path) as archive:
                names = set()
                for entry in archive.infolist():
                    name = entry.filename
                    parts = name.rstrip("/").split("/")
                    if (not name or "\\" in name or any(part in ("", ".", "..") for part in parts)
                            or ":" in parts[0]):
                        errors.append(f"Invalid archive path in {path.name}: {name!r}")
                    key = name.rstrip("/").casefold()
                    if key in names:
                        errors.append(f"Duplicate archive path in {path.name}: {name!r}")
                    names.add(key)
        except (OSError, zipfile.BadZipFile) as error:
            errors.append(f"Invalid package archive {path}: {error}")
    if errors:
        raise ValueError("\n".join(errors))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", type=Path, nargs="+")
    args = parser.parse_args()
    try:
        inventory = verify_sizes(args.directories)
        verify_archive_paths(inventory)
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1
    largest = max(inventory, key=lambda item: item[1])
    print(f"Verified {len(inventory)} package/symbol sizes and archive paths; largest "
          f"{largest[0].name}: {largest[1]:,} / {MAX_PACKAGE_BYTES:,} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
