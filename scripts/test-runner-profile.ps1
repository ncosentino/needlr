<#
.SYNOPSIS
    Validates Needlr's digest-pinned PitCrew profile and portable SDK setup.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$profilePath = Join-Path $repoRoot '.pitcrew\runner-profile.json'
$actionPath = Join-Path $repoRoot '.github\actions\setup-dotnet\action.yml'
$deliveryPath = Join-Path $repoRoot '.github\genesis-delivery.json'
$expectedImage = 'ghcr.io/ncosentino/needlr-runner@sha256:233a06905fc35312fe73099d12e93bf496ac1b98dd19a0482be000c12d7b4461'

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
Assert-Condition `
    -Condition ([string]$profile.'$schema' -ceq 'https://raw.githubusercontent.com/ncosentino/pitcrew/87162e6fad6a961b9bc2f026639f2fa7df0795ba/runner-profile.schema.json') `
    -Message 'The PitCrew profile schema must be pinned to the reviewed upstream contract.'
Assert-Condition `
    -Condition ([string]$profile.name -ceq 'needlr-ci') `
    -Message "The PitCrew profile name must be 'needlr-ci'."
Assert-Condition `
    -Condition ([string]$profile.image -ceq $expectedImage) `
    -Message 'The PitCrew profile must pin the approved immutable GHCR digest.'
Assert-Condition `
    -Condition ([int]$profile.replicas -eq 1) `
    -Message 'The committed profile must preserve Needlr capacity at one worker.'
Assert-Condition `
    -Condition ([bool]$profile.pullImage) `
    -Message 'The external profile must pull the approved GHCR image.'
Assert-Condition `
    -Condition ([bool]$profile.disableDefaultLabels) `
    -Message 'The specialized profile must disable GitHub default labels.'
Assert-Condition `
    -Condition (@($profile.labels) -contains 'needlr') `
    -Message "The specialized profile must advertise the 'needlr' capability."
Assert-Condition `
    -Condition (@($profile.labels) -notcontains 'self-hosted') `
    -Message 'The specialized profile must not expose the broad self-hosted label.'

$verification = @($profile.verificationCommands) -join "`n"
Assert-Condition `
    -Condition ($verification -match '/actions-runner/bin/Runner\.Listener') `
    -Message 'The profile must verify Runner.Listener.'
Assert-Condition `
    -Condition ($verification -match '10\.0\.302') `
    -Message 'The profile must verify the exact Needlr SDK.'
Assert-Condition `
    -Condition ($verification -match 'clang --version') `
    -Message 'The profile must verify the Native AOT compiler.'

$action = Get-Content -LiteralPath $actionPath -Raw
Assert-Condition `
    -Condition ($action -match 'dotnet --list-sdks') `
    -Message 'The setup action must inspect installed SDKs.'
Assert-Condition `
    -Condition ($action -match "setup-required == 'true'") `
    -Message 'The setup action must install only when the exact SDK is absent.'
Assert-Condition `
    -Condition ($action -match 'uses: actions/setup-dotnet@v4') `
    -Message 'Hosted fallback must retain actions/setup-dotnet.'
Assert-Condition `
    -Condition ($action -match 'DOTNET_INSTALL_DIR') `
    -Message 'Fallback installation must use a writable directory.'

$workflowText = @(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot '.github\workflows') -Filter '*.yml' |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$localSetupCount = (
    [regex]::Matches(
        $workflowText,
        'uses:\s*\./\.github/actions/setup-dotnet')).Count
$globalJsonCount = (
    [regex]::Matches(
        $workflowText,
        'global-json-file:\s*(?:[A-Za-z0-9._/-]+/)?global\.json')).Count
Assert-Condition `
    -Condition ($workflowText -notmatch 'uses:\s*actions/setup-dotnet@') `
    -Message 'Workflows must use the conditional local setup action.'
Assert-Condition `
    -Condition ($localSetupCount -eq $globalJsonCount -and $localSetupCount -gt 0) `
    -Message 'Every local setup action usage must select an exact global.json.'
Assert-Condition `
    -Condition ($workflowText -notmatch 'DOTNET_INSTALL_DIR=\$RUNNER_TEMP') `
    -Message 'Workflows must not overwrite the preinstalled profile SDK path.'
Assert-Condition `
    -Condition ($workflowText -match 'head\.repo\.full_name != github\.repository') `
    -Message 'External fork routing must remain ahead of CI_RUNNER.'

$delivery = Get-Content -LiteralPath $deliveryPath -Raw | ConvertFrom-Json
$runnerProfile = @($delivery.runnerProfiles) |
    Where-Object { $_.id -eq 'needlr-ci' } |
    Select-Object -First 1
Assert-Condition `
    -Condition ($null -ne $runnerProfile) `
    -Message 'The Genesis delivery contract must declare the needlr-ci profile.'
Assert-Condition `
    -Condition ((@($runnerProfile.labels) -join ',') -ceq 'needlr-ci') `
    -Message 'The Genesis delivery contract must use the profile routing label.'

Write-Host 'Runner profile contract validation passed.' -ForegroundColor Green
