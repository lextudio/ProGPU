#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'win-arm64')][string] $Rid,
    [Parameter(Mandatory)][string] $PackagePath,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $DotnetExecutable = 'dotnet'
)

# Stages only the reviewed redistributable compiler, never WARP or a replacement
# WebGPU backend. No source or package-cache mutation and no implicit download.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-DxcHash([string] $Path, [string] $Expected) {
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash -ine $Expected) {
        throw "Compiler artifact hash differs from the reviewed pin: $Path"
    }
}

function Assert-DxcMachine([string] $Path, [int] $ExpectedMachine) {
    $Reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Path))
    try {
        if ($Reader.BaseStream.Length -lt 64 -or $Reader.ReadUInt16() -ne 0x5A4D) {
            throw 'Compiler artifact has no complete DOS header.'
        }
        $Reader.BaseStream.Position = 0x3C
        $Offset = $Reader.ReadUInt32()
        if ($Offset -gt $Reader.BaseStream.Length - 6) { throw 'Compiler artifact PE header is truncated.' }
        $Reader.BaseStream.Position = $Offset
        if ($Reader.ReadUInt32() -ne 0x4550 -or $Reader.ReadUInt16() -ne $ExpectedMachine) {
            throw 'Compiler artifact PE signature or architecture differs from the selected RID.'
        }
    } finally { $Reader.Dispose() }
}

function Copy-DxcEntry([IO.Compression.ZipArchive] $Archive, [string] $EntryName, [string] $Destination) {
    $Entries = @($Archive.Entries | Where-Object FullName -CEQ $EntryName)
    if ($Entries.Count -ne 1 -or $Entries[0].Length -le 0 -or $Entries[0].Length -gt 64MB) {
        throw "Missing, duplicate or invalid compiler package entry: $EntryName"
    }
    # Fixed allowlisted names, not archive-controlled destination paths.
    $InputStream = $Entries[0].Open()
    try {
        $OutputStream = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $InputStream.CopyTo($OutputStream) } finally { $OutputStream.Dispose() }
    } finally { $InputStream.Dispose() }
}

$Pin = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'wgpu-dxc/compiler-package.json') | ConvertFrom-Json
if ($Pin.schemaVersion -ne 1 -or $Pin.packageId -cne 'Microsoft.Direct3D.DXC') { throw 'Unsupported compiler package pin.' }
$Runtime = $Pin.runtimes.$Rid
$PackagePath = [IO.Path]::GetFullPath($PackagePath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Compiler output must be a new directory; existing artifacts are never overwritten.' }
Assert-DxcHash $PackagePath $Pin.sha256

# Verify the pinned author as well as NuGet's package integrity and timestamps.
# Trust-store/network failures remain failures; do not disable verification.
& $DotnetExecutable nuget verify $PackagePath --all --certificate-fingerprint $Pin.authorCertificateSha256
if ($LASTEXITCODE -ne 0) { throw "Compiler package signature verification failed: $LASTEXITCODE" }

$Parent = [IO.Path]::GetDirectoryName($OutputDirectory)
[IO.Directory]::CreateDirectory($Parent) | Out-Null
$Pending = Join-Path $Parent ('.dxc-staging-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($Pending) | Out-Null
try {
    $Archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        foreach ($Name in @('dxcompiler.dll', 'dxil.dll')) {
            $Destination = Join-Path $Pending $Name
            Copy-DxcEntry $Archive "$($Runtime.archiveDirectory)/$Name" $Destination
            Assert-DxcHash $Destination $Runtime.$Name
            Assert-DxcMachine $Destination $Runtime.machine
        }
        foreach ($Name in $Pin.notices) {
            if ($Name -cnotin @('LICENSE-LLVM.txt', 'LICENSE-MS.txt', 'LICENCE-MIT.txt')) { throw 'Unrecognized compiler notice.' }
            Copy-DxcEntry $Archive $Name (Join-Path $Pending $Name)
        }
        Copy-DxcEntry $Archive 'Microsoft.Direct3D.DXC.nuspec' (Join-Path $Pending 'Microsoft.Direct3D.DXC.nuspec')
    } finally { $Archive.Dispose() }

    $Manifest = [ordered]@{
        schemaVersion = 1
        packageId = $Pin.packageId
        packageVersion = $Pin.version
        packageUrl = $Pin.url
        packageSha256 = $Pin.sha256
        authorCertificateSha256 = $Pin.authorCertificateSha256
        rid = $Rid
        libraries = [ordered]@{ 'dxcompiler.dll' = $Runtime.'dxcompiler.dll'; 'dxil.dll' = $Runtime.'dxil.dll' }
        notices = $Pin.notices
        qualification = 'artifact-only'
    }
    $Manifest | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8NoBOM -LiteralPath (Join-Path $Pending 'dxc-compiler-package.json')
    # Atomic publication on the same volume; a competing/existing target fails.
    [IO.Directory]::Move($Pending, $OutputDirectory)
} finally {
    # Only this invocation's unique temporary directory is eligible for cleanup.
    if (Test-Path -LiteralPath $Pending) { Remove-Item -LiteralPath $Pending -Recurse -Force }
}
Write-Output "Staged verified DXC compiler artifact ($Rid): $OutputDirectory"
