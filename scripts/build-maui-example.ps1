#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the .NET MAUI example head (NeedlrMauiExampleApp).

.DESCRIPTION
    This example is intentionally NOT part of NexusLabs.Needlr.slnx and is excluded from the regular
    CI build, because a MAUI head requires the MAUI workload and platform target frameworks that the
    workload-free library build does not. Building it is a regression guard: it exercises Needlr's
    source generator on a real MAUI head (the per-platform application-entry exclusion) and the
    NexusLabs.Needlr.Maui integration end to end.

    Prerequisites: the .NET MAUI workload, e.g.
        dotnet workload install maui-android maui-windows

.PARAMETER Configuration
    Build configuration (Debug or Release). Defaults to Debug.

.PARAMETER Frameworks
    Target frameworks to build. Defaults to Windows + Android. The Windows TFM only builds on Windows.

.EXAMPLE
    ./scripts/build-maui-example.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [string[]]$Frameworks = @('net10.0-windows10.0.19041.0', 'net10.0-android')
)

$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot '..' 'src' 'Examples' 'Maui' 'NeedlrMauiExampleApp' 'NeedlrMauiExampleApp.csproj'

foreach ($tfm in $Frameworks) {
    Write-Host "Building $tfm ($Configuration)..." -ForegroundColor Cyan
    dotnet build $proj --configuration $Configuration --framework $tfm
    if ($LASTEXITCODE -ne 0) {
        throw "MAUI example build failed for $tfm (exit $LASTEXITCODE)."
    }
}

Write-Host "MAUI example built successfully for: $($Frameworks -join ', ')" -ForegroundColor Green
