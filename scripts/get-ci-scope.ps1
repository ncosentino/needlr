<#
.SYNOPSIS
    Classifies changed files for Needlr source and agent-marketplace CI.

.DESCRIPTION
    Marketplace-only changes skip the expensive .NET, package, and AOT paths.
    Any unrecognized path fails closed into source validation. Mixed changes
    require both validation paths.
#>
param(
    [string]$BaseSha,
    [string]$HeadSha,
    [string[]]$ChangedPaths,
    [switch]$ForceAll,
    [switch]$NoCiOutput
)

$ErrorActionPreference = 'Stop'

$marketplaceOnlyPatterns = @(
    '^\.claude-plugin/',
    '^agents/',
    '^evals/agents/',
    '^skills/needlr-research/',
    '^plugin\.json$',
    '^scripts/validate-agent-marketplace\.ps1$'
)

function Test-AnyPattern {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Patterns
    )

    return $null -ne ($Patterns | Where-Object { $Path -match $_ } | Select-Object -First 1)
}

if ($ForceAll -or $BaseSha -match '^0+$') {
    $sourceRequired = $true
    $marketplaceRequired = $true
    $paths = @()
} else {
    if ($null -eq $ChangedPaths) {
        if ([string]::IsNullOrWhiteSpace($BaseSha) -or
            [string]::IsNullOrWhiteSpace($HeadSha)) {
            throw 'BaseSha and HeadSha are required when ChangedPaths is not supplied.'
        }

        $diffOutput = & git diff --name-only "$BaseSha...$HeadSha" 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Could not determine changed files.`n$($diffOutput | Out-String)"
        }
        $paths = @($diffOutput | Where-Object { $_ })
    } else {
        $paths = @($ChangedPaths | Where-Object { $_ })
    }

    $normalizedPaths = @($paths | ForEach-Object { $_.Replace('\', '/') })
    if ($normalizedPaths.Count -eq 0) {
        $sourceRequired = $true
        $marketplaceRequired = $true
    } else {
        $marketplaceRequired = $null -ne (
            $normalizedPaths |
                Where-Object {
                    Test-AnyPattern -Path $_ -Patterns $marketplaceOnlyPatterns
                } |
                Select-Object -First 1)
        $sourceRequired = $null -ne (
            $normalizedPaths |
                Where-Object {
                    -not (Test-AnyPattern -Path $_ -Patterns $marketplaceOnlyPatterns)
                } |
                Select-Object -First 1)
    }
}

$result = [ordered]@{
    source_required = $sourceRequired
    marketplace_required = $marketplaceRequired
    changed_count = $paths.Count
}

if (-not $NoCiOutput -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "source_required=$($sourceRequired.ToString().ToLowerInvariant())"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "marketplace_required=$($marketplaceRequired.ToString().ToLowerInvariant())"
}

$result | ConvertTo-Json -Compress
