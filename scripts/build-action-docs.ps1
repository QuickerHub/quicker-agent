# Build actions/*/info.html from doc.yaml
param(
    [string]$Id,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$builderRoot = Join-Path $repoRoot "action_doc_builder"

$uvArgs = @("run", "--project", $builderRoot, "python", "-m", "action_doc_builder.cli")
if ($Id) { $uvArgs += @("--id", $Id) }
if ($Force) { $uvArgs += "--force" }

Push-Location $repoRoot
try {
    & uv @uvArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
