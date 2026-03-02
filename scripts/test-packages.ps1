<#
.SYNOPSIS
    Validates the contents of key Needlr NuGet packages after packing.

.DESCRIPTION
    Packs the specified projects, extracts each resulting .nupkg, and asserts that
    the nuspec lists the correct transitive dependencies with the correct asset exclusions.

    Exits non-zero on any failure so this can be called from CI or release.ps1.

.PARAMETER NoBuild
    Skip the dotnet pack step (use existing bin/Release output).
#>
param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot  = Join-Path $scriptDir '..'
$failed    = $false

function Assert-NuspecDependency {
    param(
        [string]$NupkgPath,
        [string]$DependencyId,
        [string]$RequiredExclude,   # if set, the exclude attribute must CONTAIN this value
        [string]$ForbiddenExclude   # if set, the exclude attribute must NOT contain this value
    )

    $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "needlr-pkg-test-$([System.IO.Path]::GetRandomFileName())"
    Expand-Archive -Path $NupkgPath -DestinationPath $tmpDir -Force

    $nuspecPath = Get-ChildItem -Path $tmpDir -Filter '*.nuspec' -Recurse | Select-Object -First 1
    if (-not $nuspecPath) {
        Write-Host "  ERROR: No nuspec found in $NupkgPath" -ForegroundColor Red
        Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
        return $false
    }

    [xml]$nuspec = Get-Content $nuspecPath.FullName
    $ns = 'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd'
    $nsmgr = New-Object System.Xml.XmlNamespaceManager($nuspec.NameTable)
    $nsmgr.AddNamespace('ns', $ns)

    $dep = $nuspec.SelectSingleNode("//ns:dependency[@id='$DependencyId']", $nsmgr)

    Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue

    if (-not $dep) {
        Write-Host "  ERROR: dependency '$DependencyId' not found in nuspec" -ForegroundColor Red
        return $false
    }

    $exclude = $dep.GetAttribute('exclude')

    if ($RequiredExclude -and ($exclude -notmatch $RequiredExclude)) {
        Write-Host "  ERROR: dependency '$DependencyId' has exclude='$exclude' but expected it to contain '$RequiredExclude'" -ForegroundColor Red
        return $false
    }

    if ($ForbiddenExclude -and ($exclude -match $ForbiddenExclude)) {
        Write-Host "  ERROR: dependency '$DependencyId' has exclude='$exclude' but must NOT contain '$ForbiddenExclude' (analyzer delivery would be blocked)" -ForegroundColor Red
        return $false
    }

    return $true
}

$assertions = @(
    @{
        Project    = 'src/NexusLabs.Needlr.Build/NexusLabs.Needlr.Build.csproj'
        PackageName = 'NexusLabs.Needlr.Build'
        Checks     = @(
            @{
                Id              = 'NexusLabs.Needlr.Generators'
                RequiredExclude = 'Compile'    # compile/runtime excluded — DLL is an Analyzer, not a lib
                ForbiddenExclude = 'Analyzers' # must NOT exclude Analyzers — otherwise generator DLL never flows
                Description     = 'Generators must be a dependency with Analyzers NOT excluded'
            },
            @{
                Id              = 'NexusLabs.Needlr.Generators.Attributes'
                RequiredExclude = $null
                ForbiddenExclude = $null
                Description     = 'Generators.Attributes must be a dependency'
            }
        )
    }
)

foreach ($entry in $assertions) {
    $projPath = Join-Path $repoRoot $entry.Project
    $projPath = [System.IO.Path]::GetFullPath($projPath)
    $projDir  = Split-Path $projPath

    Write-Host ""
    Write-Host "=== $($entry.PackageName) ===" -ForegroundColor Cyan

    if (-not $NoBuild) {
        Write-Host "  Packing..." -NoNewline
        $packOut = & dotnet pack $projPath -c Release -v q 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host " FAILED" -ForegroundColor Red
            Write-Host $packOut
            $failed = $true
            continue
        }
        Write-Host " OK" -ForegroundColor Green
    }

    $nupkg = Get-ChildItem -Path (Join-Path $projDir 'bin/Release') -Filter '*.nupkg' |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

    if (-not $nupkg) {
        Write-Host "  ERROR: No .nupkg found in $(Join-Path $projDir 'bin/Release')" -ForegroundColor Red
        $failed = $true
        continue
    }

    Write-Host "  Package: $($nupkg.Name)"

    foreach ($check in $entry.Checks) {
        Write-Host "  Checking: $($check.Description)..." -NoNewline
        $ok = Assert-NuspecDependency `
            -NupkgPath       $nupkg.FullName `
            -DependencyId    $check.Id `
            -RequiredExclude $check.RequiredExclude `
            -ForbiddenExclude $check.ForbiddenExclude

        if ($ok) {
            Write-Host " OK" -ForegroundColor Green
        } else {
            $failed = $true
        }
    }
}

Write-Host ""
if ($failed) {
    Write-Host "Package validation FAILED." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Package validation passed." -ForegroundColor Green
    exit 0
}
