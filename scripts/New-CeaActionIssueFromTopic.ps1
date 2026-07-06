#Requires -Version 7.0
<#
.SYNOPSIS
  Create a QuickerHub/cea-action-issues GitHub issue from a getquicker action topic.

.EXAMPLE
  pwsh -File ./scripts/New-CeaActionIssueFromTopic.ps1 -TopicId 41029 -Area ocr-studio -Type bug

.EXAMPLE
  pwsh -File ./scripts/New-CeaActionIssueFromTopic.ps1 -TopicId 41029 -Area ocr-studio -Type bug -ReplyAndArchive
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [int] $TopicId,

    [Parameter(Mandatory = $true)]
    [string] $Area,

    [ValidateSet('bug', 'feat', 'chore', 'idea')]
    [string] $Type = 'bug',

    [string] $Repo = 'QuickerHub/cea-action-issues',

    [switch] $ReplyAndArchive,

    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

function Get-QkagentExe {
    $cmd = Get-Command qkagent -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $local = Join-Path $PSScriptRoot '..' 'publish' 'agent' 'qkagent.exe'
    if (Test-Path -LiteralPath $local) { return (Resolve-Path -LiteralPath $local).Path }
    throw 'qkagent not found. Run publish/publish-agent.ps1 first.'
}

$qkagent = Get-QkagentExe
$jsonLine = & $qkagent action-topics get --id $TopicId --json 2>&1 | Where-Object { $_ -match '^\{' } | Select-Object -Last 1
if (-not $jsonLine) { throw "qkagent get failed for topic #$TopicId" }

$payload = $jsonLine | ConvertFrom-Json
$topic = $payload.topic
$labels = @("area:actions", "type:$Type")
$title = "[$Area] $($topic.Title)"

$body = @"
## What happened
$($topic.BodyText)

## Source
- getquicker topic: $($topic.TopicUrl)
- category: $($topic.Category)
- author: $($topic.Author)
- action: $($topic.SharedActionTitle) ($($topic.SharedActionId))

## Steps to reproduce
1. (fill from topic / follow up with user)

## Version / environment
(fill Quicker / package version)
"@

if ($DryRun) {
    Write-Output "TITLE: $title"
    Write-Output "LABELS: $($labels -join ', ')"
    Write-Output $body
    exit 0
}

$issueUrl = gh issue create -R $Repo --title $title --label $labels --body $body
Write-Host "Created: $issueUrl" -ForegroundColor Green

if (-not $issueUrl -match '#(\d+)$') {
    throw "Could not parse issue number from: $issueUrl"
}
$issueNum = $Matches[1]

if ($ReplyAndArchive) {
    $reply = @"
感谢反馈！我们已在 GitHub 跟踪此问题：
$issueUrl

后续版本修复后会在此通知。欢迎补充复现步骤或环境信息。
"@
    $replyFile = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -LiteralPath $replyFile -Value $reply -Encoding utf8NoBOM
        & $qkagent action-topics reply --id $TopicId --content-file $replyFile --json | Out-Null
        & $qkagent action-topics archive --id $TopicId --json | Out-Null
        Write-Host "Replied and archived topic #$TopicId" -ForegroundColor Green
    }
    finally {
        Remove-Item -LiteralPath $replyFile -Force -ErrorAction SilentlyContinue
    }
}

Write-Output $issueUrl
