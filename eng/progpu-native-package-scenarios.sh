#!/usr/bin/env bash
set -euo pipefail

# Independent consumer processes retain identical assertions in each group.
scenarios=(
  --mil-drawing-group-only
  --mil-glyph-run-drawing-only
  --mil-text-render-options-only
  --mil-visual-clip-only
  --mil-visual-opacity-mask-only
  --mil-visual-effect-only
  --mil-visual-guideline-only
  --mil-drawing-image-only
  --mil-guideline-only
)

if (( $# != 1 )); then
  echo "usage: $0 <all|core|drawings|visuals|guidelines>" >&2
  exit 2
fi
case "$1" in
  all) offset=0; count=9 ;;
  core) exit 0 ;;
  drawings) offset=0; count=3 ;;
  visuals) offset=3; count=3 ;;
  guidelines) offset=6; count=3 ;;
  *) echo "Unknown native package scenario group: $1" >&2; exit 2 ;;
esac
printf '%s\n' "${scenarios[@]:offset:count}"
