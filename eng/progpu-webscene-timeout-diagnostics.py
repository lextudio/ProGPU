#!/usr/bin/env python3
"""Preserve the original CTest result; collect one separate bounded timeout replay."""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import subprocess
import sys
import time

TARGET_SECONDS = 60
SAMPLE_SECONDS = 55
SAMPLE_INTERVAL_MS = 10


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def stop_owned(process):
    if process is not None and process.poll() is None:
        process.kill()
        process.wait(timeout=5)


def collect(executable, provider, renderer, output, sampler=Path("/usr/bin/sample")):
    inputs = [path.resolve(strict=True) for path in (executable, provider, renderer)]
    if not all(path.is_file() for path in inputs):
        raise ValueError("The original executable, provider and renderer must be regular files.")
    output.mkdir()  # Never overwrite an earlier diagnostic attempt.
    receipt = {"diagnosticOnly": True, "qualified": False,
               "inputs": {str(path): sha256(path) for path in inputs},
               "targetSeconds": TARGET_SECONDS, "samplingSeconds": SAMPLE_SECONDS,
               "samplingIntervalMs": SAMPLE_INTERVAL_MS, "errors": []}
    target = sample = None
    started = time.monotonic()
    try:
        with (output / "target.log").open("xb") as target_log, (output / "sampler.log").open("xb") as sample_log:
            # Only the output directory changes. The executable, provider argument,
            # original fixture and GPU_COMPLETE presentation contract are unchanged.
            command = [str(inputs[0]), str(inputs[1]), str(output / "progpu-native-webscene-provider.ppm")]
            receipt["command"] = command
            target = subprocess.Popen(command, cwd=output, stdout=target_log, stderr=subprocess.STDOUT)
            receipt["pid"] = target.pid
            deadline = started + TARGET_SECONDS
            try:
                sample = subprocess.Popen(
                    [str(sampler), str(target.pid), str(SAMPLE_SECONDS), str(SAMPLE_INTERVAL_MS),
                     "-mayDie", "-file", str(output / "cpu.sample.txt")],
                    stdout=sample_log, stderr=subprocess.STDOUT)
                target.wait(timeout=max(0.001, deadline - time.monotonic()))
            except subprocess.TimeoutExpired:
                receipt["timedOut"] = True
            finally:
                stop_owned(target)
                if sample is not None:
                    try:
                        sample.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        receipt["errors"].append("Sampler did not retire after the owned target exited.")
                        stop_owned(sample)
    except Exception as error:
        receipt["errors"].append(str(error))
    finally:
        stop_owned(target)
        stop_owned(sample)
        receipt.update(targetExit=target.poll() if target else None,
                       sampleExit=sample.poll() if sample else None,
                       elapsed=time.monotonic() - started)
        receipt["inputsUnchanged"] = all(sha256(path) == receipt["inputs"][str(path)] for path in inputs)
        (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    return receipt


def run_original(build, sample_dir, provider, renderer):
    evidence = sample_dir / "progpu-native-webscene-provider.txt"
    command = ["ctest", "--test-dir", str(build), "-C", "Release", "--output-on-failure", "--verbose",
               "-R", "^progpu_native_webscene_provider_tests$"]
    with evidence.open("wb") as log:
        with subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT) as original:
            for line in original.stdout:
                log.write(line)
                sys.stdout.buffer.write(line)
                sys.stdout.buffer.flush()
            original_status = original.wait()
    # A replay is diagnostic only. Its success or failure cannot repair the
    # original CTest status or allow packaging/managed consumer execution.
    if original_status and b"progpu_native_webscene_provider_tests (Timeout)" in evidence.read_bytes():
        try:
            collect(build / "progpu_native_webscene_provider_tests", provider, renderer,
                    sample_dir / "webscene-timeout-diagnostics")
        except Exception as error:
            print("WebScene diagnostic collection failed: " + str(error), file=sys.stderr)
    return original_status


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument("--sample-dir", type=Path, required=True)
    parser.add_argument("--provider", type=Path, required=True)
    args = parser.parse_args()
    if platform.system() != "Darwin" or platform.machine() != "arm64":
        parser.error("The original WebScene provider gate requires Darwin/arm64.")
    build = args.build_dir.resolve(strict=True)
    sample_dir = args.sample_dir.resolve(strict=True)
    provider = args.provider.resolve(strict=True)
    return run_original(build, sample_dir, provider, build / "libprogpu_native_dawn.dylib")


if __name__ == "__main__":
    sys.exit(main())
