<#
.SYNOPSIS
    Publishes one mutation scope as a neutral GitHub Check.

.DESCRIPTION
    Publishes duration, counts, mutated files, and actionable mutants to a scope-specific
    Check. A later workflow job reads compact check metadata and upserts one concise PR
    comment. Raw reports remain on the ephemeral runner.
#>
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SummaryPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository,

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
    $lines = @(
        $publishedActionable |
            ForEach-Object {
                "- $($_.Status): $($_.FileName):$($_.Line) — $($_.Mutator) → $($_.Replacement)"
            })
    if ($omittedActionableCount -gt 0) {
        $lines += "- Omitted $omittedActionableCount additional mutants from the Check text size budget."
    }
    $lines -join "`n"
}

$evidence = [ordered]@{
    schemaVersion = 1
    scope = $scope
    durationSeconds = [double]$summary.durationSeconds
    score = $summary.mutationScore
    killed = [int]$summary.counts.Killed
    survived = [int]$summary.counts.Survived
    noCoverage = [int]$summary.counts.NoCoverage
    compileErrors = [int]$summary.counts.CompileError
    actionable = $actionable.Count
}
$evidenceJson = ConvertTo-Json $evidence -Compress
$externalId = 'needlr-mutation:' + [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($evidenceJson))

if ($DryRun) {
    return [PSCustomObject]@{
        Scope = $scope
        CheckName = $checkName
        CheckTitle = $checkTitle
        CheckSummary = $checkSummary
        CheckText = $checkText
        ExternalId = $externalId
        ActionableCount = $actionable.Count
    }
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
    external_id = $externalId
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

Write-Host "Published mutation Check for '$scope'."
