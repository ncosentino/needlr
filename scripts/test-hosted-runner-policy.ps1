<#
.SYNOPSIS
    Validates Needlr's GitHub-hosted-only workflow policy.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$deliveryPath = Join-Path $repoRoot '.github\genesis-delivery.json'
$githubActionsDocumentationPath = Join-Path $repoRoot 'docs\github-actions.md'
$globalJsonPath = Join-Path $repoRoot 'global.json'
$setupActionPath = Join-Path $repoRoot '.github\actions\setup-dotnet\action.yml'
$workflowDirectory = Join-Path $repoRoot '.github\workflows'
$standardRunnerDocumentationUrl =
    'https://docs.github.com/en/actions/reference/runners/github-hosted-runners#standard-github-hosted-runners-for-public-repositories'
$billingDocumentationUrl =
    'https://docs.github.com/en/billing/concepts/product-billing/github-actions#free-use-of-github-actions'

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

foreach ($path in @(
        '.pitcrew\runner-profile.json',
        '.github\runner-images\needlr-ci\Dockerfile',
        '.github\workflows\runner-image.yml',
        'scripts\test-runner-image.ps1',
        'scripts\test-runner-profile.ps1')) {
    Assert-Condition `
        -Condition (-not (Test-Path -LiteralPath (Join-Path $repoRoot $path))) `
        -Message "Removed self-hosted runner surface still exists: $path"
}

$globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
Assert-Condition `
    -Condition ([string]$globalJson.sdk.version -match '^\d+\.\d+\.\d+$') `
    -Message 'global.json must pin an exact stable .NET SDK.'
Assert-Condition `
    -Condition ([string]$globalJson.sdk.rollForward -ceq 'disable') `
    -Message 'global.json must disable SDK roll-forward.'
Assert-Condition `
    -Condition (-not [bool]$globalJson.sdk.allowPrerelease) `
    -Message 'global.json must reject prerelease SDKs.'

$setupAction = Get-Content -LiteralPath $setupActionPath -Raw
Assert-Condition `
    -Condition ($setupAction -match 'dotnet --list-sdks') `
    -Message 'The setup action must inspect installed SDKs.'
Assert-Condition `
    -Condition ($setupAction -match "setup-required == 'true'") `
    -Message 'The setup action must install only when the exact SDK is absent.'
Assert-Condition `
    -Condition ($setupAction -match 'uses: actions/setup-dotnet@v4') `
    -Message 'The setup action must retain the hosted SDK installer.'
Assert-Condition `
    -Condition ($setupAction -match 'DOTNET_INSTALL_DIR') `
    -Message 'Hosted SDK installation must use a writable directory.'

$delivery = Get-Content -LiteralPath $deliveryPath -Raw | ConvertFrom-Json
$deliveryProperties = @($delivery.PSObject.Properties.Name)
Assert-Condition `
    -Condition ([int]$delivery.schemaVersion -eq 2) `
    -Message 'The delivery contract must use the hosted-runner schema.'
Assert-Condition `
    -Condition ([string]$delivery.runnerPolicy.provider -ceq 'github-hosted') `
    -Message 'The delivery contract must select GitHub-hosted runners.'
Assert-Condition `
    -Condition ($deliveryProperties -notcontains 'runnerProfiles') `
    -Message 'The delivery contract must not declare self-hosted runner profiles.'
Assert-Condition `
    -Condition (
        @($delivery.draftValidation.PSObject.Properties.Name) -notcontains
            'pitcrewDefault') `
    -Message 'Draft validation must not contain a PitCrew-specific mode.'

$allowedLabels = @(
    $delivery.runnerPolicy.labels |
        ForEach-Object { [string]$_ })
Assert-Condition `
    -Condition (
        $allowedLabels.Count -gt 0 -and
        ($allowedLabels | Sort-Object -Unique).Count -eq $allowedLabels.Count) `
    -Message 'The hosted runner label allowlist must be non-empty and unique.'

$usedLabels = [System.Collections.Generic.List[string]]::new()
$workflowPaths = @(
    Get-ChildItem -LiteralPath $workflowDirectory -File |
        Where-Object { $_.Extension -in @('.yml', '.yaml') })
