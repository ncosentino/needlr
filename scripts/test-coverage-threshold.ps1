<#
.SYNOPSIS
    Validates Needlr coverage threshold enforcement.
#>

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'check-coverage.ps1'
$workingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("needlr-coverage-" + [Guid]::NewGuid().ToString('n'))

function New-Summary {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $path = Join-Path $workingDirectory "$Name.json"
    Set-Content -LiteralPath $path -Value $Content -NoNewline
    return $path
}

function Assert-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$SummaryPath,
        [Parameter(Mandatory = $true)][double]$MinimumLineCoverage,
        [Parameter(Mandatory = $true)][double]$MinimumBranchCoverage,
        [Parameter(Mandatory = $true)][bool]$ExpectedPassed
    )

    $output = & $scriptPath `
        -SummaryPath $SummaryPath `
        -MinimumLineCoverage $MinimumLineCoverage `
        -MinimumBranchCoverage $MinimumBranchCoverage `
        -NoCiOutput
    $exitCode = $LASTEXITCODE
    $result = ($output | ConvertFrom-Json)

    if ($result.passed -ne $ExpectedPassed) {
        throw "$Name failed. Expected passed=$ExpectedPassed; actual passed=$($result.passed)."
    }

    $expectedExitCode = if ($ExpectedPassed) { 0 } else { 1 }
    if ($exitCode -ne $expectedExitCode) {
        throw "$Name failed. Expected exit code $expectedExitCode; actual $exitCode."
    }

    Write-Host "PASS: $Name"
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$SummaryPath
    )

    try {
        & $scriptPath -SummaryPath $SummaryPath -NoCiOutput | Out-Null
    } catch {
        Write-Host "PASS: $Name"
        return
    }

    throw "$Name failed. Expected a terminating error for '$SummaryPath'."
}

try {
    New-Item -ItemType Directory -Path $workingDirectory -Force | Out-Null

    $healthy = New-Summary -Name 'healthy' -Content @'
{
  "summary": {
    "coveredlines": 13050,
    "coverablelines": 14076,
    "linecoverage": 92.7,
    "coveredbranches": 5992,
    "totalbranches": 7087,
    "branchcoverage": 84.5
  }
}
'@

    Assert-Gate `
        -Name 'Measured coverage clears the committed thresholds' `
        -SummaryPath $healthy `
        -MinimumLineCoverage 91 `
        -MinimumBranchCoverage 83 `
        -ExpectedPassed $true

    Assert-Gate `
        -Name 'Exact threshold match passes' `
        -SummaryPath $healthy `
        -MinimumLineCoverage 92.7 `
        -MinimumBranchCoverage 84.5 `
        -ExpectedPassed $true

    Assert-Gate `
        -Name 'Branch regression fails' `
        -SummaryPath $healthy `
        -MinimumLineCoverage 91 `
        -MinimumBranchCoverage 85 `
        -ExpectedPassed $false

    Assert-Gate `
        -Name 'Line regression fails' `
        -SummaryPath $healthy `
        -MinimumLineCoverage 93 `
        -MinimumBranchCoverage 83 `
        -ExpectedPassed $false

    $missingBranches = New-Summary -Name 'missing-branches' -Content @'
{
  "summary": {
    "coveredlines": 10,
    "coverablelines": 10,
    "linecoverage": 100.0
  }
}
'@

    Assert-Throws `
        -Name 'Summary without branch coverage is rejected' `
        -SummaryPath $missingBranches

    $emptySummary = New-Summary -Name 'empty' -Content '{}'

    Assert-Throws `
        -Name 'Summary without a summary section is rejected' `
        -SummaryPath $emptySummary

    Assert-Throws `
        -Name 'Missing summary file is rejected' `
        -SummaryPath (Join-Path $workingDirectory 'does-not-exist.json')

    $defaults = & $scriptPath -SummaryPath $healthy -NoCiOutput | ConvertFrom-Json
    if ($defaults.minimum_line_coverage -ne 91 -or $defaults.minimum_branch_coverage -ne 83) {
        throw "Committed default thresholds changed. Expected line=91 branch=83; actual line=$($defaults.minimum_line_coverage) branch=$($defaults.minimum_branch_coverage). Update docs/coverage.md in the same change."
    }

    Write-Host 'PASS: Committed default thresholds match the documented values'
    Write-Host 'Coverage threshold validation passed.' -ForegroundColor Green
} finally {
    Remove-Item -LiteralPath $workingDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
