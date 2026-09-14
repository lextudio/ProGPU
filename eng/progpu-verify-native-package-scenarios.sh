#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
selector="${repo_root}/eng/progpu-native-package-scenarios.sh"

# This is the original serial JIT/NativeAOT scene list, independent of grouping.
expected="$(printf '%s\n' \
  --mil-drawing-group-only --mil-glyph-run-drawing-only \
  --mil-text-render-options-only --mil-visual-clip-only \
  --mil-visual-opacity-mask-only --mil-visual-effect-only \
  --mil-visual-guideline-only --mil-drawing-image-only --mil-guideline-only | sort)"
all="$("${selector}" all | sort)"
grouped="$(for group in drawings visuals guidelines; do "${selector}" "${group}"; done | sort)"
if [[ "${all}" != "${expected}" || "${grouped}" != "${expected}" || -n "$("${selector}" core)" ]]; then
  echo 'Native package scene groups must cover the original nine cases exactly once.' >&2
  exit 1
fi
for group in drawings visuals guidelines; do
  if [[ "$("${selector}" "${group}" | wc -l | tr -d ' ')" != 3 ]]; then
    echo "Native package scene group ${group} must contain three cases." >&2
    exit 1
  fi
done
if "${selector}" unknown >/dev/null 2>&1 || "${selector}" >/dev/null 2>&1 ||
   "${selector}" core unexpected >/dev/null 2>&1; then
  echo 'Native package scenario selection must reject invalid arguments.' >&2
  exit 1
fi
echo 'Native package groups preserve all nine JIT/NativeAOT scene cases exactly once.'
