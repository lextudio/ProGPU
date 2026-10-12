#!/usr/bin/env python3
"""Repair only the six README collisions in the qualified preview.67 artifact.

This is a bounded publication recipe, not a new product build. The immutable
artifact hash admits the original qualified binaries; each repaired package
retains every entry byte except its nuspec's single readme path.
"""

import argparse
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path, PurePosixPath
import subprocess
import zipfile


SOURCE = "915c0bded45b283163bc85489111b0d74a4d9d44"
ARTIFACT_ID = 11720419571
ARTIFACT_SHA256 = "509753d4d6a5b2aafa4fe7555bcd5b126db04c160371dd4962bd18604b8daa5b"
VERSION = "0.1.0-preview.67"
REPAIRS = {
    "ProGPU.Hmi": "docs/hmi-designer.md",
    "ProGPU.Hmi.Modbus": "docs/hmi-control-integrations.md",
    "ProGPU.Hmi.Mqtt": "docs/hmi-control-integrations.md",
    "ProGPU.WinUI.Hmi": "docs/hmi-designer.md",
    "ProGPU.WinUI.Hmi.Designer": "docs/hmi-designer.md",
    "ProGPU.WinUI.Hmi.Workplace": "docs/hmi-dcs-workplace.md",
}
NEW_README = "hmi-package-readme.md"
SPEC = importlib.util.spec_from_file_location(
    "package_admission", Path(__file__).with_name("progpu-verify-package-sizes.py"))
ADMISSION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ADMISSION)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def repair_readme(data, repository_readme, specialized_readme):
    require(repository_readme != specialized_readme, "Readme identities must be distinct")
    output = io.BytesIO()
    expected = {}
    with zipfile.ZipFile(io.BytesIO(data)) as source:
        entries = source.infolist()
        readmes = [info for info in entries if info.filename == "README.md"]
        require(len(readmes) == 2, "Expected exactly two original README entries")
        require(sorted(source.read(info) for info in readmes) ==
                sorted((repository_readme, specialized_readme)), "Original source README bytes differ")
        nuspecs = [info for info in entries if info.filename.endswith(".nuspec")]
        require(len(nuspecs) == 1, "Expected one original nuspec")
        original_nuspec = source.read(nuspecs[0])
        old = b"<readme>README.md</readme>"
        new = b"<readme>hmi-package-readme.md</readme>"
        require(original_nuspec.count(old) == 1, "Unexpected original nuspec readme")
        with zipfile.ZipFile(output, "w") as target:
            target.comment = source.comment
            for info in entries:
                # Reading by ZipInfo is essential: name lookup hides duplicates.
                content = source.read(info)
                updated = copy.copy(info)
                if info.filename == "README.md" and content == specialized_readme:
                    updated.filename = NEW_README
                    updated.orig_filename = NEW_README
                elif info is nuspecs[0]:
                    content = content.replace(old, new)
                require(updated.filename.casefold() not in expected, "Unexpected duplicate archive entry")
                expected[updated.filename.casefold()] = (updated.filename, digest(content))
                target.writestr(updated, content)
    repaired = output.getvalue()
    with zipfile.ZipFile(io.BytesIO(repaired)) as target:
        actual = [(info.filename, digest(target.read(info))) for info in target.infolist()]
        require(len(actual) == len(expected) and sorted(actual) == sorted(expected.values()),
                "Repaired payload bytes differ")
    return repaired, dict(actual)


def recover(artifact, output, source_root):
    with artifact.open("rb") as stream:
        require(hashlib.file_digest(stream, "sha256").hexdigest() == ARTIFACT_SHA256,
                "Original artifact SHA-256 differs")
    require(not output.exists(), "Recovery output must be new")
    source_file = lambda path: subprocess.check_output(
        ["git", "-C", str(source_root), "show", f"{SOURCE}:{path}"])
    repository_readme = source_file("README.md")
    original_docs = {name: source_file(path) for name, path in REPAIRS.items()}
    receipt = {"source_commit": SOURCE, "artifact_id": ARTIFACT_ID,
               "original_artifact_sha256": ARTIFACT_SHA256, "packages": []}
    repaired_ids = set()
    with zipfile.ZipFile(artifact) as archive:
        entries = archive.infolist()
        require(len(entries) == 122, "Expected all 122 qualified package/symbol files")
        require(len({info.filename.casefold() for info in entries}) == 122,
                "Duplicate outer artifact paths")
        for info in entries:
            path = PurePosixPath(info.filename)
            require(len(path.parts) == 3 and path.parts[0] in ("packages", "packages-avalonia")
                    and path.parts[1] == "Release" and path.suffix in (".nupkg", ".snupkg"),
                    f"Unexpected artifact path: {info.filename}")
        require(sum(PurePosixPath(info.filename).suffix == ".nupkg" for info in entries) == 63,
                "Expected all 63 qualified packages")
        for info in entries:
            original = archive.read(info)
            data = original
            entry_hashes = None
            for package_id in REPAIRS:
                if PurePosixPath(info.filename).name == f"{package_id}.{VERSION}.nupkg":
                    data, entry_hashes = repair_readme(original, repository_readme, original_docs[package_id])
                    repaired_ids.add(package_id)
                    break
            path = output / info.filename
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            receipt["packages"].append({"path": info.filename, "original_sha256": digest(original),
                "output_sha256": digest(data), "repaired": data != original,
                "repaired_entry_sha256": entry_hashes})
    require(repaired_ids == set(REPAIRS), "Missing affected HMI packages")
    inventory = ADMISSION.verify_sizes([output / "packages/Release", output / "packages-avalonia/Release"])
    ADMISSION.verify_archive_paths(inventory)
    require(sum(row["repaired"] for row in receipt["packages"]) == 6,
            "Only six package archives may change; all symbols must remain identical")
    (output / "readme-recovery-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print("Verified all 122 original artifacts: six README-only repairs, 116 byte-identical archives.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifact", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--source-root", type=Path, default=Path(__file__).resolve().parent.parent)
    args = parser.parse_args()
    recover(args.artifact, args.output, args.source_root)


if __name__ == "__main__":
    main()
