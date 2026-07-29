<#
.SYNOPSIS
    Packs the published Needlr package subset into one output directory.

.DESCRIPTION
    Selects every packable NexusLabs.Needlr* project, excluding test, benchmark, and
    integration-test projects, and packs each selected project into the requested
    output directory.

    Release preparation and the opt-in CI release candidate share this selection so
    the published package set cannot drift between the two producers.

    Exits non-zero when the selection is empty or when any pack fails.

.PARAMETER OutputDirectory
    Directory that receives the packed .nupkg and .snupkg files. Required unless
    -ListOnly is specified.

.PARAMETER Configuration
    Build configuration to pack. Defaults to Release.

.PARAMETER NoBuild
    Pack without building. Requires an existing build of the same configuration.

.PARAMETER PublicRelease
    Pack with -p:PublicRelease=true so NBGV emits the exact release version.

.PARAMETER ListOnly
    Print the selected project paths as JSON without packing anything.
#>
param(
    [string]$OutputDirectory,
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$PublicRelease,
    [switch]$ListOnly
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
$sourceRoot = Join-Path $repoRoot 'src'

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Source directory '$sourceRoot' does not exist."
}

$excludedSuffixes = @(
    '.Tests.csproj',
    '.Benchmarks.csproj',
    'IntegrationTests.csproj'
)

$projects = @(
    Get-ChildItem -Path $sourceRoot -Recurse -File -Filter 'NexusLabs.Needlr*.csproj' |
        Where-Object {
            $name = $_.Name
            $null -eq (
                $excludedSuffixes |
                    Where-Object {
                        $name.EndsWith($_, [System.StringComparison]::OrdinalIgnoreCase)
                    } |
                    Select-Object -First 1)
        } |
        ForEach-Object {
            [System.IO.Path]::GetRelativePath($repoRoot, $_.FullName).Replace('\', '/')
        } |
        Sort-Object -CaseSensitive
)

if ($projects.Count -eq 0) {
    throw "No packable projects matched 'NexusLabs.Needlr*' under '$sourceRoot'."
}

if ($ListOnly) {
    $projects | ConvertTo-Json -AsArray
    exit 0
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    throw 'OutputDirectory is required unless -ListOnly is specified.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$resolvedOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path

$packArguments = @('--configuration', $Configuration, '--output', $resolvedOutput)
if ($NoBuild) {
    $packArguments += '--no-build'
}
if ($PublicRelease) {
    $packArguments += '-p:PublicRelease=true'
}

Write-Host "Packing $($projects.Count) projects into '$resolvedOutput'." -ForegroundColor Cyan
foreach ($project in $projects) {
    Write-Host " - $project"
}

foreach ($project in $projects) {
    & dotnet pack (Join-Path $repoRoot $project) @packArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for '$project'."
    }
}

Write-Host "Packed $($projects.Count) projects." -ForegroundColor Green
