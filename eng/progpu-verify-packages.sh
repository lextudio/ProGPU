#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${repo_root}/eng/progpu-package-list.sh"

package_version="${PROGPU_PACKAGE_VERSION:-0.1.0-preview.69}"
package_output="${PROGPU_PACKAGE_OUTPUT:-${repo_root}/artifacts/packages/Release}"
package_group="${PROGPU_PACKAGE_GROUP:-all}"

case "${package_group}" in
  all)
    selected_package_ids=("${progpu_package_ids[@]}")
    ;;
  portable)
    selected_package_ids=("${progpu_portable_package_ids[@]}")
    ;;
  avalonia-runtime)
    selected_package_ids=("${progpu_avalonia_runtime_package_ids[@]}")
    ;;
  drawing-runtime)
    selected_package_ids=("${progpu_drawing_runtime_package_ids[@]}")
    ;;
  opendevelop-macos)
    selected_package_ids=("${progpu_opendevelop_macos_package_ids[@]}")
    ;;
  opendevelop-crossdesktop)
    selected_package_ids=("${progpu_opendevelop_crossdesktop_package_ids[@]}")
    ;;
  mobile)
    selected_package_ids=("${progpu_mobile_package_ids[@]}")
    ;;
  *)
    echo "Unknown PROGPU_PACKAGE_GROUP '${package_group}'. Expected all, portable, avalonia-runtime, drawing-runtime, opendevelop-macos, opendevelop-crossdesktop, or mobile." >&2
    exit 1
    ;;
esac

is_selected_artifact() {
  local file_name="$1"
  local package_id
  for package_id in "${selected_package_ids[@]}"; do
    if [[ "${file_name}" == "${package_id}.${package_version}.nupkg" ||
          "${file_name}" == "${package_id}.${package_version}.snupkg" ]]; then
      return 0
    fi
  done
  return 1
}

is_selected_package_id() {
  local candidate="$1"
  local package_id
  for package_id in "${selected_package_ids[@]}"; do
    if [[ "${candidate}" == "${package_id}" ]]; then
      return 0
    fi
  done
  return 1
}

is_shipping_package_id() {
  local candidate="$1"
  local package_id
  for package_id in "${progpu_package_ids[@]}"; do
    if [[ "${candidate}" == "${package_id}" ]]; then
      return 0
    fi
  done
  return 1
}

is_owned_nonshipping_project_id() {
  local candidate="$1"
  local project
  local project_id
  for project in "${progpu_nonshipping_projects[@]}"; do
    project_id="$(sed -nE 's/.*<PackageId>([^<]+)<\/PackageId>.*/\1/p' "$(progpu_resolve_project "${project}")" | head -n 1)"
    if [[ -z "${project_id}" ]]; then
      project_id="$(basename "${project}" .csproj)"
    fi
    if [[ "${candidate}" == "${project_id}" ]]; then
      return 0
    fi
  done
  return 1
}

