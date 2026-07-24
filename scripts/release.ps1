<#
.SYNOPSIS
    Finalizes a Needlr release from an already-prepared commit on protected main.

.DESCRIPTION
    Validates the release version, changelog, analyzer tracking, synchronized main
    branch, same-commit CI, and package contents before creating and pushing only
    the version tag. Version and release-note changes must already be merged through
    a pull request.

.PARAMETER Version
    Exact semantic version to release, without the leading v.

.PARAMETER Prerelease
    Pre-release label used to calculate the next version from existing tags.

.PARAMETER Base
    Base X.Y.Z version used with Prerelease.

.PARAMETER DryRun
    Validates prepared release metadata and prints the tag-only actions without
    requiring main, querying CI, packing, creating a tag, or pushing.
#>
param(
  [Parameter(Position = 0)][string]$Version,
  [string]$Prerelease,
  [string]$Base,
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDir '..'))
Set-Location -Path $repoRoot

function Ensure-CleanRepo {
  $status = & git status --porcelain 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect the working tree.`n$($status | Out-String)"
  }
  if ($status) {
    throw "Working tree not clean. Commit or stash every tracked and untracked change first."
  }
}

function Ensure-Nbgv {
  $nbgvPath = Get-Command nbgv -ErrorAction SilentlyContinue
  if ($nbgvPath) {
    return
  }

  $toolsDir = Join-Path (Join-Path $HOME '.dotnet') 'tools'
  $toolName = if ($IsWindows) { 'nbgv.exe' } else { 'nbgv' }
  $toolsPath = Join-Path $toolsDir $toolName
  if (Test-Path $toolsPath) {
    $env:Path = "$toolsDir$([System.IO.Path]::PathSeparator)$env:Path"
    return
  }

  throw "NBGV CLI not found. Install with: dotnet tool install -g nbgv"
}

function Update-OriginState {
  $fetchOutput = & git fetch --quiet origin main --tags 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "Could not refresh origin/main and release tags.`n$($fetchOutput | Out-String)"
  }
}

function Get-CurrentBaseVersion {
  $semver = (& nbgv get-version -v SemVer2 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0 -or -not $semver) {
    throw "Could not get the current version from NBGV."
  }

  return ($semver -split '[-+]')[0]
}

function Get-NextPrereleaseVersion {
  param(
    [Parameter(Mandatory = $true)][string]$BaseVersion,
    [Parameter(Mandatory = $true)][string]$Label
  )

  if ($BaseVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Base version '$BaseVersion' must use X.Y.Z format."
  }
  if ($Label -notmatch '^[0-9A-Za-z-]+$') {
    throw "Prerelease label '$Label' contains unsupported characters."
  }

  $pattern = "v$([regex]::Escape($BaseVersion))-$([regex]::Escape($Label)).*"
  $tags = @(& git tag --list $pattern | Where-Object { $_ })
  if ($LASTEXITCODE -ne 0) {
    throw "Could not enumerate release tags."
  }

  $max = 0
  foreach ($tag in $tags) {
    if ($tag -match "^v$([regex]::Escape($BaseVersion))-$([regex]::Escape($Label))\.(\d+)$") {
      $counter = [int]$Matches[1]
      if ($counter -gt $max) {
        $max = $counter
      }
    }
  }

  return "$BaseVersion-$Label.$($max + 1)"
}

function Get-RepositorySlug {
  $remoteUrl = (& git remote get-url origin 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) {
    throw "Could not read the origin remote URL."
  }

  if ($remoteUrl -notmatch 'github\.com[:/](?<slug>[^/:\s]+/[^/\s]+?)(?:\.git)?$') {
    throw "Could not parse a GitHub repository slug from origin URL '$remoteUrl'."
  }

  return $Matches['slug']
}

function Ensure-LocalMainMatchesOrigin {
  $currentBranch = (& git branch --show-current 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) {
    throw "Could not determine the current branch."
  }
  if ($currentBranch -ne 'main') {
    throw "Release finalization must run from local main. Current branch: $currentBranch"
  }

  $headSha = (& git rev-parse HEAD 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve local HEAD."
  }

  $originMainSha = (& git rev-parse origin/main 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve origin/main after fetching it."
  }

  if ($headSha -ne $originMainSha) {
    throw "Local main must exactly match origin/main before release finalization. Local: $headSha; origin/main: $originMainSha"
  }
}

function Get-ChangelogEntry {
  param(
    [Parameter(Mandatory = $true)][string]$ReleaseVersion
  )

  $changelogPath = Join-Path $repoRoot 'CHANGELOG.md'
  if (-not (Test-Path $changelogPath)) {
    throw "CHANGELOG.md was not found."
  }

  $content = Get-Content $changelogPath -Raw
  if ($content -match "(?ms)^## \[$([regex]::Escape($ReleaseVersion))\].*?(?=^## \[|\z)") {
    return $Matches[0].Trim()
  }

  throw "CHANGELOG.md does not contain an exact '## [$ReleaseVersion]' release section."
}

