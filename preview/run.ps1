# Start local HTML preview server (http://127.0.0.1:8765)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (Get-Command uv -ErrorAction SilentlyContinue) {
  uv sync
  uv run python preview_server.py
} else {
  Write-Host "uv not found; using python -m pip"
  python -m pip install -q fastapi "uvicorn[standard]"
  python preview_server.py
}
