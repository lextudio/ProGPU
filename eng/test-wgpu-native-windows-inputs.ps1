#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Tokens = $null
$Errors = $null
$Ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'build-wgpu-native-windows.ps1'), [ref]$Tokens, [ref]$Errors)
if ($Errors.Count) { throw ($Errors | Out-String) }
# Exercise the actual helpers without executing the platform-specific build.
foreach ($Name in @('Invoke-Checked', 'Set-ReviewedInput')) {
    $Function = $Ast.Find({ param($Node) $Node -is [Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -eq $Name }, $true)
    if (-not $Function) { throw "Missing build helper $Name" }
    . ([scriptblock]::Create($Function.Extent.Text))
}
$File = [IO.Path]::GetTempFileName()
$Passed = 0
try {
    $Original = "# Unicode dependency metadata: $([char]0x26A0)`n[package]`nname = 'test'"
    $Expected = "$Original`n# Original ProGPU build metadata"
    [IO.File]::WriteAllText($File, $Original.Replace("`n", "`r`n") + "`r`n")
    Set-ReviewedInput $File $Original $Expected
    if ([IO.File]::ReadAllText($File) -cne "$Expected`n") { throw 'Incorrect normalized metadata.' }
    $Passed++
    Set-ReviewedInput $File $Original $Expected
    if ([IO.File]::ReadAllText($File) -cne "$Expected`n") { throw 'Retry changed metadata.' }
    $Passed++
    [IO.File]::WriteAllText($File, '# caller changes')
    $Rejected = $false
    try { Set-ReviewedInput $File $Original $Expected } catch {
        if ($_.Exception.Message -notlike 'Refusing to replace modified*') { throw }
        $Rejected = $true
    }
    if (-not $Rejected -or [IO.File]::ReadAllText($File) -cne '# caller changes') { throw 'Modified input was not preserved.' }
    $Passed++
    $PowerShell = (Get-Process -Id $PID).Path
    $PreviousEncoding = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.Encoding]::GetEncoding(437)
        $Result = (Invoke-Checked $PowerShell @('-NoProfile', '-Command', '[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); [Console]::WriteLine([char]0x26A0)') | Out-String).Trim()
        if ($Result -cne [string][char]0x26A0 -or [Console]::OutputEncoding.CodePage -ne 437) { throw 'Native UTF-8 capture or encoding restoration failed.' }
        $Passed++
        $Rejected = $false
        try { Invoke-Checked $PowerShell @('-NoProfile', '-Command', 'exit 7') } catch {
            if ($_.Exception.Message -notlike '*failed with exit code 7.*') { throw }
            $Rejected = $true
        }
        if (-not $Rejected -or [Console]::OutputEncoding.CodePage -ne 437) { throw 'Native failure or encoding restoration was lost.' }
        $Passed++
    } finally { [Console]::OutputEncoding = $PreviousEncoding }
    $Inputs = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'wgpu-dxc/build-inputs.json') | ConvertFrom-Json
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'wgpu-dxc/Cargo.lock')).Hash -ne $Inputs.lockSha256) { throw 'Dependency lock drift.' }
    $Passed++
    Write-Output "Passed $Passed native dependency input checks."
} finally { Remove-Item -LiteralPath $File }
exit 0
