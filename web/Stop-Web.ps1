param([ValidateRange(1024,65535)][int]$Port = 4173)
$ErrorActionPreference = 'Stop'
$taskStatePath = Join-Path $PSScriptRoot ".preview-state\$Port.json"
if (Test-Path -LiteralPath $taskStatePath) {
    $taskState = Get-Content -LiteralPath $taskStatePath -Raw | ConvertFrom-Json
    Invoke-RestMethod "http://127.0.0.1:$Port/_preview/stop" -Method Post -Headers @{Authorization = "Bearer $($taskState.token)"} -TimeoutSec 3 | Out-Null
    Write-Output 'Local preview stopped.'
} else { Write-Output 'No preview started by this folder is running on that port.' }
