<#
.SYNOPSIS
    Publishes one mutation scope as current pull-request evidence.

.DESCRIPTION
    Upserts one scope-specific PR comment and one neutral check run. Raw reports remain
    on the ephemeral runner and are never uploaded or used as a baseline.
#>
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SummaryPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository,

    [Parameter(Mandatory)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$PullRequestNumber,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$HeadSha,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$RunUrl,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Invoke-GhApi {
    param(
        [Parameter(Mandatory)][ValidateSet('POST', 'PATCH')]
        [string]$Method,
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)]$Payload
    )

    $payloadPath = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllText(
            $payloadPath,
            (ConvertTo-Json $Payload -Depth 10) + "`n",
            [Text.UTF8Encoding]::new($false))
        $output = & gh api `
            --method $Method `
            $Endpoint `
            --input $payloadPath 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "gh api $Method $Endpoint failed.`n$($output | Out-String)"
        }
        return ($output | Out-String) | ConvertFrom-Json
    } finally {
        Remove-Item -LiteralPath $payloadPath -Force
    }
}

if (-not (Test-Path -LiteralPath $SummaryPath -PathType Leaf)) {
    throw "Mutation summary was not found at '$SummaryPath'."
}

$summary = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
$reportPath = Join-Path ([string]$summary.reportDirectory) 'mutation-report.json'
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Mutation report was not found at '$reportPath'."
}

$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$maxPublishedMutants = 100
$actionable = @(
    $report.files.PSObject.Properties |
        ForEach-Object {
            $fileName = $_.Name
            @($_.Value.mutants) |
                Where-Object status -in @('Survived', 'NoCoverage') |
                ForEach-Object {
                    [PSCustomObject]@{
                        Status = [string]$_.status
                        FileName = $fileName
                        Line = [int]$_.location.start.line
                        Mutator = [string]$_.mutatorName
                        Replacement = [string]$_.replacement
                    }
                }
        } |
        Sort-Object FileName, Line, Mutator)
$publishedActionable = @($actionable | Select-Object -First $maxPublishedMutants)
$omittedActionableCount = $actionable.Count - $publishedActionable.Count

$scope = [string]$summary.scope
$duration = [TimeSpan]::FromSeconds([double]$summary.durationSeconds)
$durationText = if ($duration.TotalMinutes -ge 1) {
    '{0}m {1}s' -f [Math]::Floor($duration.TotalMinutes), $duration.Seconds
} else {
    '{0}s' -f [Math]::Round($duration.TotalSeconds)
}
$scoreText = if ($null -eq $summary.mutationScore) {
    'N/A'
} else {
    "$($summary.mutationScore)%"
}
$marker = "<!-- needlr-mutation:$scope -->"
$shortSha = $HeadSha.Substring(0, 10)

$comment = [Text.StringBuilder]::new()
[void]$comment.AppendLine($marker)
[void]$comment.AppendLine("## Mutation evidence: ``$scope``")
[void]$comment.AppendLine()
[void]$comment.AppendLine(
    "Current head: ``$shortSha`` · [workflow run]($RunUrl)")
[void]$comment.AppendLine()
[void]$comment.AppendLine('| Duration | Score | Killed | Survived | No coverage | Compile errors |')
[void]$comment.AppendLine('| ---: | ---: | ---: | ---: | ---: | ---: |')
[void]$comment.AppendLine(
    "| $durationText | $scoreText | $($summary.counts.Killed) | " +
    "$($summary.counts.Survived) | $($summary.counts.NoCoverage) | " +
    "$($summary.counts.CompileError) |")
[void]$comment.AppendLine()
$mutatedFileList = (
    @($summary.mutateFiles) |
        ForEach-Object { "``$_``" }) -join ', '
[void]$comment.AppendLine(
    "**Mutated files:** $mutatedFileList")
[void]$comment.AppendLine()
[void]$comment.AppendLine(
    "_Advisory and ephemeral: no score gate, baseline, report artifact, or dashboard upload._")

