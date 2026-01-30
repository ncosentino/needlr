param(
  [Parameter(Position=0)][string]$Version,
  [string]$Prerelease,
  [string]$Base,
  [switch]$DryRun
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

# Build and pack validation - MUST pass before any release actions
Write-Host "Validating build and pack..." -ForegroundColor Cyan

# Find all packable projects (those with IsPackable not explicitly false)
$srcDir = Join-Path $PSScriptRoot "..\src"
$projects = Get-ChildItem -Path $srcDir -Filter "*.csproj" -Recurse | Where-Object {
  $content = Get-Content $_.FullName -Raw
  # Exclude test projects and projects with IsPackable=false
  -not ($_.Name -match "\.Tests\.") -and
  -not ($content -match "<IsPackable>false</IsPackable>")
}

foreach ($proj in $projects) {
  Write-Host "  Packing $($proj.Name)..." -NoNewline
  $packResult = & dotnet pack $proj.FullName -c Release -v q --no-restore 2>&1
  if ($LASTEXITCODE -ne 0) {
    Write-Host " FAILED" -ForegroundColor Red
    Write-Host $packResult
    throw "Pack validation failed for $($proj.Name). Fix errors before releasing."
  }
  Write-Host " OK" -ForegroundColor Green
}
Write-Host "Build and pack validation passed." -ForegroundColor Green

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
  Write-Host "  git push origin HEAD --tags"
  exit 0
}

& nbgv set-version $Version | Out-Null
git commit -am "chore: bump version to $Version" | Out-Null
& nbgv tag | Out-Null
git push origin HEAD --tags

Write-Host "Tagged and pushed v$Version"
