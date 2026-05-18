# Publishes QuickerAgent.Console as non-single-file win-x64 self-contained layout.
# Safe to run from repo root or from this directory.

$ErrorActionPreference = 'Stop'

function Get-QuickerAgentRepoRoot {
    param([string]$StartPath)

    if ([string]::IsNullOrWhiteSpace($StartPath)) {
        $StartPath = (Get-Location).Path
    }

    $current = (Resolve-Path -LiteralPath $StartPath).Path.TrimEnd('\')
    for ($i = 0; $i -lt 8; $i++) {
        $marker = Join-Path $current 'QuickerAgent.Console\QuickerAgent.Console.csproj'
        if (Test-Path -LiteralPath $marker) {
            return $current
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrEmpty($parent)) {
            break
        }
        $current = (Get-Item -LiteralPath $parent).FullName.TrimEnd('\')
    }

    throw "Repository root not found (missing QuickerAgent.Console\QuickerAgent.Console.csproj). Start from quicker-agent or run from publish/."
}

$repoRoot = Get-QuickerAgentRepoRoot -StartPath $PSScriptRoot
Set-Location -LiteralPath $repoRoot

Write-Host "Publishing qkagent.exe (QuickerAgent.Console, non-single-file, win-x64, self-contained)..." -ForegroundColor Green

$publishDir = Join-Path $repoRoot 'publish\agent'
if (Test-Path -LiteralPath $publishDir) {
    Write-Host "Cleaning previous publish output..." -ForegroundColor Yellow
    Remove-Item -LiteralPath (Join-Path $publishDir '*') -Recurse -Force -ErrorAction SilentlyContinue
}
else {
    Write-Host "Creating publish directory: $publishDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}

$csproj = Join-Path $repoRoot 'QuickerAgent.Console\QuickerAgent.Console.csproj'
Write-Host "dotnet publish -> $publishDir" -ForegroundColor Yellow
dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed (dotnet exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Publish succeeded." -ForegroundColor Green
Write-Host "Executable: $publishDir\qkagent.exe" -ForegroundColor Cyan

$envFile = Join-Path $repoRoot '.env'
if (Test-Path -LiteralPath $envFile) {
    Write-Host "Copying .env to publish folder..." -ForegroundColor Yellow
    Copy-Item -LiteralPath $envFile -Destination (Join-Path $publishDir '.env') -Force
    Write-Host "Copied .env" -ForegroundColor Green
}
else {
    Write-Host "No .env in repo root; skip copy." -ForegroundColor Yellow
}

$example = Join-Path $repoRoot 'env.example'
if (Test-Path -LiteralPath $example) {
    Copy-Item -LiteralPath $example -Destination (Join-Path $publishDir 'env.example') -Force
    Write-Host "Copied env.example to publish folder." -ForegroundColor Green
}

Write-Host "Installing Playwright Chromium (if bootstrap present)..." -ForegroundColor Yellow
$originalLocation = Get-Location
try {
    Set-Location -LiteralPath $publishDir
    $playwrightPs1 = Join-Path $publishDir 'playwright.ps1'
    $playwrightCli = Join-Path $publishDir 'Microsoft.Playwright.CLI.dll'
    if (Test-Path -LiteralPath $playwrightPs1) {
        Write-Host "Running: pwsh -File playwright.ps1 install chromium" -ForegroundColor Green
        pwsh -NoProfile -File $playwrightPs1 install chromium
    }
    elseif (Test-Path -LiteralPath $playwrightCli) {
        Write-Host "Running Playwright install chromium (CLI dll)..." -ForegroundColor Green
        dotnet exec Microsoft.Playwright.CLI.dll install chromium
    }
    else {
        Write-Host "No playwright.ps1 or Microsoft.Playwright.CLI.dll; skip browser install." -ForegroundColor Yellow
        Write-Host "action-doc needs Chrome/Edge or Playwright Chromium; run install when you add the CLI." -ForegroundColor Cyan
    }
}
catch {
    Write-Host "Playwright install failed: $($_.Exception.Message)" -ForegroundColor Yellow
}
finally {
    Set-Location -LiteralPath $originalLocation.Path
}

$exePath = Join-Path $publishDir 'qkagent.exe'
if (Test-Path -LiteralPath $exePath) {
    $fileInfo = Get-Item -LiteralPath $exePath
    Write-Host "Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Examples:" -ForegroundColor Yellow
Write-Host "  .\publish\agent\qkagent.exe action-doc pull --code <guid> --json"
Write-Host "  .\publish\agent\qkagent.exe action-doc push --code <guid> --json"
Write-Host ""

try {
    $publishPath = (Resolve-Path -LiteralPath $publishDir).Path
}
catch {
    $publishPath = $publishDir
}

Write-Host "Adding publish folder to user PATH (if missing)..." -ForegroundColor Yellow
Write-Host "Publish path: $publishPath" -ForegroundColor Cyan

$currentPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
if ($currentPath -notlike "*$publishPath*") {
    $newPath = if ($currentPath) { "$currentPath;$publishPath" } else { $publishPath }
    [Environment]::SetEnvironmentVariable('PATH', $newPath, 'User')
    Write-Host "Appended to user PATH: $publishPath" -ForegroundColor Green
    Write-Host "Restart the terminal (or refreshenv) for PATH to take effect." -ForegroundColor Yellow
}
else {
    Write-Host "Publish path already on user PATH." -ForegroundColor Green
}

Write-Host ""
Write-Host "When PATH includes the publish folder (open a new terminal): qkagent.exe action-doc upload --dir ..." -ForegroundColor Cyan

exit 0
