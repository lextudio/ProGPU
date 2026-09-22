#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${repo_root}/eng/progpu-package-list.sh"

dotnet="${repo_root}/.dotnet/dotnet"
if [[ ! -x "${dotnet}" ]]; then
  dotnet="dotnet"
fi

configuration="${PROGPU_CONFIGURATION:-Release}"
package_version="${PROGPU_PACKAGE_VERSION:-0.1.0-preview.62}"
package_output="${PROGPU_PACKAGE_OUTPUT:-${repo_root}/artifacts/packages/${configuration}}"
package_group="${PROGPU_PACKAGE_GROUP:-all}"

case "${package_group}" in
  all)
    selected_package_ids=("${progpu_package_ids[@]}")
    selected_package_projects=("${progpu_package_projects[@]}")
    ;;
  portable)
    selected_package_ids=("${progpu_portable_package_ids[@]}")
    selected_package_projects=("${progpu_portable_package_projects[@]}")
    ;;
  cad)
    selected_package_ids=("${progpu_cad_package_ids[@]}")
    selected_package_projects=("${progpu_cad_package_projects[@]}")
    ;;
  avalonia-runtime)
    selected_package_ids=("${progpu_avalonia_runtime_package_ids[@]}")
    selected_package_projects=("${progpu_avalonia_runtime_package_projects[@]}")
    ;;
  drawing-runtime)
    selected_package_ids=("${progpu_drawing_runtime_package_ids[@]}")
    selected_package_projects=("${progpu_drawing_runtime_package_projects[@]}")
    ;;
  opendevelop-macos)
    selected_package_ids=("${progpu_opendevelop_macos_package_ids[@]}")
    selected_package_projects=("${progpu_opendevelop_macos_package_projects[@]}")
    ;;
  mobile)
    selected_package_ids=("${progpu_mobile_package_ids[@]}")
    selected_package_projects=("${progpu_mobile_package_projects[@]}")
    ;;
  *)
    echo "Unknown PROGPU_PACKAGE_GROUP '${package_group}'. Expected all, portable, cad, avalonia-runtime, drawing-runtime, opendevelop-macos, or mobile." >&2
    exit 1
    ;;
esac

"${repo_root}/eng/progpu-verify-package-list.sh"

mkdir -p "${package_output}"

echo "Packing ProGPU ${package_version} ${package_group} packages to ${package_output}..."
for index in "${!selected_package_ids[@]}"; do
  package_id="${selected_package_ids[$index]}"
  project="${selected_package_projects[$index]}"

  rm -f \
    "${package_output}/${package_id}.${package_version}.nupkg" \
    "${package_output}/${package_id}.${package_version}.snupkg"

  # Pin AnyCPU rather than relying on the default. Every package here carries its managed
  # assembly in a RID-NEUTRAL lib/<tfm> folder and ships no runtimes/<rid> tree at all, so an
  # architecture-stamped assembly has no correct copy to fall back to: it loads on exactly one
  # architecture and fails everywhere else with "Could not load file or assembly 'X' ... The
  # system cannot find the file specified" - naming a file that is sitting in the output folder,
  # because the CLR does not JIT around a wrong-architecture managed assembly.
  #
  # A clean `dotnet pack` already produces AnyCPU; this is about what happens when it is NOT
  # clean. Every project here also has bin/x64 and bin/ARM64 outputs from the LibreWPF graph
  # builds, and an ambient Platform/PlatformTarget - inherited from a parent build, an exported
  # variable, or a nested invocation - silently redirects pack to one of those. That is how
  # ProGPU.DirectX shipped arm64 and seven others shipped x64 in the same feed, while
  # ProGPU.Backend in the same run was correct. PlatformTarget is the one that actually stamps
  # the PE machine field; Platform is set too so the output path cannot drift either.
  pack_arguments=(
    --configuration "${configuration}" \
    --output "${package_output}" \
    --verbosity minimal \
    -p:Platform=AnyCPU \
    -p:PlatformTarget=AnyCPU \
    -p:ContinuousIntegrationBuild=true \
    -p:Version="${package_version}" \
    -p:PackageVersion="${package_version}"
  )
  if [[ "${package_id}" == "ProGPU.Xaml.SourceGenerator" ||
        "${package_id}" == "ProGPU.Backend.Dx12" ||
        "${package_id}" == "ProGPU.BinaryCompatibility" ]]; then
    pack_arguments+=(-p:IncludeSymbols=false)
  else
    pack_arguments+=(-p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg)
  fi
  if [[ "${package_id}" == "ACadSharp.ProGPU" ]]; then
    # ACadSharp enables GeneratePackageOnBuild in Release. A direct clean
    # dotnet pack must disable that build-time pack cycle so Pack builds the
    # net10.0 fork output before collecting ACadSharp.dll.
    pack_arguments+=(
      -p:ProGpuForkPackage=true
      -p:GeneratePackageOnBuild=false
    )
  fi

  "${dotnet}" pack "${repo_root}/${project}" "${pack_arguments[@]}"

done

PROGPU_PACKAGE_VERSION="${package_version}" \
PROGPU_PACKAGE_OUTPUT="${package_output}" \
PROGPU_PACKAGE_GROUP="${package_group}" \
  "${repo_root}/eng/progpu-verify-packages.sh"

if [[ "${package_group}" == "portable" || "${package_group}" == "all" ]]; then
  PROGPU_CONFIGURATION="${configuration}" \
  PROGPU_PACKAGE_VERSION="${package_version}" \
  PROGPU_PACKAGE_OUTPUT="${package_output}" \
    "${repo_root}/eng/progpu-verify-xaml-package-consumer.sh"
  PROGPU_CONFIGURATION="${configuration}" \
  PROGPU_PACKAGE_VERSION="${package_version}" \
  PROGPU_PACKAGE_OUTPUT="${package_output}" \
    "${repo_root}/eng/progpu-verify-drawing-extension-package-consumer.sh"
fi

if [[ "${package_group}" == "cad" || "${package_group}" == "portable" || "${package_group}" == "all" ]]; then
  PROGPU_CONFIGURATION="${configuration}" \
  PROGPU_PACKAGE_VERSION="${package_version}" \
  PROGPU_PACKAGE_OUTPUT="${package_output}" \
    "${repo_root}/eng/progpu-verify-cad-package-consumer.sh"
fi

echo "ProGPU ${package_group} NuGet package build succeeded for ${package_version}."