for package_id in "${selected_package_ids[@]}"; do
  package="${package_output}/${package_id}.${package_version}.nupkg"
  symbols="${package_output}/${package_id}.${package_version}.snupkg"
  if [[ ! -f "${package}" ]]; then
    echo "Expected package was not produced: ${package}" >&2
    exit 1
  fi
  if [[ "${package_id}" == "ProGPU.BinaryCompatibility" ]]; then
    if [[ -f "${symbols}" ]]; then
      echo "Asset-only package must not produce a symbol package: ${symbols}" >&2
      exit 1
    fi
    for compatibility_entry in \
      build/ProGPU.BinaryCompatibility.targets \
      buildTransitive/ProGPU.BinaryCompatibility.targets \
      tools/net10.0/SkiaSharp.dll \
      tools/net10.0/Avalonia.Skia.dll; do
      if ! unzip -Z1 "${package}" | grep -Fx "${compatibility_entry}" >/dev/null; then
        echo "${package_id} is missing ${compatibility_entry}." >&2
        exit 1
      fi
    done
  elif [[ "${package_id}" == "ProGPU.Native.Sdk" ]]; then
    if [[ -f "${symbols}" ]]; then
      echo "Static-SDK-assets-only package must not produce an empty symbol package: ${symbols}" >&2
      exit 1
    fi
    if [[ "${package_group}" == "opendevelop-macos" || "${package_group}" == "opendevelop-crossdesktop" ]]; then
      # This final assembly is built on macOS and contains the downloaded native
      # SDK payloads. The cross-desktop closure adds Linux; neither lane ships
      # the unsupported osx-x64 slice.
      package_entries="$(unzip -Z1 "${package}")"
      if [[ "${package_group}" == "opendevelop-crossdesktop" ]]; then
        sdk_rids=(win-x64 win-arm64 linux-x64 linux-arm64 osx-arm64)
        excluded_rids=(osx-x64)
      else
        sdk_rids=(win-x64 win-arm64 osx-arm64)
        excluded_rids=(linux-x64 linux-arm64 osx-x64)
      fi
      for sdk_rid in "${sdk_rids[@]}"; do
        if ! grep -E "^runtimes/${sdk_rid}/native/sdk/.+" <<<"${package_entries}" >/dev/null; then
          echo "${package_id} is missing SDK payloads for ${sdk_rid}." >&2
          exit 1
        fi
      done
      for excluded_rid in "${excluded_rids[@]}"; do
        if grep -E "^runtimes/${excluded_rid}/" <<<"${package_entries}" >/dev/null; then
          echo "${package_id} unexpectedly contains ${excluded_rid} payloads." >&2
          exit 1
        fi
      done
      (
        # macOS' default mktemp directory is /var/folders, which resolves to
        # /private/var/folders. The dependency verifier intentionally rejects
        # that non-canonical path, so create the transient extraction tree
        # under the canonical repository artifacts directory instead.
        mkdir -p "${repo_root}/artifacts"
        package_root="$(mktemp -d "${repo_root}/artifacts/progpu-package-verify.XXXXXX")"
        trap 'rm -rf "${package_root}"' EXIT
        unzip -q "${package}" -d "${package_root}"
        python3 "${repo_root}/eng/progpu-verify-freetype.py" --package-root "${package_root}" --require --rids "${sdk_rids[@]}"
        python3 "${repo_root}/eng/progpu-edit-word-icu-dependency.py" verify-package --package-root "${package_root}" --require --rids "${sdk_rids[@]}"
      )
    fi
  elif [[ "${package_id}" == "ProGPU.Backend.Dx12" ]]; then
    if [[ -f "${symbols}" ]]; then
      echo "Native-assets-only package must not produce an empty symbol package: ${symbols}" >&2
      exit 1
    fi
    "${repo_root}/eng/progpu-verify-dx12-package.sh" "${package}"
  elif [[ ! -f "${symbols}" ]]; then
    echo "Expected symbol package was not produced: ${symbols}" >&2
    exit 1
  fi

  if [[ "${package_id}" == "ProGPU.Backend.Native" ]]; then
    # Each lane claims exactly the native slices its consumers ship:
    #   PROGPU_PACKAGE_WINDOWS_ONLY=1  win-x64 + win-arm64, including Direct2D
    #   opendevelop-macos              win-x64 + win-arm64 + osx-arm64
    #   opendevelop-crossdesktop       win-x64 + win-arm64 + linux-x64 + linux-arm64 + osx-arm64
    #   otherwise                      every desktop RID
    windows_entries=(
      runtimes/win-x64/native/progpu_native.dll
      runtimes/win-x64/native/progpu_native_dawn.dll
      runtimes/win-x64/native/progpu_native_direct2d.dll
      runtimes/win-arm64/native/progpu_native.dll
      runtimes/win-arm64/native/progpu_native_dawn.dll
      runtimes/win-arm64/native/progpu_native_direct2d.dll)
    if [[ -n "${ProGpuNativePackageRids:-}" ]]; then
      # An explicit closure (the same list the MSBuild package validation uses)
      # names its RIDs; this lets e.g. drawing-runtime ship without Linux.
      native_entries=()
      for native_rid in ${ProGpuNativePackageRids}; do
        case "${native_rid}" in
          win-*) native_entries+=(runtimes/${native_rid}/native/progpu_native.dll
                   runtimes/${native_rid}/native/progpu_native_dawn.dll
                   runtimes/${native_rid}/native/progpu_native_direct2d.dll) ;;
          osx-*) native_entries+=(runtimes/${native_rid}/native/libprogpu_native.dylib
                   runtimes/${native_rid}/native/libprogpu_native_dawn.dylib) ;;
          linux-*) native_entries+=(runtimes/${native_rid}/native/libprogpu_native.so
                     runtimes/${native_rid}/native/libprogpu_native_dawn.so) ;;
          *) echo "Unknown RID '${native_rid}' in ProGpuNativePackageRids." >&2; exit 1 ;;
        esac
      done
    elif [[ "${PROGPU_PACKAGE_WINDOWS_ONLY:-0}" == "1" ]]; then
      native_entries=("${windows_entries[@]}")
    elif [[ "${package_group}" == "opendevelop-macos" ]]; then
      native_entries=(
        "${windows_entries[@]}"
        runtimes/osx-arm64/native/libprogpu_native.dylib
        runtimes/osx-arm64/native/libprogpu_native_dawn.dylib)
    else
      native_entries=(
        runtimes/linux-x64/native/libprogpu_native.so
        runtimes/linux-arm64/native/libprogpu_native.so
        runtimes/osx-x64/native/libprogpu_native.dylib
        runtimes/osx-arm64/native/libprogpu_native.dylib
        "${windows_entries[@]}")
    fi
    native_entries+=(build/native/include/progpu_native.h)
    for native_entry in "${native_entries[@]}"; do
      if ! unzip -Z1 "${package}" | grep -Fx "${native_entry}" >/dev/null; then
        echo "${package_id} is missing ${native_entry}." >&2
        exit 1
      fi
    done
    if [[ "${package_group}" == "opendevelop-macos" || "${package_group}" == "opendevelop-crossdesktop" ]]; then
      package_entries="$(unzip -Z1 "${package}")"
      if [[ "${package_group}" == "opendevelop-crossdesktop" ]]; then
        excluded_rids=(osx-x64)
      else
        excluded_rids=(linux-x64 linux-arm64 osx-x64)
      fi
      for excluded_rid in "${excluded_rids[@]}"; do
        if grep -E "^runtimes/${excluded_rid}/" <<<"${package_entries}" >/dev/null; then
          echo "${package_id} unexpectedly contains ${excluded_rid} payloads." >&2
          exit 1
        fi
      done
    fi
  fi

  while IFS=$'\t' read -r dependency_id dependency_version; do
    [[ -z "${dependency_id}" ]] && continue
    if is_shipping_package_id "${dependency_id}"; then
      if [[ "${dependency_version}" != "${package_version}" ]]; then
        echo "${package_id} depends on ${dependency_id} ${dependency_version}, expected ${package_version}." >&2
        exit 1
      fi
      if [[ "${package_group}" == "avalonia-runtime" || "${package_group}" == "drawing-runtime" || "${package_group}" == "opendevelop-macos" || "${package_group}" == "opendevelop-crossdesktop" ]] && ! is_selected_package_id "${dependency_id}"; then
        echo "${package_id} depends on ${dependency_id}, which is missing from the isolated ${package_group} package closure." >&2
        exit 1
      fi
    elif [[ "${dependency_id}" == ProGPU.* || "${dependency_id}" == LibreWPF.* ]] || is_owned_nonshipping_project_id "${dependency_id}"; then
      echo "${package_id} depends on unpublished internal package ${dependency_id}." >&2
      exit 1
    fi
  done < <(unzip -p "${package}" '*.nuspec' | sed -nE 's/.*<dependency id="([^"]+)" version="([^"]+)".*/\1\t\2/p')
