#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${repo_root}/eng/progpu-package-list.sh"

package_version="${PROGPU_PACKAGE_VERSION:-0.1.0-preview.62}"
package_output="${PROGPU_PACKAGE_OUTPUT:-${repo_root}/artifacts/packages/Release}"
package_group="${PROGPU_PACKAGE_GROUP:-all}"

case "${package_group}" in
  all)
    selected_package_ids=("${progpu_package_ids[@]}")
    ;;
  portable)
    selected_package_ids=("${progpu_portable_package_ids[@]}")
    ;;
  cad)
    selected_package_ids=("${progpu_cad_package_ids[@]}")
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
  mobile)
    selected_package_ids=("${progpu_mobile_package_ids[@]}")
    ;;
  *)
    echo "Unknown PROGPU_PACKAGE_GROUP '${package_group}'. Expected all, portable, cad, avalonia-runtime, drawing-runtime, opendevelop-macos, or mobile." >&2
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
    project_id="$(sed -nE 's/.*<PackageId>([^<]+)<\/PackageId>.*/\1/p' "${repo_root}/${project}" | head -n 1)"
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
  if [[ "${package_id}" == "ProGPU.Xaml.SourceGenerator" ]]; then
    if [[ -f "${symbols}" ]]; then
      echo "Analyzer-only package must not produce an empty symbol package: ${symbols}" >&2
      exit 1
    fi
    for analyzer_pdb in \
      ProGPU.Xaml.SourceGenerator.pdb \
      ProGPU.Xaml.pdb \
      ProGPU.Xaml.Roslyn.pdb; do
      if ! unzip -Z1 "${package}" | grep -Fx "analyzers/dotnet/cs/${analyzer_pdb}" >/dev/null; then
        echo "${package_id} is missing analyzer symbols ${analyzer_pdb}." >&2
        exit 1
      fi
    done
  elif [[ "${package_id}" == "ProGPU.BinaryCompatibility" ]]; then
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
    native_entries=(
      runtimes/win-x64/native/progpu_native.dll \
      runtimes/win-x64/native/progpu_native_direct2d.dll \
      runtimes/win-arm64/native/progpu_native.dll \
      runtimes/win-arm64/native/progpu_native_direct2d.dll \
      build/native/include/progpu_native.h)
    if [[ "${package_group}" == "opendevelop-macos" ]]; then
      # Matches progpu-pack.sh: the macOS release lane intentionally omits Windows-only Direct2D.
      native_entries=(
        runtimes/win-x64/native/progpu_native.dll \
        runtimes/win-arm64/native/progpu_native.dll \
        build/native/include/progpu_native.h)
    fi
    if [[ "${PROGPU_PACKAGE_WINDOWS_ONLY:-0}" != "1" ]]; then
      native_entries=(
        runtimes/linux-x64/native/libprogpu_native.so
        runtimes/linux-arm64/native/libprogpu_native.so
        runtimes/osx-x64/native/libprogpu_native.dylib
        runtimes/osx-arm64/native/libprogpu_native.dylib
        "${native_entries[@]}")
    fi
    for native_entry in "${native_entries[@]}"; do
      if ! unzip -Z1 "${package}" | grep -Fx "${native_entry}" >/dev/null; then
        echo "${package_id} is missing ${native_entry}." >&2
        exit 1
      fi
    done
  fi

  if [[ "${package_id}" == "ACadSharp.ProGPU" ]]; then
    if ! unzip -Z1 "${package}" | grep -Fx "lib/net10.0/ACadSharp.dll" >/dev/null; then
      echo "${package_id} is missing its net10.0 ACadSharp assembly." >&2
      exit 1
    fi
  elif [[ "${package_id}" == "ProGPU.CAD" ]]; then
    cad_nuspec="$(unzip -p "${package}" '*.nuspec')"
    if ! grep -F '<dependency id="ACadSharp.ProGPU" version="' <<<"${cad_nuspec}" >/dev/null; then
      echo "${package_id} must depend on the reviewed ACadSharp.ProGPU fork package." >&2
      exit 1
    fi
    if grep -F '<dependency id="ACadSharp" ' <<<"${cad_nuspec}" >/dev/null; then
      echo "${package_id} must not resolve the upstream ACadSharp package identity." >&2
      exit 1
    fi
  fi

  while IFS=$'\t' read -r dependency_id dependency_version; do
    [[ -z "${dependency_id}" ]] && continue
    if is_shipping_package_id "${dependency_id}"; then
      if [[ "${dependency_version}" != "${package_version}" ]]; then
        echo "${package_id} depends on ${dependency_id} ${dependency_version}, expected ${package_version}." >&2
        exit 1
      fi
      if [[ "${package_group}" == "avalonia-runtime" || "${package_group}" == "cad" || "${package_group}" == "drawing-runtime" || "${package_group}" == "opendevelop-macos" ]] && ! is_selected_package_id "${dependency_id}"; then
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
