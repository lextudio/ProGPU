#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'win-arm64')][string] $Rid,
    [Parameter(Mandatory)][string] $PackagePath,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $DotnetExecutable = 'dotnet'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Pin = Get-Content -Raw (Join-Path $PSScriptRoot 'wgpu-dxc/libclang-package.json') | ConvertFrom-Json
$Runtime = $Pin.runtimes.$Rid
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Build-time libclang output must be a new directory.' }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $PackagePath).Hash -ine $Runtime.packageSha256) { throw 'Build-time libclang package hash differs from the reviewed pin.' }
& $DotnetExecutable nuget verify $PackagePath --all --certificate-fingerprint $Pin.authorCertificateSha256
if ($LASTEXITCODE -ne 0) { throw 'Build-time libclang signature verification failed.' }
$Parent = [IO.Path]::GetDirectoryName($OutputDirectory)
[IO.Directory]::CreateDirectory($Parent) | Out-Null
$Pending = Join-Path $Parent ('.libclang-staging-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($Pending) | Out-Null
try {
    $Archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        foreach ($EntryName in @("runtimes/$Rid/native/libclang.dll", 'LICENSE.TXT')) {
            $Entries = @($Archive.Entries | Where-Object FullName -CEQ $EntryName)
            if ($Entries.Count -ne 1 -or $Entries[0].Length -le 0 -or $Entries[0].Length -gt 128MB) { throw 'Invalid build-time libclang archive entry.' }
            $Source = $Entries[0].Open()
            try {
                $Destination = [IO.File]::Open((Join-Path $Pending ([IO.Path]::GetFileName($EntryName))), [IO.FileMode]::CreateNew)
                try { $Source.CopyTo($Destination) } finally { $Destination.Dispose() }
            } finally { $Source.Dispose() }
        }
    } finally { $Archive.Dispose() }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Pending 'libclang.dll')).Hash -ine $Runtime.librarySha256) { throw 'Build-time libclang DLL hash differs from the reviewed pin.' }
    [IO.Directory]::Move($Pending, $OutputDirectory)
} finally {
    if (Test-Path -LiteralPath $Pending) { Remove-Item -LiteralPath $Pending -Recurse -Force }
}
Write-Output "Staged pinned build-time libclang $($Pin.version) for $Rid. This is not a runtime package asset."
