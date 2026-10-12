#!/usr/bin/env python3
"""Admit the immutable preview.67 source and the reviewed publication recipe."""

import json
from pathlib import Path
import re
import subprocess
import sys


REPO = "repos/wieslawsoltes/ProGPU"
SOURCE = "915c0bded45b283163bc85489111b0d74a4d9d44"
RELEASE = 38185156962
BUILD = 38185119471
PUBLISHER = 114617517419
TAG = "2956b6278ad28ae78118c739329a1ad73b1b9a2d"
ARTIFACT = 11720419571
ARTIFACT_SHA256 = "509753d4d6a5b2aafa4fe7555bcd5b126db04c160371dd4962bd18604b8daa5b"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def api(path):
    return json.loads(subprocess.check_output(["gh", "api", f"{REPO}/{path}"]))


def verify_build(run, jobs, commit):
    require(run["head_sha"] == commit and run["path"] == ".github/workflows/build.yml"
            and run["status"] == "completed" and run["conclusion"] == "success",
            "Whole Build source or conclusion differs")
    require(jobs["total_count"] == len(jobs["jobs"]) == 59
            and all(job["conclusion"] == "success" for job in jobs["jobs"]),
            "Every one of the 59 Build jobs must pass")


def verify_release(run, jobs):
    require(run["head_sha"] == SOURCE and run["path"] == ".github/workflows/release.yml"
            and run["status"] == "completed", "Original release source differs")
    require(jobs["total_count"] == len(jobs["jobs"]) == 20, "Original release job inventory differs")
    require(sum(job["id"] == PUBLISHER for job in jobs["jobs"]) == 1,
            "Original publisher identity differs")
    require(all(job["conclusion"] == "success" for job in jobs["jobs"] if job["id"] != PUBLISHER),
            "All 19 original release validation jobs must pass")


def main(recipe, receipt_path):
    require(re.fullmatch(r"[0-9a-f]{40}", recipe) is not None, "Expected an exact recipe commit")
    tag = api("git/ref/tags/v0.1.0-preview.67")
    require(tag["object"]["type"] == "tag" and tag["object"]["sha"] == TAG, "Immutable release tag moved")
    tag_object = api(f"git/tags/{TAG}")
    require(tag_object["object"]["type"] == "commit" and tag_object["object"]["sha"] == SOURCE,
            "Immutable product source moved")
    build, build_jobs = api(f"actions/runs/{BUILD}"), api(f"actions/runs/{BUILD}/jobs?per_page=100")
    verify_build(build, build_jobs, SOURCE)
    release, release_jobs = api(f"actions/runs/{RELEASE}"), api(f"actions/runs/{RELEASE}/jobs?per_page=100")
    verify_release(release, release_jobs)
    artifact = api(f"actions/artifacts/{ARTIFACT}")
    require(artifact["workflow_run"]["id"] == RELEASE and not artifact["expired"]
            and artifact["digest"] == f"sha256:{ARTIFACT_SHA256}"
            and artifact["name"] == "progpu-nuget-0.1.0-preview.67", "Original artifact identity differs")
    runs = api(f"actions/runs?head_sha={recipe}&per_page=100")["workflow_runs"]
    candidates = [run for run in runs if run["path"] == ".github/workflows/build.yml"
                  and run["status"] == "completed" and run["conclusion"] == "success"]
    require(candidates, "Publication recipe requires its own whole successful Build")
    recipe_run = candidates[0]
    recipe_jobs = api(f"actions/runs/{recipe_run['id']}/jobs?per_page=100")
    verify_build(recipe_run, recipe_jobs, recipe)
    receipt = {"source_commit": SOURCE, "recipe_commit": recipe, "tag": tag, "tag_object": tag_object,
               "original_build": build, "original_build_jobs": build_jobs,
               "original_release": release, "original_release_jobs": release_jobs,
               "original_artifact": artifact, "recipe_build": recipe_run, "recipe_build_jobs": recipe_jobs}
    Path(receipt_path).write_text(json.dumps(receipt, indent=2) + "\n")
    print("Qualified original 59-job Build, 19 release gates, immutable tag/artifact and 59-job recipe Build.")


if __name__ == "__main__":
    main(*sys.argv[1:])
