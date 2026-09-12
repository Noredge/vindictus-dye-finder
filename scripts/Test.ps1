param([string]$DotNet = 'dotnet', [string]$Samples)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Build.ps1') -DotNet $DotNet
Push-Location $repoRoot
try {
    $testArgs = @('tests/DyeFinder.Tests/bin/Release/net10.0-windows/DyeFinder.Tests.dll')
    if ($Samples) { $testArgs += $Samples }
    & $DotNet @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    $smokeArgs = @('src/DyeFinder.App/bin/Release/net10.0-windows/VindictusDyeFinder.dll', '--smoke-test', '.work/smoke')
    if ($Samples) { $smokeArgs += (Join-Path $Samples '2026_09_12_0023.png') }
    & $DotNet @smokeArgs
    if ($LASTEXITCODE -ne 0) { throw 'WPF smoke test failed' }
    Get-Content .work/smoke/smoke-test.txt
} finally { Pop-Location }
