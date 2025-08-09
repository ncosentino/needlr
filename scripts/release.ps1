param(
  [Parameter(Mandatory=$true)][string]$Version
)

# Move to repo root (assumes scripts/ is directly under root)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location -Path (Join-Path $scriptDir '..')

# Sanity checks
git diff --quiet; if ($LASTEXITCODE -ne 0) { throw "Working tree not clean. Commit or stash first." }
if (-not (Get-Command nbgv -ErrorAction SilentlyContinue)) { throw "Install NBGV: dotnet tool install -g nbgv" }

# Bump version, commit, tag, push
nbgv set-version $Version
git commit -am "chore: bump version to $Version"
nbgv tag
git push origin HEAD --tags
Write-Host "Tagged and pushed v$Version"
