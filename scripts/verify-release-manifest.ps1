<#
.SYNOPSIS
    Verifies a packed Needlr release candidate against its manifest.

.DESCRIPTION
    Re-computes the SHA-256 digest of every packed file, rejects packages the manifest
    does not describe, and compares the manifest against the caller's expectations for
    release version, package version, source commit, and producing workflow run.

    Publication jobs call this before their first irreversible operation so promoted
    or handed-off artifacts are proven to belong to the released commit without
    restoring, building, or testing the source again.

    Exits non-zero and reports every mismatch when verification fails.

.PARAMETER PackageDirectory
    Directory holding the packed .nupkg and .snupkg files.

.PARAMETER ManifestPath
    Manifest path. Defaults to release-manifest.json inside PackageDirectory.

.PARAMETER ExpectedVersion
    Semantic release version the candidate must declare.

.PARAMETER ExpectedPackageVersion
    NuGet package version the candidate must declare.

.PARAMETER ExpectedSourceSha
    Commit SHA the candidate must have been produced from.

.PARAMETER ExpectedProducingRunId
    Workflow run identifier the candidate must have been produced by.

.PARAMETER ExpectedValidatedCiRunId
    Main CI run identifier that must have validated the candidate's source commit.
#>
param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$ManifestPath,
    [string]$ExpectedVersion,
    [string]$ExpectedPackageVersion,
    [string]$ExpectedSourceSha,
    [string]$ExpectedProducingRunId,
    [string]$ExpectedValidatedCiRunId
)

$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([Parameter(Mandatory = $true)][string]$Message)

    $failures.Add($Message)
}

function Assert-Expected {
    param(
        [Parameter(Mandatory = $true)][string]$Field,
        [Parameter(Mandatory = $true)][string]$Parameter,
        [string]$Expected,
        [string]$Actual
    )

    # An explicitly requested but empty expectation means the caller lost a workflow
    # value. Publishing against an unchecked field is never the safer outcome, so this
    # is recorded like every other failure and reported by the aggregate throw below.
    # Comparing an empty expectation against the manifest afterwards would only repeat
    # the same problem as a second, less useful message.
    if ([string]::IsNullOrWhiteSpace($Expected)) {
        Add-Failure "Expectation '$Parameter' was requested without a value."
        return
    }

    if (-not [string]::Equals($Expected, $Actual, [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Failure "Manifest $Field is '$Actual' but the release requires '$Expected'."
    }
}

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    throw "Package directory '$PackageDirectory' does not exist."
}

$resolvedPackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $resolvedPackageDirectory 'release-manifest.json'
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Release manifest '$ManifestPath' does not exist."
}

try {
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
} catch {
    throw "Release manifest '$ManifestPath' is not valid JSON: $($_.Exception.Message)"
}

if ($manifest.schemaVersion -ne 1) {
    throw "Release manifest schema version '$($manifest.schemaVersion)' is not supported."
}

foreach ($field in @('version', 'packageVersion', 'sourceSha', 'producingRunId', 'producingWorkflow')) {
    if ([string]::IsNullOrWhiteSpace($manifest.$field)) {
        throw "Release manifest is missing required field '$field'."
    }
}

$manifestPackages = @($manifest.packages)
if ($manifestPackages.Count -eq 0) {
    throw 'Release manifest does not describe any packages.'
}

Write-Host "Verifying release candidate in '$resolvedPackageDirectory'." -ForegroundColor Cyan

$expectations = @(
    @{ Field = 'version'; Parameter = 'ExpectedVersion'; Expected = $ExpectedVersion; Actual = $manifest.version },
    @{ Field = 'packageVersion'; Parameter = 'ExpectedPackageVersion'; Expected = $ExpectedPackageVersion; Actual = $manifest.packageVersion },
    @{ Field = 'sourceSha'; Parameter = 'ExpectedSourceSha'; Expected = $ExpectedSourceSha; Actual = $manifest.sourceSha },
    @{ Field = 'producingRunId'; Parameter = 'ExpectedProducingRunId'; Expected = $ExpectedProducingRunId; Actual = $manifest.producingRunId },
    @{ Field = 'validatedCiRunId'; Parameter = 'ExpectedValidatedCiRunId'; Expected = $ExpectedValidatedCiRunId; Actual = $manifest.validatedCiRunId }
)

foreach ($expectation in $expectations) {
    if (-not $PSBoundParameters.ContainsKey($expectation.Parameter)) {
        continue
    }

    Assert-Expected `
        -Field $expectation.Field `
        -Parameter $expectation.Parameter `
        -Expected $expectation.Expected `
        -Actual $expectation.Actual
}

$versionSuffix = ".$($manifest.packageVersion)"
$presentFiles = @(
    Get-ChildItem -LiteralPath $resolvedPackageDirectory -File |
        Where-Object { $_.Extension -in @('.nupkg', '.snupkg') }
)
$presentNames = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@($presentFiles | ForEach-Object { $_.Name }),
    [System.StringComparer]::OrdinalIgnoreCase)
$manifestNames = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@($manifestPackages | ForEach-Object { $_.name }),
    [System.StringComparer]::OrdinalIgnoreCase)

$primaryCount = 0
foreach ($package in $manifestPackages) {
    if ([string]::IsNullOrWhiteSpace($package.name) -or
        [string]::IsNullOrWhiteSpace($package.sha256)) {
        Add-Failure 'Release manifest contains a package entry without a name or digest.'
        continue
    }

    if ($package.name.EndsWith('.nupkg', [System.StringComparison]::OrdinalIgnoreCase)) {
        $primaryCount++
    }

    $packagePath = Join-Path $resolvedPackageDirectory $package.name
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        Add-Failure "Manifest package '$($package.name)' is missing from the candidate."
        continue
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($package.name)
    if (-not $baseName.EndsWith($versionSuffix, [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Failure "Package '$($package.name)' does not use manifest version '$($manifest.packageVersion)'."
    }

    $actualDigest = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualDigest -ne $package.sha256.ToLowerInvariant()) {
        Add-Failure "Package '$($package.name)' digest is '$actualDigest' but the manifest records '$($package.sha256)'."
    }
}

if ($primaryCount -eq 0) {
    Add-Failure 'Release manifest does not describe any .nupkg package.'
}

foreach ($name in $presentNames) {
    if (-not $manifestNames.Contains($name)) {
        Add-Failure "Package '$name' is present in the candidate but absent from the manifest."
    }
}

if ($failures.Count -gt 0) {
    $detail = ($failures | ForEach-Object { "  - $_" }) -join "`n"
    throw "Release candidate verification failed with $($failures.Count) problem(s):`n$detail"
}

Write-Host "Verified $($manifestPackages.Count) packages for $($manifest.version) from $($manifest.sourceSha)." -ForegroundColor Green
Write-Host "Produced by $($manifest.producingWorkflow) run $($manifest.producingRunId)."