function Ensure-VersionPrepared {
  param(
    [Parameter(Mandatory = $true)][string]$ReleaseVersion
  )

  if ($ReleaseVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Version '$ReleaseVersion' is not a supported semantic version without a leading v."
  }

  $versionJsonPath = Join-Path $repoRoot 'version.json'
  $versionJson = Get-Content $versionJsonPath -Raw | ConvertFrom-Json
  $declaredVersion = [string]$versionJson.version
  if ($declaredVersion -ne $ReleaseVersion) {
    throw "version.json contains '$declaredVersion', not '$ReleaseVersion'. Prepare and merge the version change through a pull request first."
  }

  $nbgvVersion = (& nbgv get-version -v SemVer2 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0 -or -not $nbgvVersion) {
    throw "Could not resolve the semantic version from NBGV."
  }
  if ($nbgvVersion -ne $ReleaseVersion) {
    throw "NBGV resolves '$nbgvVersion', not '$ReleaseVersion'. The release tag must point at the exact merged version-preparation commit."
  }
}

function Ensure-TagAvailable {
  param(
    [Parameter(Mandatory = $true)][string]$ReleaseVersion
  )

  $tagName = "v$ReleaseVersion"
  $localTag = @(& git tag --list $tagName | Where-Object { $_ })
  if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect local tags."
  }
  if ($localTag.Count -gt 0) {
    throw "Local tag '$tagName' already exists. Never reuse a release version."
  }

  $remoteTag = @(& git ls-remote --tags origin "refs/tags/$tagName" 2>&1 | Where-Object { $_ })
  if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect origin for tag '$tagName'."
  }
  if ($remoteTag.Count -gt 0) {
    throw "Remote tag '$tagName' already exists. Never reuse a release version."
  }
}

function Ensure-SuccessfulMainCi {
  $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
  if (-not $ghCommand) {
    throw "gh CLI not found. Install and authenticate gh before releasing."
  }

  $repoSlug = Get-RepositorySlug
  $sha = (& git rev-parse HEAD 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve HEAD for the CI gate."
  }

  Write-Host "Checking successful main CI for $repoSlug @ $sha ..." -ForegroundColor Cyan
  $uri = "repos/$repoSlug/actions/workflows/ci.yml/runs"
  $runsJson = & gh api --method GET $uri -f branch=main -f event=push -f "head_sha=$sha" -F per_page=100 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "Could not retrieve main CI workflow runs.`n$($runsJson | Out-String)"
  }

  try {
    $response = ($runsJson | Out-String) | ConvertFrom-Json
  } catch {
    throw "GitHub returned invalid workflow-run JSON: $($_.Exception.Message)"
  }

  $run = @($response.workflow_runs) |
    Where-Object {
      $_.head_sha -eq $sha -and
      $_.head_branch -eq 'main' -and
      $_.event -eq 'push'
    } |
    Sort-Object { [datetime]$_.created_at } -Descending |
    Select-Object -First 1

  if (-not $run) {
    throw "No ci.yml push run exists for main commit $sha."
  }
  if ($run.status -ne 'completed' -or $run.conclusion -ne 'success') {
    throw "Main CI must complete successfully before release finalization. Status: $($run.status); conclusion: $($run.conclusion); run: $($run.html_url)"
  }

  Write-Host "Same-commit main CI gate passed: $($run.html_url)" -ForegroundColor Green
}

