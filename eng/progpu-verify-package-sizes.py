#!/usr/bin/env python3
"""Reject an incomplete or oversized upload inventory before any NuGet push."""

import argparse
from pathlib import Path
import sys


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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", type=Path, nargs="+")
    args = parser.parse_args()
    try:
        inventory = verify_sizes(args.directories)
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1
    largest = max(inventory, key=lambda item: item[1])
    print(f"Verified {len(inventory)} package/symbol sizes; largest "
          f"{largest[0].name}: {largest[1]:,} / {MAX_PACKAGE_BYTES:,} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
