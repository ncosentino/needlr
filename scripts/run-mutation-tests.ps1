<#
.SYNOPSIS
    Runs one bounded Needlr mutation-testing scope.

.PARAMETER Scope
    Scope name declared in scripts/mutation/scopes.json.

.PARAMETER MutateFiles
    Project-relative authored source files selected for this pull request.

.PARAMETER SinceTarget
    Optional git target used for Stryker changed-code analysis.

.PARAMETER OutputPath
    Report root. Defaults to artifacts/mutation under the repository.
#>
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Scope,

    [string[]]$MutateFiles,

    [string]$SinceTarget,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $PSScriptRoot 'mutation' 'scopes.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$definition = @($manifest.scopes) |
    Where-Object { [string]$_.name -ceq $Scope } |
    Select-Object -First 1
if ($null -eq $definition) {
    throw "Unknown mutation scope '$Scope'."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot 'artifacts' 'mutation'
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$scopeOutput = Join-Path $OutputPath $Scope
if (Test-Path -LiteralPath $scopeOutput) {
    throw (
        "Mutation output already exists at '$scopeOutput'. " +
        'Choose a new OutputPath or remove that scope directory explicitly.')
}

$maxFiles = [int]$manifest.limits.maxFilesPerScope
$selectedFiles = if (@($MutateFiles).Count -gt 0) {
    @($MutateFiles)
} else {
    @($definition.priorityFiles | Select-Object -First $maxFiles)
}
if ($selectedFiles.Count -gt $maxFiles) {
    throw "Scope '$Scope' exceeds the $maxFiles-file mutation limit."
}

$workingDirectory = Join-Path $repoRoot ([string]$definition.workingDirectory)
$configDirectory = Join-Path $OutputPath 'configs'
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
$configPath = Join-Path $configDirectory "$Scope.json"

$defaults = $manifest.defaults
$options = [ordered]@{
    project = [string]$definition.project
    'test-runner' = [string]$defaults.testRunner
    configuration = [string]$defaults.configuration
    'mutation-level' = [string]$defaults.mutationLevel
    'coverage-analysis' = [string]$defaults.coverageAnalysis
    concurrency = [int]$defaults.concurrency
    mutate = $selectedFiles
    reporters = @($defaults.reporters)
    'report-file-name' = 'mutation-report'
    thresholds = [ordered]@{
        high = [int]$defaults.thresholds.high
        low = [int]$defaults.thresholds.low
        break = [int]$defaults.thresholds.break
    }
    'break-on-initial-test-failure' = $true
}
if (-not [string]::IsNullOrWhiteSpace($SinceTarget)) {
    $options.since = [ordered]@{
        enabled = $true
        target = $SinceTarget
    }
}

[IO.File]::WriteAllText(
    $configPath,
    (ConvertTo-Json ([ordered]@{ 'stryker-config' = $options }) -Depth 10) + "`n",
    [Text.UTF8Encoding]::new($false))

Push-Location $repoRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

Write-Host (
    "Running Stryker.NET scope '$Scope' for files: " +
    ($selectedFiles -join ', ')) -ForegroundColor Cyan
$relativeConfigPath = [IO.Path]::GetRelativePath(
    $workingDirectory,
    $configPath)
Push-Location $workingDirectory
try {
    & dotnet stryker `
        --config-file $relativeConfigPath `
        --output $scopeOutput `
        --skip-version-check 2>&1 |
        Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Stryker.NET scope '$Scope' failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

$reportDirectory = Join-Path $scopeOutput 'reports'
$jsonPath = Join-Path $reportDirectory 'mutation-report.json'
$markdownPath = Join-Path $reportDirectory 'mutation-report.md'
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
    throw "Stryker.NET scope '$Scope' did not produce $jsonPath."
}
if (-not (Test-Path -LiteralPath $markdownPath -PathType Leaf)) {
    throw "Stryker.NET scope '$Scope' did not produce $markdownPath."
}

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
$mutants = @(
    $report.files.PSObject.Properties |
        ForEach-Object {
            $fileName = $_.Name
            @($_.Value.mutants) |
                ForEach-Object {
                    [PSCustomObject]@{
                        FileName = $fileName
                        Line = [int]$_.location.start.line
                        Mutator = [string]$_.mutatorName
                        Replacement = [string]$_.replacement
                        Status = [string]$_.status
                    }
                }
        })
if ($mutants.Count -eq 0) {
    throw "Stryker.NET scope '$Scope' produced no mutants."
}

$counts = [ordered]@{
    Killed = 0
    Survived = 0
    NoCoverage = 0
    Timeout = 0
    CompileError = 0
    RuntimeError = 0
    Ignored = 0
}
foreach ($group in ($mutants | Group-Object Status | Sort-Object Name)) {
    $counts[$group.Name] = $group.Count
}

$detected = $counts.Killed + $counts.Timeout
$undetected = $counts.Survived + $counts.NoCoverage
$score = if ($detected + $undetected -eq 0) {
    $null
} else {
    [Math]::Round(($detected * 100.0) / ($detected + $undetected), 2)
}
$actionable = @(
    $mutants |
        Where-Object Status -in @('Survived', 'NoCoverage') |
        Sort-Object FileName, Line, Mutator)

$summary = [ordered]@{
    scope = $Scope
    mutateFiles = $selectedFiles
    sinceTarget = $SinceTarget
    totalMutants = $mutants.Count
    mutationScore = $score
    counts = $counts
    reportDirectory = $reportDirectory
}
$summaryPath = Join-Path $scopeOutput 'mutation-summary.json'
[IO.File]::WriteAllText(
    $summaryPath,
    (ConvertTo-Json $summary -Depth 5) + "`n",
    [Text.UTF8Encoding]::new($false))

Write-Host (
    "Scope '$Scope': score=$score; killed=$($counts.Killed); " +
    "survived=$($counts.Survived); no-coverage=$($counts.NoCoverage); " +
    "compile-errors=$($counts.CompileError).")

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value @"
## Mutation testing: $Scope

_Ephemeral advisory result. Mutation score does not gate this workflow._

- Mutated files: $($selectedFiles -join ', ')
- Since target: $($SinceTarget -replace '^$', 'none')
- Actionable mutants: $($actionable.Count)

"@
    Get-Content -LiteralPath $markdownPath |
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY

    if ($actionable.Count -gt 0) {
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value @"

### Actionable mutants

| Status | File | Line | Mutator | Replacement |
| --- | --- | ---: | --- | --- |
"@
        foreach ($mutant in $actionable) {
            $replacement = $mutant.Replacement.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
            Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value (
                "| $($mutant.Status) | $($mutant.FileName) | $($mutant.Line) | " +
                "$($mutant.Mutator) | ``$replacement`` |")
        }
    }
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "`n"
}

[PSCustomObject]$summary
