param([string]$DotNet = 'dotnet', [string]$RuntimePackages, [switch]$Online, [string]$OutputDirectory='artifacts/win-x64-0.3.3')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.work/cli'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$previousPackages = $env:NUGET_PACKAGES
if ($RuntimePackages) { $env:NUGET_PACKAGES = $RuntimePackages }
Push-Location $repoRoot
try {
    # Fresh staging prevents files from an older folder-based build leaking into the ZIP.
    $stage = Join-Path $repoRoot ('.work/single-file-' + [guid]::NewGuid().ToString('N'))
    $feedArgs = if ($Online) { @('--source', 'https://api.nuget.org/v3/index.json') } else { @('--configfile', 'NuGet.Config') }
    & $DotNet publish src/DyeFinder.App/DyeFinder.App.csproj -c Release -r win-x64 --self-contained true -o $stage @feedArgs -p:RuntimeFrameworkVersion=10.0.11 -p:TargetLatestRuntimePatch=false -p:NuGetAudit=false -p:DebugType=None -p:DebugSymbols=false -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed; offline runtime packages 10.0.11 are required.' }
    $published = @(Get-ChildItem -LiteralPath $stage -Force)
    if ($published.Count -ne 1 -or $published[0].Name -ne 'VindictusDyeFinder.exe') { throw 'Single-file publish produced unexpected loose files.' }
    $allowed = @('VindictusDyeFinder.exe', 'README.txt', 'LICENSE', 'ASSET-NOTICES.md')
    if (Test-Path -LiteralPath $OutputDirectory) {
        if (Get-ChildItem -LiteralPath $OutputDirectory -Force | Where-Object { $_.PSIsContainer -or $_.Name -notin $allowed }) {
            throw 'Output contains old or unrelated files. Choose a new output directory.'
        }
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Copy-Item -LiteralPath $published[0].FullName -Destination $OutputDirectory -Force
    Copy-Item -LiteralPath 'scripts/PORTABLE-README.txt' -Destination (Join-Path $OutputDirectory 'README.txt') -Force
    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $OutputDirectory 'LICENSE') -Force
    Copy-Item -LiteralPath 'src/DyeFinder.App/Assets/README.md' -Destination (Join-Path $OutputDirectory 'ASSET-NOTICES.md') -Force
    Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.pdb' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    Write-Output "Portable build: $OutputDirectory/VindictusDyeFinder.exe"
} finally { $env:NUGET_PACKAGES=$previousPackages; Pop-Location }
