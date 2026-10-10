param([ValidateRange(1024,65535)][int]$Port = 4173, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$taskUrl = "http://127.0.0.1:$Port"
$taskServerPath = Join-Path $PSScriptRoot 'scripts\serve.mjs'
$taskNode = Get-Command node.exe -ErrorAction SilentlyContinue
if (-not $taskNode) { throw 'Node.js is required for this local preview. Install Node.js 24, or use a hosted static build.' }
$taskAlreadyRunning = $false
try { $taskStatus = Invoke-RestMethod "$taskUrl/_preview/status" -TimeoutSec 2; $taskAlreadyRunning = $taskStatus.app -eq 'vindictus-dye-finder-web-preview' } catch {}
if (-not $taskAlreadyRunning) {
    $taskStateDirectory = Join-Path $PSScriptRoot '.preview-state'
    New-Item -ItemType Directory -Path $taskStateDirectory -Force | Out-Null
    $taskServerArgs = @('"' + $taskServerPath + '"', [string]$Port)
    $taskProcess = Start-Process -FilePath $taskNode.Source -ArgumentList $taskServerArgs -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskStateDirectory 'server.log') -RedirectStandardError (Join-Path $taskStateDirectory 'server-error.log')
    $taskReady = $false
    for ($taskAttempt = 0; $taskAttempt -lt 25; $taskAttempt++) {
        try { $taskStatus = Invoke-RestMethod "$taskUrl/_preview/status" -TimeoutSec 1; if ($taskStatus.app -eq 'vindictus-dye-finder-web-preview') { $taskReady = $true; break } } catch {}
        Start-Sleep -Milliseconds 200
    }
    if (-not $taskReady) { throw "Preview could not start on port $Port. Check .preview-state\server-error.log or choose another port." }
}
if (-not $NoBrowser) { Start-Process "$taskUrl/" }
