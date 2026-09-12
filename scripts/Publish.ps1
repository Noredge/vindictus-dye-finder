param([string]$DotNet = 'dotnet', [Parameter(Mandatory=$true)][string]$RuntimePackages, [string]$OutputDirectory='artifacts/win-x64-0.3.2')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.work/cli'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$previousPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = $RuntimePackages
Push-Location $repoRoot
try {
    & $DotNet publish src/DyeFinder.App/DyeFinder.App.csproj -c Release -r win-x64 --self-contained true -o $OutputDirectory --configfile NuGet.Config -p:RuntimeFrameworkVersion=10.0.11 -p:TargetLatestRuntimePatch=false -p:NuGetAudit=false -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed; offline runtime packages 10.0.11 are required.' }
    Copy-Item -LiteralPath README.md -Destination (Join-Path $OutputDirectory 'README.md') -Force
    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $OutputDirectory 'LICENSE') -Force
    Copy-Item -LiteralPath 'src/DyeFinder.App/Assets/README.md' -Destination (Join-Path $OutputDirectory 'ASSET-NOTICES.md') -Force
    Copy-Item -LiteralPath TODO.md,HANDOFF.md -Destination $OutputDirectory -Force
    Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.pdb' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    Write-Output "Portable build: $OutputDirectory/VindictusDyeFinder.exe"
} finally { $env:NUGET_PACKAGES=$previousPackages; Pop-Location }
