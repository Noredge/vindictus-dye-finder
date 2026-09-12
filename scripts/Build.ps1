param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.work/cli'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $repoRoot
try {
    & $DotNet build tests/DyeFinder.Tests/DyeFinder.Tests.csproj -c Release --configfile NuGet.Config -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
} finally { Pop-Location }