done

while IFS= read -r -d '' artifact; do
  file_name="$(basename "${artifact}")"
  if ! is_selected_artifact "${file_name}"; then
    echo "Unexpected ${package_version} package artifact in ${package_group} output: ${artifact}" >&2
    exit 1
  fi
done < <(find "${package_output}" -maxdepth 1 -type f \( -name "*.${package_version}.nupkg" -o -name "*.${package_version}.snupkg" \) -print0)

# Every managed assembly in lib/<tfm> must be AnyCPU. These packages ship no runtimes/<rid> tree,
# so an architecture-stamped assembly has no correct copy to fall back to: it fails to load on
# every other architecture, reported as "Could not load file or assembly 'X' ... The system cannot
# find the file specified" for a file that is present, because the CLR does not JIT around a
# wrong-architecture managed assembly. Pack pins AnyCPU, but an ambient Platform inherited from a
# parent build has defeated that before - ProGPU.DirectX shipped arm64 and seven others x64 in one
# feed - and the symptom only surfaces in a consumer on the other architecture, long afterwards.
# Fail here, where the cause is still visible.
arch_scratch="$(mktemp -d)"
trap 'rm -rf "${arch_scratch}"' EXIT
arch_offenders=0
for index in "${!selected_package_ids[@]}"; do
  package_id="${selected_package_ids[$index]}"
  package="${package_output}/${package_id}.${package_version}.nupkg"
  [[ -f "${package}" ]] || continue
  while IFS= read -r entry; do
    [[ -n "${entry}" ]] || continue
    probe="${arch_scratch}/probe.dll"
    unzip -p "${package}" "${entry}" > "${probe}" 2>/dev/null || continue
    [[ -s "${probe}" ]] || continue
    pe_offset="$(od -An -tu4 -j 60 -N 4 "${probe}" 2>/dev/null | tr -d ' ')"
    [[ "${pe_offset}" =~ ^[0-9]+$ ]] || continue
    machine="$(od -An -tx2 -j $((pe_offset + 4)) -N 2 "${probe}" 2>/dev/null | tr -d ' ')"
    # 0x014c is both AnyCPU and x86, and is the only acceptable value in a RID-neutral folder.
    if [[ -n "${machine}" && "${machine}" != "014c" ]]; then
      echo "${package_id}: ${entry} is architecture-stamped (machine 0x${machine}); lib/ must be AnyCPU." >&2
      arch_offenders=$((arch_offenders + 1))
    fi
  done < <(unzip -Z1 "${package}" 2>/dev/null | grep -E '^lib/[^/]+/.*\.dll$' || true)
done
if [[ "${arch_offenders}" -gt 0 ]]; then
  echo "${arch_offenders} RID-neutral assemblies are architecture-stamped. Pack from a clean" >&2
  echo "environment with no inherited Platform/PlatformTarget, or remove the projects' bin/x64" >&2
  echo "and bin/ARM64 outputs first." >&2
  exit 1
fi

echo "Verified ${#selected_package_ids[@]} ProGPU ${package_group} packages and symbol packages for ${package_version}."

python3 "${repo_root}/eng/progpu-verify-package-sizes.py" "${package_output}"
