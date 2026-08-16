<#
.SYNOPSIS
    Classifies changed files for Needlr mutation testing.

.DESCRIPTION
    Mutation tooling and shared build changes run both scopes. Runtime and generator
    source or test changes run only their corresponding scope. Manual and scheduled
    workflows force both scopes.
#>
param(
    [string]$BaseSha,
    [string]$HeadSha,
    [string[]]$ChangedPaths,
    [switch]$ForceAll,
    [switch]$NoCiOutput
)

$ErrorActionPreference = 'Stop'

$sharedPatterns = @(
    '^\.config/dotnet-tools\.json$',
    '^\.github/workflows/mutation-testing\.yml$',
    '^global\.json$',
    '^scripts/get-mutation-scope\.ps1$',
    '^scripts/run-mutation-tests\.ps1$',
    '^scripts/test-mutation\.ps1$',
    '^scripts/mutation/',
    '^src/Directory\.Build\.props$',
    '^src/Directory\.Packages\.props$'
)
$runtimePatterns = @(
    '^src/NexusLabs\.Needlr/',
    '^src/NexusLabs\.Needlr\.Tests/'
)
$sourceGenPatterns = @(
    '^src/NexusLabs\.Needlr\.Carter/',
    '^src/NexusLabs\.Needlr\.Carter\.Tests/',
    '^src/NexusLabs\.Needlr\.Generators/',
    '^src/NexusLabs\.Needlr\.Generators\.Attributes/',
    '^src/NexusLabs\.Needlr\.Generators\.Tests/'
)

function Test-AnyPattern {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$Patterns
    )

    return $null -ne (
        $Patterns |
            Where-Object { $Path -match $_ } |
            Select-Object -First 1)
}

if ($ForceAll -or $BaseSha -match '^0+$') {
    $runtimeRequired = $true
    $sourceGenRequired = $true
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
    $sharedChanged = $null -ne (
        $normalizedPaths |
            Where-Object { Test-AnyPattern -Path $_ -Patterns $sharedPatterns } |
            Select-Object -First 1)
    $runtimeRequired = $sharedChanged -or $null -ne (
        $normalizedPaths |
            Where-Object { Test-AnyPattern -Path $_ -Patterns $runtimePatterns } |
            Select-Object -First 1)
    $sourceGenRequired = $sharedChanged -or $null -ne (
        $normalizedPaths |
            Where-Object { Test-AnyPattern -Path $_ -Patterns $sourceGenPatterns } |
            Select-Object -First 1)
}

$result = [ordered]@{
    runtime_required = $runtimeRequired
    sourcegen_required = $sourceGenRequired
    changed_count = $paths.Count
}

if (-not $NoCiOutput -and
    -not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    Add-Content `
        -Path $env:GITHUB_OUTPUT `
        -Value "runtime_required=$($runtimeRequired.ToString().ToLowerInvariant())"
    Add-Content `
        -Path $env:GITHUB_OUTPUT `
        -Value "sourcegen_required=$($sourceGenRequired.ToString().ToLowerInvariant())"
}

$result | ConvertTo-Json -Compress