if ($actionable.Count -gt 0) {
    [void]$comment.AppendLine()
    [void]$comment.AppendLine("### Actionable mutants ($($actionable.Count))")
    [void]$comment.AppendLine()
    [void]$comment.AppendLine('| Status | File | Line | Mutator | Replacement |')
    [void]$comment.AppendLine('| --- | --- | ---: | --- | --- |')
    foreach ($mutant in $publishedActionable) {
        $replacement = $mutant.Replacement.Replace('|', '\|')
        $replacement = $replacement.Replace("`r", ' ')
        $replacement = $replacement.Replace("`n", ' ')
        $replacement = $replacement.Replace('`', "'")
        [void]$comment.AppendLine(
            "| $($mutant.Status) | ``$($mutant.FileName)`` | $($mutant.Line) | " +
            "$($mutant.Mutator) | ``$replacement`` |")
    }
    if ($omittedActionableCount -gt 0) {
        [void]$comment.AppendLine()
        [void]$comment.AppendLine(
            "_Omitted $omittedActionableCount additional actionable mutants from the comment size budget. " +
            "Open the linked job summary for the complete current-run list._")
    }
}

$commentBody = $comment.ToString().TrimEnd()
$checkName = "Mutation evidence ($scope)"
$checkTitle = "$scoreText · $($summary.counts.Killed) killed · $($actionable.Count) actionable · $durationText"
$checkSummary = @"
| Score | Killed | Survived | No coverage | Compile errors | Duration |
| ---: | ---: | ---: | ---: | ---: | ---: |
| $scoreText | $($summary.counts.Killed) | $($summary.counts.Survived) | $($summary.counts.NoCoverage) | $($summary.counts.CompileError) | $durationText |

Mutated files: $(@($summary.mutateFiles) -join ', ')

Advisory result only. Raw reports remain ephemeral.
"@.Trim()
$checkText = if ($actionable.Count -eq 0) {
    'No surviving or uncovered mutants.'
} else {
    ($publishedActionable |
        ForEach-Object {
            "- $($_.Status): $($_.FileName):$($_.Line) — $($_.Mutator) → $($_.Replacement)"
        }) -join "`n"
}

if ($DryRun) {
    return [PSCustomObject]@{
        Scope = $scope
        CommentBody = $commentBody
        CheckName = $checkName
        CheckTitle = $checkTitle
        CheckSummary = $checkSummary
        CheckText = $checkText
        ActionableCount = $actionable.Count
    }
}

$commentsOutput = & gh api `
    --paginate `
    --slurp `
    "repos/$Repository/issues/$PullRequestNumber/comments?per_page=100" 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Could not list pull-request comments.`n$($commentsOutput | Out-String)"
}
$commentPages = ($commentsOutput | Out-String) | ConvertFrom-Json
$comments = @(
    foreach ($page in @($commentPages)) {
        foreach ($pageComment in @($page)) {
            $pageComment
        }
    })
$existingComment = @(
    $comments |
        Where-Object { [string]$_.body -like "*$marker*" } |
        Select-Object -First 1)
if ($existingComment.Count -eq 1) {
    [void](Invoke-GhApi `
        -Method PATCH `
        -Endpoint "repos/$Repository/issues/comments/$($existingComment[0].id)" `
        -Payload ([ordered]@{ body = $commentBody }))
} else {
    [void](Invoke-GhApi `
        -Method POST `
        -Endpoint "repos/$Repository/issues/$PullRequestNumber/comments" `
        -Payload ([ordered]@{ body = $commentBody }))
}

$encodedCheckName = [Uri]::EscapeDataString($checkName)
$checksOutput = & gh api `
    -H 'Accept: application/vnd.github+json' `
    "repos/$Repository/commits/$HeadSha/check-runs?check_name=$encodedCheckName&per_page=100" 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Could not list mutation check runs.`n$($checksOutput | Out-String)"
}
$checks = ($checksOutput | Out-String) | ConvertFrom-Json
$existingCheck = @($checks.check_runs | Select-Object -First 1)
$checkPayload = [ordered]@{
    name = $checkName
    status = 'completed'
    conclusion = 'neutral'
    details_url = $RunUrl
    output = [ordered]@{
        title = $checkTitle
        summary = $checkSummary
        text = $checkText
    }
}
if ($existingCheck.Count -eq 1) {
    [void](Invoke-GhApi `
        -Method PATCH `
        -Endpoint "repos/$Repository/check-runs/$($existingCheck[0].id)" `
        -Payload $checkPayload)
} else {
    $checkPayload['head_sha'] = $HeadSha
    [void](Invoke-GhApi `
        -Method POST `
        -Endpoint "repos/$Repository/check-runs" `
        -Payload $checkPayload)
}

Write-Host "Published mutation evidence for '$scope' to PR #$PullRequestNumber."
