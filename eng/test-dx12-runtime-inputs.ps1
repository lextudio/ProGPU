#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Tokens = $null
$Errors = $null
$Ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'stage-dx12-runtime.ps1'), [ref]$Tokens, [ref]$Errors)
if ($Errors.Count) { throw ($Errors | Out-String) }
$Function = $Ast.Find({ param($Node) $Node -is [Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -eq 'Assert-Dx12RuntimeFile' }, $true)
if (-not $Function) { throw 'Missing runtime validation helper.' }
. ([scriptblock]::Create($Function.Extent.Text))
$File = [IO.Path]::GetTempFileName()
$Passed = 0
function Expect-Rejection([scriptblock] $Action, [string] $Message) {
    try { & $Action } catch { if ($_.Exception.Message -notlike $Message) { throw }; return }
    throw 'Invalid runtime input was accepted.'
}
try {
    foreach ($Machine in @(0xAA64, 0x8664)) {
        $Bytes = [byte[]]::new(128)
        [BitConverter]::GetBytes([uint16]0x5A4D).CopyTo($Bytes, 0)
        [BitConverter]::GetBytes([uint32]64).CopyTo($Bytes, 60)
        [BitConverter]::GetBytes([uint32]0x4550).CopyTo($Bytes, 64)
        [BitConverter]::GetBytes([uint16]$Machine).CopyTo($Bytes, 68)
        [IO.File]::WriteAllBytes($File, $Bytes)
        $Hash = (Get-FileHash $File).Hash
        Assert-Dx12RuntimeFile $File $Hash $Machine
        $Passed++
        Expect-Rejection { Assert-Dx12RuntimeFile $File ('0' * 64) $Machine } '*hash mismatch*'
        $Passed++
        Expect-Rejection { Assert-Dx12RuntimeFile $File $Hash 0x014C } '*architecture differs*'
        $Passed++
        [BitConverter]::GetBytes([uint32]::MaxValue).CopyTo($Bytes, 60)
        [IO.File]::WriteAllBytes($File, $Bytes)
        Expect-Rejection { Assert-Dx12RuntimeFile $File (Get-FileHash $File).Hash $Machine } '*header is truncated*'
        $Passed++
    }
    [IO.File]::WriteAllBytes($File, [byte[]]::new(2))
    Expect-Rejection { Assert-Dx12RuntimeFile $File (Get-FileHash $File).Hash 0xAA64 } '*DOS header is invalid*'
    $Passed++
    Write-Output "Passed $Passed DX12 runtime input checks."
} finally { Remove-Item -LiteralPath $File }
