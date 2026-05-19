# Dev: FastAPI API on 8765 + Vite (HMR) on 5176
$ErrorActionPreference = "Stop"
$PreviewRoot = $PSScriptRoot
$WebRoot = Join-Path $PreviewRoot "web"

if (-not (Test-Path (Join-Path $WebRoot "node_modules"))) {
    Write-Host "Installing npm dependencies..." -ForegroundColor Yellow
    Push-Location $WebRoot
    npm install
    Pop-Location
}

$apiJob = Start-Job -ScriptBlock {
    Set-Location $using:PreviewRoot
    if (Get-Command uv -ErrorAction SilentlyContinue) {
        uv run python preview_server.py
    } else {
        python preview_server.py
    }
}

Start-Sleep -Seconds 2
Write-Host "API:    http://127.0.0.1:8765/" -ForegroundColor Cyan
Write-Host "UI HMR: http://127.0.0.1:5176/" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop." -ForegroundColor Yellow

try {
    Push-Location $WebRoot
    npm run dev
} finally {
    Stop-Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job $apiJob -Force -ErrorAction SilentlyContinue
    Pop-Location
}
