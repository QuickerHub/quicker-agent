# Sync quicker-agent Cursor skills and slash commands to the user directory (~/.cursor).
# Source of truth: .cursor/skills/ and .cursor/commands/ in this repository.
# Safe to re-run; existing targets are removed and replaced (full overwrite).

#Requires -Version 5.1

[CmdletBinding()]
param(
    [string[]]$Skills = @('quicker-agent-exe', 'action-doc-workflow'),
    [switch]$SkipCommands,
    [switch]$SkipSkills
)

$ErrorActionPreference = 'Stop'

function Get-QuickerAgentRepoRoot {
    param([string]$StartPath)

    if ([string]::IsNullOrWhiteSpace($StartPath)) {
        $StartPath = $PSScriptRoot
    }

    $current = (Resolve-Path -LiteralPath $StartPath).Path.TrimEnd('\')
    for ($i = 0; $i -lt 10; $i++) {
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

    throw 'quicker-agent repo root not found (missing QuickerAgent.Console\QuickerAgent.Console.csproj).'
}

function Install-CursorUserSkill {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkillName,

        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    $src = Join-Path $SourceRoot 'skills' $SkillName
    $skillFile = Join-Path $src 'SKILL.md'
    if (-not (Test-Path -LiteralPath $skillFile)) {
        throw "Skill source not found: $skillFile"
    }

    $dest = Join-Path $DestinationRoot 'skills' $SkillName
    if (Test-Path -LiteralPath $dest) {
        Remove-Item -LiteralPath $dest -Recurse -Force
    }

    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item -Path (Join-Path $src '*') -Destination $dest -Recurse -Force

    Write-Host "  skill  $SkillName -> $dest" -ForegroundColor Green
}

function Install-CursorUserCommands {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    $src = Join-Path $SourceRoot 'commands'
    if (-not (Test-Path -LiteralPath $src)) {
        Write-Host '  commands source missing; skip.' -ForegroundColor Yellow
        return 0
    }

    $dest = Join-Path $DestinationRoot 'commands'
    New-Item -ItemType Directory -Path $dest -Force | Out-Null

    $count = 0
    Get-ChildItem -LiteralPath $src -File |
        Where-Object { $_.Extension -in '.md', '.mdc', '.markdown', '.txt' } |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dest $_.Name) -Force
            Write-Host "  command $($_.Name) -> $dest" -ForegroundColor Green
            $count++
        }

    return $count
}

function Remove-StaleCursorUserCommands {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot,

        [Parameter(Mandatory = $true)]
        [string[]]$RetiredNames
    )

    $dest = Join-Path $DestinationRoot 'commands'
    if (-not (Test-Path -LiteralPath $dest)) {
        return
    }

    foreach ($name in $RetiredNames) {
        $path = Join-Path $dest $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
            Write-Host "  removed stale command $name" -ForegroundColor Yellow
        }
    }
}

$repoRoot = Get-QuickerAgentRepoRoot -StartPath $PSScriptRoot
$cursorSrc = Join-Path $repoRoot '.cursor'
$userCursor = Join-Path $env:USERPROFILE '.cursor'

Write-Host "Installing Cursor user assets from $cursorSrc" -ForegroundColor Cyan
Write-Host "Target: $userCursor" -ForegroundColor Cyan

if (-not $SkipSkills) {
    Write-Host 'Skills:' -ForegroundColor Yellow
    foreach ($name in $Skills) {
        Install-CursorUserSkill -SkillName $name -SourceRoot $cursorSrc -DestinationRoot $userCursor
    }
}

if (-not $SkipCommands) {
    Write-Host 'Commands:' -ForegroundColor Yellow
    Remove-StaleCursorUserCommands -DestinationRoot $userCursor -RetiredNames @('quicker-agent-exe.md')
    $cmdCount = Install-CursorUserCommands -SourceRoot $cursorSrc -DestinationRoot $userCursor
    if ($cmdCount -eq 0) {
        Write-Host '  (no command files copied)' -ForegroundColor Yellow
    }
}

Write-Host ''
Write-Host 'Done. In Cursor chat type /action-info to run the slash command.' -ForegroundColor Cyan
Write-Host 'Skills are available globally (quicker-agent-exe, action-doc-workflow).' -ForegroundColor Cyan

exit 0
