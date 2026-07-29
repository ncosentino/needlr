<#
.SYNOPSIS
    Writes the machine-readable manifest for a packed Needlr release candidate.

.DESCRIPTION
    Validates that every packed file carries the expected package version, records a
    SHA-256 digest for each file, and writes a deterministic JSON manifest describing
    the candidate: source commit, release version, producing workflow run, and package
    digests.

    The manifest is the contract between the job that produces packages and every job
    that publishes them. Publication jobs re-verify it with verify-release-manifest.ps1
    instead of rebuilding the source commit.

    Exits non-zero when the candidate is empty, mis-versioned, or unreadable.

.PARAMETER PackageDirectory
    Directory holding the packed .nupkg and .snupkg files.

.PARAMETER Version
    Exact semantic release version, without the leading v.

.PARAMETER PackageVersion
    NuGet package version stamped into the packed file names.

.PARAMETER SourceSha
    Full 40-character commit SHA the candidate was produced from.

.PARAMETER ProducingRunId
    Identifier of the workflow run that produced the candidate.

.PARAMETER ProducingWorkflow
    Workflow file that produced the candidate, for example ci.yml or release.yml.

.PARAMETER ValidatedCiRunId
    Identifier of the main CI run that validated the source commit. Recorded so every
    publication job can prove it published artifacts prepared from a validated commit.

.PARAMETER ManifestPath
    Manifest output path. Defaults to release-manifest.json inside PackageDirectory.
#>
param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$PackageVersion,
    [Parameter(Mandatory = $true)][string]$SourceSha,
    [Parameter(Mandatory = $true)][string]$ProducingRunId,
    [Parameter(Mandatory = $true)][string]$ProducingWorkflow,
    [string]$ValidatedCiRunId,
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    throw "Package directory '$PackageDirectory' does not exist."
}

if ($SourceSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Source SHA '$SourceSha' is not a full 40-character commit SHA."
}

# A supplied-but-empty provenance value means the caller lost a workflow value.
if ($PSBoundParameters.ContainsKey('ValidatedCiRunId') -and
    [string]::IsNullOrWhiteSpace($ValidatedCiRunId)) {
    throw 'ValidatedCiRunId was supplied without a value.'
}

$resolvedPackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $resolvedPackageDirectory 'release-manifest.json'
}

$packageFiles = @(
    Get-ChildItem -LiteralPath $resolvedPackageDirectory -File |
        Where-Object { $_.Extension -in @('.nupkg', '.snupkg') } |
        Sort-Object -Property Name -CaseSensitive
)

$primaryPackages = @($packageFiles | Where-Object { $_.Extension -eq '.nupkg' })
if ($primaryPackages.Count -eq 0) {
    throw "No NuGet packages were produced in '$resolvedPackageDirectory'."
}

$versionSuffix = ".$PackageVersion"
$misversioned = @(
    $packageFiles |
        Where-Object {
            -not $_.BaseName.EndsWith(
                $versionSuffix,
                [System.StringComparison]::OrdinalIgnoreCase)
        } |
        ForEach-Object { $_.Name }
)
if ($misversioned.Count -gt 0) {
    throw "These packages do not use expected version '$PackageVersion': $($misversioned -join ', ')."
}

$packages = @(
    $packageFiles | ForEach-Object {
        [ordered]@{
            name = $_.Name
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            sizeBytes = $_.Length
        }
    }
)

$manifest = [ordered]@{
    schemaVersion = 1
    version = $Version
    packageVersion = $PackageVersion
    sourceSha = $SourceSha.ToLowerInvariant()
    producingRunId = $ProducingRunId
    producingWorkflow = $ProducingWorkflow
}

if (-not [string]::IsNullOrWhiteSpace($ValidatedCiRunId)) {
    $manifest['validatedCiRunId'] = $ValidatedCiRunId
}

$manifest['packages'] = $packages

$json = ($manifest | ConvertTo-Json -Depth 5) -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($ManifestPath, $json + "`n")

Write-Host "Wrote release manifest '$ManifestPath'." -ForegroundColor Green
Write-Host "  version:    $Version"
Write-Host "  sourceSha:  $($manifest.sourceSha)"
Write-Host "  producedBy: $ProducingWorkflow run $ProducingRunId"
foreach ($package in $packages) {
    Write-Host "  $($package.name) $($package.sha256)"
}
