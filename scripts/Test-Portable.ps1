param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$expected = @('ASSET-NOTICES.md','LICENSE','README.txt','VindictusDyeFinder.exe')
$items = @(Get-ChildItem -LiteralPath $Directory -Force)
if (($items | Where-Object PSIsContainer) -or (Compare-Object $expected @($items.Name))) { throw 'Portable folder must contain only the EXE and three supporting documents.' }
$trialRoot = Join-Path $repoRoot ('.work/portable-check-' + [guid]::NewGuid().ToString('N'))
$exeFolder = Join-Path $trialRoot 'app'
New-Item -ItemType Directory -Path $exeFolder -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $Directory 'VindictusDyeFinder.exe') -Destination $exeFolder
$previousExtraction = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
try {
    # An empty extraction cache and EXE-only folder prove no loose build files are required.
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $trialRoot 'runtime'
    $reportFolder = Join-Path $trialRoot 'smoke'
    $check = Start-Process -FilePath (Join-Path $exeFolder 'VindictusDyeFinder.exe') -ArgumentList @('--smoke-test', ('"' + $reportFolder + '"')) -WorkingDirectory $exeFolder -WindowStyle Hidden -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw "Single-file smoke failed ($($check.ExitCode)); inspect $reportFolder" }
    if (@(Get-ChildItem -LiteralPath $exeFolder -Force).Count -ne 1) { throw 'App created unexpected files next to its EXE.' }
    Get-Content -LiteralPath (Join-Path $reportFolder 'smoke-test.txt')
    Write-Output "PASS: four-file portable package; EXE-only startup with an empty runtime extraction cache. Reports: $reportFolder"
} finally { $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $previousExtraction }
