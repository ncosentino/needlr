<#
.SYNOPSIS
    Enforces Needlr's minimum line and branch coverage thresholds.

.DESCRIPTION
    Reads the ReportGenerator JsonSummary produced by CI and fails when the
    measured coverage drops below the agreed thresholds. Thresholds are
    deliberately set slightly below the last observed hosted result so that
    ordinary run-to-run noise does not break the build while real regressions
    still do.

    The thresholds and the rationale behind them are documented in
    docs/coverage.md; update both together.
#>
param(
    [string]$SummaryPath = 'coverage/report/Summary.json',
    [double]$MinimumLineCoverage = 91,
    [double]$MinimumBranchCoverage = 83,
    [switch]$NoCiOutput
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SummaryPath)) {
    throw "Coverage summary '$SummaryPath' was not found. Run the coverage collection and ReportGenerator steps first."
}

$summaryJson = Get-Content -LiteralPath $SummaryPath -Raw
if ([string]::IsNullOrWhiteSpace($summaryJson)) {
    throw "Coverage summary '$SummaryPath' is empty."
}

$summary = ($summaryJson | ConvertFrom-Json).summary
if ($null -eq $summary) {
    throw "Coverage summary '$SummaryPath' does not contain a 'summary' section."
}

foreach ($property in @('linecoverage', 'branchcoverage')) {
    if ($null -eq $summary.$property) {
        throw "Coverage summary '$SummaryPath' does not contain '$property'."
    }
}

$lineCoverage = [double]$summary.linecoverage
$branchCoverage = [double]$summary.branchcoverage

$failures = @()
if ($lineCoverage -lt $MinimumLineCoverage) {
    $failures += "Line coverage $lineCoverage% is below the required $MinimumLineCoverage%."
}
if ($branchCoverage -lt $MinimumBranchCoverage) {
    $failures += "Branch coverage $branchCoverage% is below the required $MinimumBranchCoverage%."
}

$result = [ordered]@{
    line_coverage = $lineCoverage
    branch_coverage = $branchCoverage
    covered_lines = $summary.coveredlines
    coverable_lines = $summary.coverablelines
    covered_branches = $summary.coveredbranches
    total_branches = $summary.totalbranches
    minimum_line_coverage = $MinimumLineCoverage
    minimum_branch_coverage = $MinimumBranchCoverage
    passed = ($failures.Count -eq 0)
}

if (-not $NoCiOutput) {
    Write-Host "Line coverage: $lineCoverage% ($($summary.coveredlines)/$($summary.coverablelines)); minimum $MinimumLineCoverage%."
    Write-Host "Branch coverage: $branchCoverage% ($($summary.coveredbranches)/$($summary.totalbranches)); minimum $MinimumBranchCoverage%."

    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
    }
}

$result | ConvertTo-Json -Compress

if ($failures.Count -gt 0) {
    exit 1
}

exit 0
