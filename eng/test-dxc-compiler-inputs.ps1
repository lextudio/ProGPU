#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Tokens = $null
$Errors = $null
$Script = Join-Path $PSScriptRoot 'stage-dxc-compiler.ps1'
$Ast = [Management.Automation.Language.Parser]::ParseFile($Script, [ref]$Tokens, [ref]$Errors)
if ($Errors.Count) { throw ($Errors | Out-String) }
foreach ($Name in @('Assert-DxcHash', 'Assert-DxcMachine', 'Copy-DxcEntry')) {
    $Function = $Ast.Find({ param($Node) $Node -is [Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -eq $Name }, $true)
    if (-not $Function) { throw "Missing compiler staging helper: $Name" }
    . ([scriptblock]::Create($Function.Extent.Text))
}
function Require-Rejection([scriptblock] $Action, [string] $Pattern) {
    try { & $Action } catch {
        if ($_.Exception.Message -notlike $Pattern) { throw }
        return
    }
    throw "Expected rejection: $Pattern"
}
$Temporary = [IO.Directory]::CreateTempSubdirectory('progpu-dxc-inputs-').FullName
$Passed = 0
try {
    $Pe = Join-Path $Temporary 'compiler.dll'
    foreach ($Machine in @(0x8664, 0xAA64)) {
        $Bytes = [byte[]]::new(128)
        [BitConverter]::GetBytes([uint16]0x5A4D).CopyTo($Bytes, 0)
        [BitConverter]::GetBytes([uint32]64).CopyTo($Bytes, 0x3C)
        [BitConverter]::GetBytes([uint32]0x4550).CopyTo($Bytes, 64)
        [BitConverter]::GetBytes([uint16]$Machine).CopyTo($Bytes, 68)
        [IO.File]::WriteAllBytes($Pe, $Bytes)
        Assert-DxcMachine $Pe $Machine
        $Passed++
        Require-Rejection { Assert-DxcMachine $Pe 0x014C } '*architecture differs*'
        $Passed++
    }
    $Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Pe).Hash
    Assert-DxcHash $Pe $Hash
    $Passed++
    Require-Rejection { Assert-DxcHash $Pe ('0' * 64) } '*hash differs*'
    $Passed++
    $Bytes[64] = 0
    [IO.File]::WriteAllBytes($Pe, $Bytes)
    Require-Rejection { Assert-DxcMachine $Pe 0xAA64 } '*signature or architecture*'
    $Passed++
    [BitConverter]::GetBytes([uint32]126).CopyTo($Bytes, 0x3C)
    [IO.File]::WriteAllBytes($Pe, $Bytes)
    Require-Rejection { Assert-DxcMachine $Pe 0xAA64 } '*PE header is truncated*'
    $Passed++
    [IO.File]::WriteAllBytes($Pe, [byte[]]::new(4))
    Require-Rejection { Assert-DxcMachine $Pe 0xAA64 } '*complete DOS header*'
    $Passed++

    $ZipPath = Join-Path $Temporary 'fixture.zip'
    $Zip = [IO.Compression.ZipFile]::Open($ZipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($Name in @('valid', 'duplicate', 'duplicate')) {
            $Writer = [IO.StreamWriter]::new($Zip.CreateEntry($Name).Open())
            try { $Writer.Write('retained payload') } finally { $Writer.Dispose() }
        }
        $Zip.CreateEntry('empty') | Out-Null
    } finally { $Zip.Dispose() }
    $Zip = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $Output = Join-Path $Temporary 'output'
        Copy-DxcEntry $Zip 'valid' $Output
        if ([IO.File]::ReadAllText($Output) -cne 'retained payload') { throw 'Incorrect entry copy.' }
        $Passed++
        Require-Rejection { Copy-DxcEntry $Zip 'valid' $Output } '*already exists*'
        if ([IO.File]::ReadAllText($Output) -cne 'retained payload') { throw 'Existing destination modified.' }
        $Passed++
        foreach ($Name in @('missing', 'duplicate', 'empty', 'VALID')) {
            Require-Rejection { Copy-DxcEntry $Zip $Name (Join-Path $Temporary 'rejected') } '*invalid compiler package entry*'
            if (Test-Path -LiteralPath (Join-Path $Temporary 'rejected')) { throw 'Rejected entry was published.' }
            $Passed++
        }
    } finally { $Zip.Dispose() }

    Require-Rejection { & $Script -Rid win-arm64 -PackagePath $Pe -OutputDirectory $Temporary } '*must be a new directory*'
    $Passed++
    $RejectedOutput = Join-Path $Temporary 'bad-package'
    Require-Rejection { & $Script -Rid win-arm64 -PackagePath $Pe -OutputDirectory $RejectedOutput } '*hash differs*'
    if (Test-Path -LiteralPath $RejectedOutput) { throw 'Rejected package created output.' }
    $Passed++
    Write-Output "Passed $Passed compiler artifact input checks."
} finally { Remove-Item -LiteralPath $Temporary -Recurse -Force }
exit 0
