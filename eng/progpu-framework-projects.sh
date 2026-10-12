#!/usr/bin/env bash

_progpu_framework_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Source roots are explicit build inputs. Default CI checkouts are prepared from
# full revisions in framework-sources.json; no recursive submodules are added.
progpu_resolve_project() {
  case "$1" in
    external/LibreWPF/*)
      printf '%s/%s\n' "${LibreWpfSourceRoot:-${_progpu_framework_root}/external/LibreWPF}" "${1#external/LibreWPF/}"
      ;;
    external/LibreWinForms/*)
      printf '%s/%s\n' "${LibreWinFormsSourceRoot:-${_progpu_framework_root}/external/LibreWinForms}" "${1#external/LibreWinForms/}"
      ;;
    *) printf '%s/%s\n' "${_progpu_framework_root}" "$1" ;;
  esac
}