function Get-UnshippedAnalyzerRules {
  param(
    [Parameter(Mandatory = $true)][string]$SrcDir
  )

  $unshippedFiles = Get-ChildItem -Path $SrcDir -Filter 'AnalyzerReleases.Unshipped.md' -Recurse -File
  $filesWithRules = @()

  foreach ($file in $unshippedFiles) {
    $ruleLines = @(Get-Content -Path $file.FullName | Where-Object { $_ -match '^NDLR' })
    if ($ruleLines.Count -eq 0) {
      continue
    }

    $relativePath = $file.FullName.Replace((Resolve-Path "$SrcDir\..").Path, '').TrimStart('\', '/')
    $filesWithRules += [PSCustomObject]@{
      Path = $relativePath
      Rules = $ruleLines
    }
  }

  return ,$filesWithRules
}

function Ensure-AnalyzerReleaseTracking {
  param(
    [Parameter(Mandatory = $true)][string]$ReleaseVersion
  )

  Write-Host "Checking analyzer release tracking..." -ForegroundColor Cyan
  $srcDir = Join-Path $repoRoot 'src'
  $unshippedAnalyzers = Get-UnshippedAnalyzerRules -SrcDir $srcDir
  if ($unshippedAnalyzers.Count -eq 0) {
    Write-Host "Analyzer release tracking gate passed." -ForegroundColor Green
    return
  }

  $baseVersion = ($ReleaseVersion -split '[-+]')[0]
  Write-Host "BLOCKED: analyzer projects have unshipped rules." -ForegroundColor Red
  Write-Host ""
  Write-Host "Move each rule below into the matching AnalyzerReleases.Shipped.md" -ForegroundColor Yellow
  Write-Host "under the base-version header before opening the release-preparation PR:" -ForegroundColor Yellow
  Write-Host "  ## Release $baseVersion" -ForegroundColor Yellow
  Write-Host ""
  Write-Host "Pending unshipped rules:" -ForegroundColor Red
  foreach ($entry in $unshippedAnalyzers) {
    Write-Host ""
    Write-Host "  $($entry.Path):" -ForegroundColor Cyan
    foreach ($rule in $entry.Rules) {
      $id = ($rule -split '\s*\|\s*')[0]
      Write-Host "    - $id" -ForegroundColor Yellow
    }
  }
  Write-Host ""

  throw "Fix unshipped analyzer rules in the release-preparation pull request. See docs/releasing.md."
}

if ($Version -and $Prerelease) {
  throw "Specify either Version or Prerelease, not both."
}
if ($Base -and -not $Prerelease) {
  throw "Base can only be used with Prerelease."
}

Ensure-CleanRepo
Ensure-Nbgv
Update-OriginState

if (-not $Version) {
  if (-not $Prerelease) {
    throw "Usage: release.ps1 <version> OR release.ps1 -Prerelease <label> [-Base <X.Y.Z>] [-DryRun]"
  }

  $baseVersion = if ($Base) { $Base } else { Get-CurrentBaseVersion }
  $Version = Get-NextPrereleaseVersion -BaseVersion $baseVersion -Label $Prerelease
}

Write-Host "Finalizing release for version: $Version"

Ensure-VersionPrepared -ReleaseVersion $Version
$changelogEntry = Get-ChangelogEntry -ReleaseVersion $Version
Ensure-AnalyzerReleaseTracking -ReleaseVersion $Version
Ensure-TagAvailable -ReleaseVersion $Version

if ($DryRun) {
  Write-Host ""
  Write-Host "=== CHANGELOG ENTRY ===" -ForegroundColor Cyan
  Write-Host $changelogEntry -ForegroundColor Green
  Write-Host ""
  Write-Host "=== ACTIONS ===" -ForegroundColor Cyan
  Write-Host "[DRY RUN] A real run would additionally require:"
  Write-Host "  local main exactly matching origin/main"
  Write-Host "  successful ci.yml push CI for that exact commit"
  Write-Host "  successful solution pack and package-content validation"
  Write-Host "[DRY RUN] It would then run:"
  Write-Host "  nbgv tag"
  Write-Host "  git push origin refs/tags/v$Version"
  Write-Host "[DRY RUN] No branch commit or branch push will be performed."
  exit 0
}

Ensure-LocalMainMatchesOrigin
Ensure-SuccessfulMainCi

Write-Host "Validating build and pack (solution-level, parallel)..." -ForegroundColor Cyan
$slnx = Join-Path (Join-Path $repoRoot 'src') 'NexusLabs.Needlr.slnx'
$packResult = & dotnet pack $slnx -c Release -v q 2>&1
if ($LASTEXITCODE -ne 0) {
  Write-Host "  Pack FAILED" -ForegroundColor Red
  Write-Host ($packResult | Select-Object -Last 30 | Out-String)
  throw "Solution-level pack failed. Fix errors before releasing."
}
Write-Host "Build and pack validation passed." -ForegroundColor Green

Write-Host "Validating package contents (nuspec assertions)..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'test-packages.ps1') -NoBuild
if ($LASTEXITCODE -ne 0) {
  throw "Package content validation failed. Fix the packaging issue before releasing."
}
Write-Host "Package content validation passed." -ForegroundColor Green

Ensure-CleanRepo
Update-OriginState
Ensure-LocalMainMatchesOrigin
Ensure-TagAvailable -ReleaseVersion $Version

& nbgv tag
if ($LASTEXITCODE -ne 0) {
  throw "nbgv tag failed. No remote changes were made."
}

$tagName = "v$Version"
$headSha = (& git rev-parse HEAD 2>&1 | Out-String).Trim()
$tagSha = (& git rev-parse "refs/tags/$tagName^{commit}" 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $tagSha -ne $headSha) {
  throw "Local tag '$tagName' does not resolve to HEAD. Inspect and remove the local tag before retrying."
}

git push origin "refs/tags/$tagName"
if ($LASTEXITCODE -ne 0) {
  throw "Tag push failed for $tagName. No branch commit was pushed. Inspect the local tag before retrying."
}

Write-Host "Tag $tagName pushed. release.yml will revalidate same-commit main CI before publishing." -ForegroundColor Green
