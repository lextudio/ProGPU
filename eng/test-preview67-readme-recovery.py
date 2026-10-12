#!/usr/bin/env python3
"""Verify bounded README publication repair without manufacturing runtime files."""

import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
import warnings
import zipfile


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


REPAIR = load("readme_recovery", "progpu-recover-preview67-readmes.py")
QUALIFICATION = load("readme_qualification", "progpu-verify-preview67-qualification.py")


class ReadmeRepairTests(unittest.TestCase):
    def package(self, extra=(), readme=b"<readme>README.md</readme>"):
        stream = io.BytesIO()
        entries = [("A.nuspec", b"<package>" + readme + b"<repository commit='original'/></package>"),
                   ("README.md", b"repository readme"), ("README.md", b"specialized readme"),
                   ("notices/original.txt", b"original owned metadata, unmodified"), *extra]
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with zipfile.ZipFile(stream, "w") as archive:
                for name, data in entries:
                    archive.writestr(name, data)
        return stream.getvalue()

    def repair(self, data):
        return REPAIR.repair_readme(data, b"repository readme", b"specialized readme")

    def test_only_specialized_readme_path_and_single_nuspec_element_change(self):
        original = self.package()
        output, hashes = self.repair(original)
        with zipfile.ZipFile(io.BytesIO(output)) as archive:
            self.assertEqual(len(archive.infolist()), 4)
            self.assertEqual(archive.read("README.md"), b"repository readme")
            self.assertEqual(archive.read("hmi-package-readme.md"), b"specialized readme")
            self.assertEqual(archive.read("notices/original.txt"), b"original owned metadata, unmodified")
            self.assertEqual(archive.read("A.nuspec"), b"<package><readme>hmi-package-readme.md</readme>"
                             b"<repository commit='original'/></package>")
            self.assertEqual(hashes["notices/original.txt"], REPAIR.digest(archive.read("notices/original.txt")))
        self.assertEqual(output, self.repair(original)[0])

    def test_mismatched_source_document_and_repeat_repair_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "source README bytes differ"):
            REPAIR.repair_readme(self.package(), b"wrong source", b"specialized readme")
        output, _ = self.repair(self.package())
        with self.assertRaisesRegex(ValueError, "exactly two"):
            self.repair(output)

    def test_additional_collision_and_changed_metadata_are_rejected(self):
        for extra in (("README.md", b"third readme"), ("hmi-package-readme.md", b"existing"),
                      ("NOTICES/ORIGINAL.TXT", b"ambiguous"), ("B.nuspec", b"other")):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                self.repair(self.package(extra=[extra]))
        with self.assertRaisesRegex(ValueError, "nuspec readme"):
            self.repair(self.package(readme=b"<readme>elsewhere.md</readme>"))

    def test_unqualified_artifact_fails_before_creating_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifact = root / "input.zip"
            artifact.write_bytes(self.package())
            with self.assertRaisesRegex(ValueError, "SHA-256 differs"):
                REPAIR.recover(artifact, root / "output", root)
            self.assertFalse((root / "output").exists())


class QualificationTests(unittest.TestCase):
    def test_build_requires_whole_successful_exact_source(self):
        run = {"head_sha": "original", "path": ".github/workflows/build.yml",
               "status": "completed", "conclusion": "success"}
        jobs = {"total_count": 59, "jobs": [{"conclusion": "success"} for _ in range(59)]}
        QUALIFICATION.verify_build(run, jobs, "original")
        with self.assertRaises(ValueError):
            QUALIFICATION.verify_build(run, jobs, "different")
        jobs["jobs"][0]["conclusion"] = "skipped"
        with self.assertRaises(ValueError):
            QUALIFICATION.verify_build(run, jobs, "original")

    def test_only_original_publisher_can_be_failed(self):
        run = {"head_sha": QUALIFICATION.SOURCE, "path": ".github/workflows/release.yml",
               "status": "completed"}
        jobs = {"total_count": 20, "jobs": [{"id": index, "conclusion": "success"} for index in range(19)]
                + [{"id": QUALIFICATION.PUBLISHER, "conclusion": "failure"}]}
        QUALIFICATION.verify_release(run, jobs)
        jobs["jobs"][0]["conclusion"] = "failure"
        with self.assertRaises(ValueError):
            QUALIFICATION.verify_release(run, jobs)
        jobs["jobs"][0]["conclusion"] = "success"
        jobs["jobs"].pop()
        with self.assertRaises(ValueError):
            QUALIFICATION.verify_release(run, jobs)


if __name__ == "__main__":
    unittest.main()