$allWorkflowText = [System.Text.StringBuilder]::new()
foreach ($workflowPath in $workflowPaths) {
    $workflow = Get-Content -LiteralPath $workflowPath.FullName -Raw
    $workflowLines = @(Get-Content -LiteralPath $workflowPath.FullName)
    [void]$allWorkflowText.AppendLine($workflow)
    Assert-Condition `
        -Condition (
            $workflow -notmatch
                '(?i)self-hosted|vars\.CI_RUNNER|needlr-ci|pitcrew') `
        -Message "$($workflowPath.Name) contains self-hosted runner routing."

    $runnerMatches = [regex]::Matches(
        $workflow,
        '(?m)^\s*runs-on:\s*([^\r\n#]+?)\s*$')
    Assert-Condition `
        -Condition ($runnerMatches.Count -gt 0) `
        -Message "$($workflowPath.Name) does not declare a runner."

    foreach ($runnerMatch in $runnerMatches) {
        $runnerLabel = $runnerMatch.Groups[1].Value.Trim().Trim("'`"")
        Assert-Condition `
            -Condition ($runnerLabel -notmatch '\$\{\{') `
            -Message "$($workflowPath.Name) uses dynamic runner routing."
        Assert-Condition `
            -Condition ($runnerLabel -in $allowedLabels) `
            -Message "$($workflowPath.Name) uses undeclared runner '$runnerLabel'."
        $usedLabels.Add($runnerLabel)
    }

    for ($lineIndex = 0; $lineIndex -lt $workflowLines.Count; $lineIndex++) {
        $uploadMatch = [regex]::Match(
            $workflowLines[$lineIndex],
            '^(\s*)uses:\s*actions/upload-artifact@v4\s*$')
        if (-not $uploadMatch.Success) {
            continue
        }

        $usesIndent = $uploadMatch.Groups[1].Value.Length
        $blockEnd = $workflowLines.Count
        for ($candidateIndex = $lineIndex + 1;
            $candidateIndex -lt $workflowLines.Count;
            $candidateIndex++) {
            $candidateLine = $workflowLines[$candidateIndex]
            if ([string]::IsNullOrWhiteSpace($candidateLine)) {
                continue
            }

            $candidateIndent =
                [regex]::Match($candidateLine, '^(\s*)').Groups[1].Value.Length
            if ($candidateIndent -lt $usesIndent) {
                $blockEnd = $candidateIndex
                break
            }
        }

        $uploadBlock = $workflowLines[$lineIndex..($blockEnd - 1)] -join "`n"
        Assert-Condition `
            -Condition (
                $uploadBlock -match
                    'retention-days:\s*(?:1|\$\{\{\s*env\.RELEASE_ARTIFACT_RETENTION_DAYS\s*\}\})') `
            -Message "$($workflowPath.Name) has an artifact without one-day retention."
    }

    if ($workflowPath.Name -ceq 'release.yml') {
        Assert-Condition `
            -Condition (
                $workflow -match
                    '(?m)^  RELEASE_ARTIFACT_RETENTION_DAYS:\s*1\r?$') `
            -Message 'Release artifacts must retain for one day.'
    }

    if ($workflow -match '(?m)^\s+runs-on:\s*windows-latest\r?$') {
        Assert-Condition `
            -Condition (
                $workflow -match [regex]::Escape($standardRunnerDocumentationUrl) -and
                $workflow -match
                    '(?i)free[\s#]+and[\s#]+unlimited[\s#]+for[\s#]+public[\s#]+repositories') `
            -Message "$($workflowPath.Name) must document why windows-latest is free for this public repository."
    }
}

$workflowText = $allWorkflowText.ToString()
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
    -Message 'Workflows must use the repository setup action.'
Assert-Condition `
    -Condition ($workflowText -notmatch '(?m)^\s+dotnet-version:') `
    -Message 'Workflows must not bypass global.json with dotnet-version.'
Assert-Condition `
    -Condition (
        $localSetupCount -gt 0 -and
        $localSetupCount -eq $globalJsonCount) `
    -Message 'Every workflow .NET setup must select an exact global.json.'

$ciWorkflow = Get-Content `
    -LiteralPath (Join-Path $workflowDirectory 'ci.yml') `
    -Raw
Assert-Condition `
    -Condition (
        ([regex]::Matches($ciWorkflow, '- name: Install AOT dependencies')).Count -eq 2 -and
        $ciWorkflow -match 'clang zlib1g-dev file') `
    -Message 'Both Native AOT jobs must install their hosted Linux prerequisites.'

$githubActionsDocumentation = Get-Content `
    -LiteralPath $githubActionsDocumentationPath `
    -Raw
Assert-Condition `
    -Condition (
        $githubActionsDocumentation -match 'windows-latest' -and
        $githubActionsDocumentation -match
            [regex]::Escape($standardRunnerDocumentationUrl) -and
        $githubActionsDocumentation -match
            [regex]::Escape($billingDocumentationUrl) -and
        $githubActionsDocumentation -match
            '(?i)free\s+and\s+unlimited\s+on\s+public\s+repositories') `
    -Message 'GitHub Actions documentation must preserve Windows free-usage evidence.'

foreach ($allowedLabel in $allowedLabels) {
    Assert-Condition `
        -Condition ($allowedLabel -in $usedLabels) `
        -Message "Declared hosted runner '$allowedLabel' is unused."
}

Write-Host 'GitHub-hosted runner policy validation passed.' -ForegroundColor Green
