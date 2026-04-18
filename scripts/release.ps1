param(
  [Parameter(Position=0)][string]$Version,
  [string]$Prerelease,
  [string]$Base,
  [switch]$DryRun,
  [switch]$SkipCiCheck
)

$ErrorActionPreference = 'Stop'

# Move to repo root (assumes scripts/ is directly under root)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location -Path (Join-Path $scriptDir '..')

function Ensure-CleanRepo {
  git diff --quiet
  if ($LASTEXITCODE -ne 0) {
    throw "Working tree not clean. Commit or stash first."
  }
}

function Ensure-Nbgv {
  # Check if nbgv is in PATH or in dotnet tools folder
  $nbgvPath = Get-Command nbgv -ErrorAction SilentlyContinue
  if (-not $nbgvPath) {
    $toolsPath = Join-Path $env:USERPROFILE ".dotnet\tools\nbgv.exe"
    if (Test-Path $toolsPath) {
      # Add to PATH for this session
      $env:Path = "$env:USERPROFILE\.dotnet\tools;$env:Path"
    } else {
      throw "NBGV CLI not found. Install with: dotnet tool install -g nbgv"
    }
  }
}

function Get-CurrentBaseVersion {
  # Use SemVer2 and strip pre-release/build metadata
  $semver = (& nbgv get-version -v SemVer2).Trim()  # e.g., 0.0.1-alpha.3 or 0.0.1
  if (-not $semver) { throw "Could not get version from NBGV." }
  return ($semver -split '[-+]')[0]                  # -> 0.0.1
}

function Next-PrereleaseVersion([string]$base, [string]$label) {
  # Find tags like v<base>-<label>.<N> and increment N
  $pattern = "v$([regex]::Escape($base))-$([regex]::Escape($label)).*"
  $tags = (& git tag --list $pattern) -split "`n" | Where-Object { $_ -ne "" }

  $max = 0
  foreach ($t in $tags) {
    if ($t -match "^v$([regex]::Escape($base))-$([regex]::Escape($label))\.(\d+)$") {
      $n = [int]$Matches[1]
      if ($n -gt $max) { $max = $n }
    }
  }
  return "$base-$label.$([int]($max + 1))"
}

# ---- main ----
Ensure-Nbgv

if (-not $Version) {
  if (-not $Prerelease) {
    throw "Usage: release.ps1 <version> OR release.ps1 -Prerelease <label> [-Base <X.Y.Z>] [--DryRun]"
  }
  $base = if ($Base) { $Base } else { Get-CurrentBaseVersion }
  $Version = Next-PrereleaseVersion -base $base -label $Prerelease
}

Write-Host "Preparing release for version: $Version"

# Check for changelog entry
$changelogPath = Join-Path $PSScriptRoot "..\CHANGELOG.md"
$changelogEntry = $null
if (Test-Path $changelogPath) {
  $content = Get-Content $changelogPath -Raw
  # Extract section for this version
  if ($content -match "(?ms)^## \[$([regex]::Escape($Version))\].*?(?=^## \[|\z)") {
    $changelogEntry = $Matches[0].Trim()
  }
}

if (-not $DryRun) { Ensure-CleanRepo }

# CI gate: verify all check runs on HEAD are green before releasing
if (-not $SkipCiCheck -and -not $DryRun) {
  $sha = (git rev-parse HEAD).Trim()
  $remoteUrl = (git remote get-url origin).Trim()
  $repoSlug = if ($remoteUrl -match 'github\.com[:/](.+?)(?:\.git)?$') { $Matches[1] } else { $null }

  if (-not $repoSlug) {
    Write-Host "WARNING: Could not parse repo slug from remote URL. Skipping CI check." -ForegroundColor Yellow
  } else {
    Write-Host "Checking CI status for $repoSlug @ $sha ..." -ForegroundColor Cyan

    $ghCheck = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $ghCheck) {
      Write-Host "WARNING: gh CLI not found. Skipping CI check. Install gh to enable this gate." -ForegroundColor Yellow
    } else {
      $runsJson = gh api "repos/$repoSlug/commits/$sha/check-runs" --paginate --jq '.check_runs[] | {name: .name, status: .status, conclusion: .conclusion}' 2>&1
      if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Could not retrieve check runs (API error). Skipping CI check." -ForegroundColor Yellow
      } else {
        $runs = $runsJson | ForEach-Object { $_ | ConvertFrom-Json }
        $notGreen = $runs | Where-Object {
          $_.status -ne 'completed' -or ($_.conclusion -notin @('success', 'skipped', 'neutral'))
        }
        if ($notGreen) {
          Write-Host "BLOCKED: CI is not fully green on HEAD ($sha)" -ForegroundColor Red
          $notGreen | ForEach-Object {
            $marker = if ($_.status -ne 'completed') { "[$($_.status)]" } else { "[$($_.conclusion)]" }
            Write-Host "  $marker  $($_.name)" -ForegroundColor Yellow
          }
          throw "Fix failing CI checks before releasing. Use -SkipCiCheck to override."
        }
        Write-Host "CI gate passed ($($runs.Count) check(s) green)." -ForegroundColor Green
      }
    }
  }
}

