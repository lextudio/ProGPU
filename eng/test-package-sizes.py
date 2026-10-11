#!/usr/bin/env python3
"""Exercise upload admission without allocating a large package payload."""

import importlib.util
from pathlib import Path
import tempfile
import unittest


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


if __name__ == "__main__":
    unittest.main()
