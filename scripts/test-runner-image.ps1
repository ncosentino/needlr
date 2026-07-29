<#
.SYNOPSIS
    Validates the repository-owned Needlr runner image contract.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$globalJsonPath = Join-Path $repoRoot 'global.json'
$dockerfilePath = Join-Path $repoRoot '.github\runner-images\needlr-ci\Dockerfile'
$workflowPath = Join-Path $repoRoot '.github\workflows\runner-image.yml'
$expectedSdkVersion = '10.0.302'

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

$globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
Assert-Condition `
    -Condition ([string]$globalJson.sdk.version -ceq $expectedSdkVersion) `
    -Message "global.json must pin .NET SDK $expectedSdkVersion."
Assert-Condition `
    -Condition ([string]$globalJson.sdk.rollForward -ceq 'disable') `
    -Message 'global.json must disable SDK roll-forward.'
Assert-Condition `
    -Condition (-not [bool]$globalJson.sdk.allowPrerelease) `
    -Message 'global.json must reject prerelease SDKs.'

$dockerfile = Get-Content -LiteralPath $dockerfilePath -Raw
$pinnedFromLines = @(
    $dockerfile -split '\r?\n' |
        Where-Object { $_ -match '^FROM .+@sha256:[0-9a-f]{64}(?:\s+AS\s+\w+)?$' })
Assert-Condition `
    -Condition ($pinnedFromLines.Count -eq 2) `
    -Message 'The runner Dockerfile must pin both base images by SHA-256 digest.'
Assert-Condition `
    -Condition ($dockerfile -match [regex]::Escape("dotnet/sdk:$expectedSdkVersion-noble@sha256:")) `
    -Message "The runner image must install the SDK pinned by global.json."
Assert-Condition `
    -Condition ($dockerfile -match '/actions-runner/bin/Runner\.Listener') `
    -Message 'The runner image must verify Runner.Listener.'
Assert-Condition `
    -Condition ($dockerfile -match 'clang' -and $dockerfile -match 'zlib1g-dev') `
    -Message 'The runner image must include Needlr Native AOT prerequisites.'
Assert-Condition `
    -Condition ($dockerfile -notmatch '(?im)^\s*(COPY|ADD)\s+\.\s') `
    -Message 'The runner image must not copy repository source into an image layer.'
Assert-Condition `
    -Condition ($dockerfile -notmatch '(?i)(api[_-]?key|access[_-]?token|password|credential|private[_-]?key)') `
    -Message 'The runner Dockerfile must not contain credential-bearing inputs.'

$workflow = Get-Content -LiteralPath $workflowPath -Raw
Assert-Condition `
    -Condition ($workflow -notmatch 'pull_request_target') `
    -Message 'Runner image validation must never use pull_request_target.'
Assert-Condition `
    -Condition ($workflow -match "(?m)^  pull_request:\r?$") `
    -Message 'Pull requests must validate the runner image.'
Assert-Condition `
    -Condition ($workflow -match "(?m)^  push:\r?$" -and $workflow -match "branches: \[main\]") `
    -Message 'Trusted main pushes must publish the runner image.'
Assert-Condition `
    -Condition ($workflow -match "github\.event_name == 'push'.*refs/heads/main") `
    -Message 'Image publication must be restricted to trusted main pushes.'
Assert-Condition `
    -Condition ($workflow -match '(?m)^\s+packages: write\r?$') `
    -Message 'The publication job must request packages: write.'
Assert-Condition `
    -Condition ($workflow -match 'ghcr\.io/ncosentino/needlr-runner') `
    -Message 'The publication workflow must target the Needlr GHCR package.'
Assert-Condition `
    -Condition ($workflow -match 'sha-\$\{\{ github\.sha \}\}') `
    -Message 'Published images must carry an immutable commit tag.'
Assert-Condition `
    -Condition ($workflow -match 'steps\.image\.outputs\.digest') `
    -Message 'The publication workflow must capture the registry manifest digest.'

$allWorkflowText = @(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot '.github\workflows') -Filter '*.yml' |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$setupDotnetCount = (
    [regex]::Matches(
        $allWorkflowText,
        'uses:\s*(?:actions/setup-dotnet@|\./\.github/actions/setup-dotnet)')).Count
$globalJsonCount = (
    [regex]::Matches(
        $allWorkflowText,
        'global-json-file:\s*(?:[A-Za-z0-9._/-]+/)?global\.json')).Count
Assert-Condition `
    -Condition ($allWorkflowText -notmatch '(?m)^\s+dotnet-version:') `
    -Message 'Workflows must not bypass the exact SDK pin with dotnet-version.'
Assert-Condition `
    -Condition ($setupDotnetCount -eq $globalJsonCount) `
    -Message 'Every .NET setup step must use an exact global.json contract.'

Write-Host 'Runner image contract validation passed.' -ForegroundColor Green