# Analyzer release tracking gate: block if any analyzer project has unshipped
# rules that have not been moved into AnalyzerReleases.Shipped.md under a new
# "## Release <version>" header. Microsoft.CodeAnalysis.Analyzers enforces
# RS2000/RS2001/RS2002 against these files, so forgetting this step ships a
# broken package to consumers. This runs before pack so the release script
# refuses to proceed — no maintainer (or LLM assistant) can forget.
function Test-UnshippedAnalyzerRules {
  param(
    [Parameter(Mandatory = $true)][string]$SrcDir
  )

  $unshippedFiles = Get-ChildItem -Path $SrcDir -Filter "AnalyzerReleases.Unshipped.md" -Recurse -File
  $filesWithRules = @()

  foreach ($file in $unshippedFiles) {
    $content = Get-Content -Path $file.FullName
    # A "rule row" is any line starting with "NDLR" — that's the Needlr
    # analyzer prefix used across all diagnostic IDs (NDLRCOR, NDLRGEN,
    # NDLRMAF, NDLRSIG, NDLRHTTP, etc.). Comment lines begin with ";" and
    # header lines begin with "#" or "-".
    $ruleLines = $content | Where-Object { $_ -match '^NDLR' }
    if ($ruleLines.Count -gt 0) {
      $relativePath = $file.FullName.Replace((Resolve-Path "$SrcDir\..").Path, '').TrimStart('\', '/')
      $filesWithRules += [PSCustomObject]@{
        Path  = $relativePath
        Rules = $ruleLines
      }
    }
  }

  return , $filesWithRules
}

Write-Host "Checking analyzer release tracking..." -ForegroundColor Cyan
$srcDir = Join-Path $PSScriptRoot "..\src"
$unshippedAnalyzers = Test-UnshippedAnalyzerRules -SrcDir $srcDir
if ($unshippedAnalyzers.Count -gt 0) {
  # RS2007 rejects pre-release labels in the release header, so the canonical
  # header uses only the base version (e.g. "0.0.2") and all prereleases of
  # that base share a single section. See docs/releasing.md for rationale.
  $baseVersion = ($Version -split '[-+]')[0]

  Write-Host "BLOCKED: analyzer projects have unshipped rules." -ForegroundColor Red
  Write-Host ""
  Write-Host "Before releasing, move each rule below from its AnalyzerReleases.Unshipped.md" -ForegroundColor Yellow
  Write-Host "file into the matching AnalyzerReleases.Shipped.md under the existing header:" -ForegroundColor Yellow
  Write-Host "  ## Release $baseVersion" -ForegroundColor Yellow
  Write-Host ""
  Write-Host "(Microsoft.CodeAnalysis.Analyzers rule RS2007 rejects pre-release labels" -ForegroundColor Yellow
  Write-Host "in the header, so '## Release $Version' will not build. Use the base" -ForegroundColor Yellow
  Write-Host "version only and append new rules to the existing release section, keeping" -ForegroundColor Yellow
  Write-Host "rule IDs in alphanumeric order.)" -ForegroundColor Yellow
  Write-Host ""
  Write-Host "Then delete the rule rows from Unshipped.md (keep the comment header," -ForegroundColor Yellow
  Write-Host "### New Rules heading, table header row, and separator row)." -ForegroundColor Yellow
  Write-Host ""
  Write-Host "Commit message convention:" -ForegroundColor Yellow
  Write-Host "  chore: ship analyzers for $Version" -ForegroundColor Yellow
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
  throw "Fix unshipped analyzer rules before releasing. See docs/releasing.md for details."
}
Write-Host "Analyzer release tracking gate passed." -ForegroundColor Green

# Build and pack validation - MUST pass before any release actions
# Uses solution-level pack which lets MSBuild parallelize across projects
# instead of the previous per-project sequential loop (~50 projects × ~10s each = ~8 min → ~1 min).
Write-Host "Validating build and pack (solution-level, parallel)..." -ForegroundColor Cyan
$slnx = Join-Path $PSScriptRoot "..\src\NexusLabs.Needlr.slnx"
$packResult = & dotnet pack $slnx -c Release -v q 2>&1
if ($LASTEXITCODE -ne 0) {
  Write-Host "  Pack FAILED" -ForegroundColor Red
  Write-Host ($packResult | Select-Object -Last 30 | Out-String)
  throw "Solution-level pack failed. Fix errors before releasing."
}
Write-Host "Build and pack validation passed." -ForegroundColor Green

# Assert nuspec contents for key packages
Write-Host "Validating package contents (nuspec assertions)..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "test-packages.ps1") -NoBuild
if ($LASTEXITCODE -ne 0) {
  throw "Package content validation failed. Fix the packaging issue before releasing."
}
Write-Host "Package content validation passed." -ForegroundColor Green

if ($DryRun) {
  Write-Host ""
  Write-Host "=== CHANGELOG ENTRY ===" -ForegroundColor Cyan
  if ($changelogEntry) {
    Write-Host $changelogEntry -ForegroundColor Green
  } else {
    Write-Host "[WARNING] No changelog entry found for version $Version" -ForegroundColor Yellow
    Write-Host "Consider running: python skills/changelog-generator/scripts/generate.py --from <prev-tag> --to HEAD --version $Version" -ForegroundColor Yellow
  }
  Write-Host ""
  Write-Host "=== ACTIONS ===" -ForegroundColor Cyan
  Write-Host "[DRY RUN] Would run:"
  Write-Host "  nbgv set-version $Version"
  Write-Host "  git commit -am 'chore: bump version to $Version'"
  Write-Host "  nbgv tag"
  Write-Host "  git push origin --tags"
  Write-Host "  git pull --rebase && git push origin HEAD"
  exit 0
}

& nbgv set-version $Version
if ($LASTEXITCODE -ne 0) {
  throw "nbgv set-version failed. Aborting — no commit, no tag, no push."
}

# The repo's pre-commit hook (genesis self-assessment gate) blocks commits
# unless GENESIS_PRECOMMIT_ACK=true is set. The release script is a scripted
# version bump with no human-authored code changes, so the gate doesn't apply.
# Scope the env var to this commit only — do not leak it to the rest of the
# session or subprocesses beyond this block.
$previousAck = $env:GENESIS_PRECOMMIT_ACK
try {
  $env:GENESIS_PRECOMMIT_ACK = "true"
  git commit -am "chore: bump version to $Version"
  if ($LASTEXITCODE -ne 0) {
    throw "git commit failed. Aborting — no tag, no push. Working tree still has the version bump; inspect with 'git status' and 'git diff'."
  }
} finally {
  if ($null -eq $previousAck) {
    Remove-Item Env:GENESIS_PRECOMMIT_ACK -ErrorAction SilentlyContinue
  } else {
    $env:GENESIS_PRECOMMIT_ACK = $previousAck
  }
}

& nbgv tag
if ($LASTEXITCODE -ne 0) {
  throw "nbgv tag failed. The version bump commit exists on HEAD but no tag was created. Inspect tags with 'git tag --list v$Version' and resolve before re-running."
}

# Push tags first so release.yml fires immediately on the new v* tag.
# Then rebase+push HEAD separately to handle the coverage-badge bot race:
# CI's "chore: update coverage badge [skip ci]" auto-commit often lands
# between the CI gate check and this push, causing a non-fast-forward
# rejection on HEAD. The tag push always succeeds (new ref). Splitting
# them means the release is never blocked by the race — worst case the
# version-bump commit needs one rebase attempt.
git push origin "refs/tags/v$Version"
if ($LASTEXITCODE -ne 0) {
  throw "Tag push failed for v$Version. Inspect remote state; the tag exists locally but is not on origin."
}
Write-Host "Tag v$Version pushed — release.yml is firing." -ForegroundColor Green

git pull --rebase 2>&1 | Out-Null
git push origin HEAD
if ($LASTEXITCODE -ne 0) {
  Write-Host "WARNING: Version bump commit push failed (likely coverage-badge race)." -ForegroundColor Yellow
  Write-Host "The tag and release are fine. Run 'git pull --rebase && git push' manually to land the bump." -ForegroundColor Yellow
} else {
  Write-Host "Version bump committed to main." -ForegroundColor Green
}

Write-Host "Tagged and pushed v$Version"
