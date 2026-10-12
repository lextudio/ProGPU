import importlib.util
import io
from pathlib import Path
import tempfile
import sys
import types
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("diagnostics", Path(__file__).parents[1] / "progpu-webscene-timeout-diagnostics.py")
diagnostics = importlib.util.module_from_spec(spec)
spec.loader.exec_module(diagnostics)


class OriginalProcess:
    def __init__(self, status, output):
        self.status = status
        self.stdout = io.BytesIO(output)
    def __enter__(self): return self
    def __exit__(self, *args): pass
    def wait(self): return self.status


class TimeoutDiagnosticsTests(unittest.TestCase):
    def original(self, status, output, collect_error=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stdout = types.SimpleNamespace(buffer=io.BytesIO())
            with patch.object(diagnostics.subprocess, "Popen", return_value=OriginalProcess(status, output)) as start, \
                 patch.object(diagnostics, "collect", side_effect=collect_error) as collect, \
                 patch.object(diagnostics.sys, "stdout", stdout):
                result = diagnostics.run_original(root, root, root / "provider", root / "renderer")
            self.assertEqual(output, (root / "progpu-native-webscene-provider.txt").read_bytes())
            self.assertEqual(output, stdout.buffer.getvalue())
            self.assertEqual(["ctest", "--test-dir", str(root), "-C", "Release", "--output-on-failure", "--verbose",
                              "-R", "^progpu_native_webscene_provider_tests$"], start.call_args.args[0])
            return result, collect.call_count

    def test_success_does_not_replay_or_sample_original_acceptance(self):
        self.assertEqual((0, 0), self.original(0, b"All original tests passed\n"))

    def test_non_timeout_failure_is_preserved_without_replay(self):
        self.assertEqual((8, 0), self.original(8, b"Pixel mismatch\n"))

    def test_timeout_has_one_diagnostic_and_retains_original_failure(self):
        self.assertEqual((8, 1), self.original(8, b"2 - progpu_native_webscene_provider_tests (Timeout)\n"))

    def test_diagnostic_error_cannot_replace_original_failure(self):
        self.assertEqual((8, 1), self.original(8, b"2 - progpu_native_webscene_provider_tests (Timeout)\n",
                                             RuntimeError("collector unavailable")))

    def test_missing_input_never_creates_or_executes_replay(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch.object(diagnostics.subprocess, "Popen") as start, self.assertRaises(FileNotFoundError):
                diagnostics.collect(root / "missing", root / "provider", root / "renderer", root / "out")
            start.assert_not_called()
            self.assertFalse((root / "out").exists())

    def test_preexisting_evidence_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            file = root / "input"; file.write_bytes(b"original")
            output = root / "out"; output.mkdir()
            with patch.object(diagnostics.subprocess, "Popen") as start, self.assertRaises(FileExistsError):
                diagnostics.collect(file, file, file, output)
            start.assert_not_called()
            self.assertEqual(b"original", file.read_bytes())

    def replay(self, code, seconds):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            script = root / "target.py"; script.write_text(code)
            # The test interpreter is a controlled target, not renderer evidence.
            # As a sampler it rejects the PID argument and exits immediately.
            with patch.object(diagnostics, "TARGET_SECONDS", seconds):
                return diagnostics.collect(Path(sys.executable), script, script, root / "out",
                                           sampler=Path(sys.executable))

    def test_completed_diagnostic_never_claims_qualification(self):
        receipt = self.replay("print('controlled child')", 5)
        self.assertEqual(0, receipt["targetExit"])
        self.assertFalse(receipt["qualified"])
        self.assertTrue(receipt["diagnosticOnly"])
        self.assertTrue(receipt["inputsUnchanged"])

    def test_expired_diagnostic_retires_its_owned_target(self):
        receipt = self.replay("import time; time.sleep(30)", 0.05)
        self.assertTrue(receipt["timedOut"])
        self.assertIsNotNone(receipt["targetExit"])
        self.assertNotEqual(0, receipt["targetExit"])
        self.assertFalse(receipt["qualified"])


if __name__ == "__main__":
    unittest.main()
