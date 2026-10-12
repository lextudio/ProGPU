#!/usr/bin/env python3
"""Exercise upload admission without allocating a large package payload."""

import importlib.util
from pathlib import Path
import tempfile
import unittest
import warnings
import zipfile


spec = importlib.util.spec_from_file_location(
    "package_sizes", Path(__file__).with_name("progpu-verify-package-sizes.py"))
sizes = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sizes)


class PackageSizeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="progpu-package-sizes.")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def package(self, name, size):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("wb") as stream:
            stream.truncate(size)
        return path

    def test_exact_boundary_and_symbols(self):
        package = self.package("A.1.0.0.nupkg", sizes.MAX_PACKAGE_BYTES)
        symbols = self.package("A.1.0.0.snupkg", 1)
        self.assertEqual(sizes.verify_sizes([self.root]),
                         [(package, sizes.MAX_PACKAGE_BYTES), (symbols, 1)])

    def test_entire_inventory_is_rejected_before_publication(self):
        self.package("runtime/A.1.0.0.nupkg", 1)
        self.package("runtime/B.1.0.0.snupkg", sizes.MAX_PACKAGE_BYTES + 1)
        self.package("avalonia/C.1.0.0.nupkg", 298_768_221)
        with self.assertRaises(ValueError) as rejection:
            sizes.verify_sizes([self.root / "runtime", self.root / "avalonia"])
        self.assertIn("B.1.0.0.snupkg", str(rejection.exception))
        self.assertIn("C.1.0.0.nupkg", str(rejection.exception))

    def test_empty_missing_and_zero_length_inputs(self):
        for directory in [self.root, self.root / "missing"]:
            with self.subTest(directory=directory), self.assertRaises(ValueError):
                sizes.verify_sizes([directory])
        self.package("A.1.0.0.nupkg", 0)
        with self.assertRaises(ValueError):
            sizes.verify_sizes([self.root])

    def test_duplicate_upload_names_and_links(self):
        self.package("a/A.1.0.0.nupkg", 1)
        self.package("b/a.1.0.0.nupkg", 1)
        with self.assertRaisesRegex(ValueError, "Duplicate package"):
            sizes.verify_sizes([self.root / "a", self.root / "b"])
        link = self.root / "c/A.1.0.0.nupkg"
        link.parent.mkdir()
        link.symlink_to(self.root / "a/A.1.0.0.nupkg")
        with self.assertRaisesRegex(ValueError, "regular file"):
            sizes.verify_sizes([link.parent])


class ArchivePathTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="progpu-package-paths.")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def package(self, name, entries):
        path = self.root / name
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with zipfile.ZipFile(path, "w") as archive:
                for entry in entries:
                    archive.writestr(entry, b"owned archive-path control")
        return (path, path.stat().st_size)

    def test_distinct_readmes_and_same_basename_in_different_directories(self):
        inventory = [self.package("A.nupkg", ["README.md", "hmi-package-readme.md",
                      "lib/net10.0/A.dll", "ref/net10.0/A.dll"])]
        sizes.verify_archive_paths(inventory)

    def test_duplicate_readmes_and_case_collisions_are_rejected(self):
        for entries in (["README.md", "README.md"], ["docs/Readme.md", "docs/readme.md"]):
            with self.subTest(entries=entries):
                package = self.package("A.nupkg", entries)
                with self.assertRaisesRegex(ValueError, "Duplicate archive path"):
                    sizes.verify_archive_paths([package])

    def test_symbol_archives_and_whole_inventory_are_checked(self):
        inventory = [self.package("A.nupkg", ["README.md"]),
                     self.package("A.snupkg", ["lib/A.pdb", "lib/A.pdb"]),
                     self.package("B.nupkg", ["README.md", "README.md"])]
        with self.assertRaises(ValueError) as rejection:
            sizes.verify_archive_paths(inventory)
        self.assertIn("A.snupkg", str(rejection.exception))
        self.assertIn("B.nupkg", str(rejection.exception))

    def test_ambiguous_paths_and_non_zip_payloads_are_rejected(self):
        for name in ("/README.md", "../README.md", "docs/../README.md", "./README.md",
                     "docs//README.md", "docs\\README.md", "C:/README.md"):
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, "Invalid archive path"):
                sizes.verify_archive_paths([self.package("A.nupkg", [name])])
        path = self.root / "broken.nupkg"
        path.write_bytes(b"not an archive")
        with self.assertRaisesRegex(ValueError, "Invalid package archive"):
            sizes.verify_archive_paths([(path, path.stat().st_size)])


if __name__ == "__main__":
    unittest.main()
