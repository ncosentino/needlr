<#
.SYNOPSIS
    Selects bounded mutation scopes and files for one pull request.

.DESCRIPTION
    Uses the committed scope manifest, changed paths, and changed-line counts to select
    at most the configured number of scopes and source files. Omitted work is reported
    explicitly so a bounded run is never mistaken for complete mutation coverage.
#>
param(
    [string]$BaseSha,
    [string]$HeadSha,
    [string[]]$ChangedPaths,
    [string]$ChangedFilesPath,
    [switch]$NoCiOutput
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $PSScriptRoot 'mutation' 'scopes.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$maxScopes = [int]$manifest.limits.maxScopesPerPullRequest
$maxFiles = [int]$manifest.limits.maxFilesPerScope

$sharedPatterns = @(
    '^\.config/dotnet-tools\.json$',
    '^\.github/genesis-delivery\.json$',
    '^\.github/workflows/mutation-testing\.yml$',
    '^global\.json$',
    '^scripts/get-mutation-scope\.ps1$',
    '^scripts/run-mutation-tests\.ps1$',
    '^scripts/test-mutation\.ps1$',
    '^scripts/mutation/',
    '^src/Directory\.Build\.props$',
    '^src/Directory\.Packages\.props$'
)

function Test-UnderRoot {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Root
    )

    return $Path -ceq $Root -or
        $Path.StartsWith("$Root/", [StringComparison]::Ordinal)
}

function Get-ChangedRecords {
    if (-not [string]::IsNullOrWhiteSpace($ChangedFilesPath)) {
        if (-not (Test-Path -LiteralPath $ChangedFilesPath -PathType Leaf)) {
            throw "Changed-files metadata was not found at '$ChangedFilesPath'."
        }

        $records = @(
            Get-Content -LiteralPath $ChangedFilesPath -Raw |
                ConvertFrom-Json)
        return @(
            foreach ($record in $records) {
                [PSCustomObject]@{
                    Path = ([string]$record.filename).Replace('\', '/')
                    ChangedLines = [int]$record.additions + [int]$record.deletions
                }
                if (-not [string]::IsNullOrWhiteSpace(
                    [string]$record.previous_filename)) {
                    [PSCustomObject]@{
                        Path = ([string]$record.previous_filename).Replace('\', '/')
                        ChangedLines = 0
                    }
                }
            })
    }

    if ($null -ne $ChangedPaths) {
        return @(
            $ChangedPaths |
                Where-Object { $_ } |
                ForEach-Object {
                    [PSCustomObject]@{
                        Path = $_.Replace('\', '/')
                        ChangedLines = 0
                    }
                })
    }

    if ([string]::IsNullOrWhiteSpace($BaseSha) -or
        [string]::IsNullOrWhiteSpace($HeadSha)) {
        throw 'BaseSha and HeadSha are required when ChangedPaths is not supplied.'
    }

    $diffOutput = & git diff --numstat --no-renames "$BaseSha...$HeadSha" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Could not determine changed files.`n$($diffOutput | Out-String)"
    }

    return @(
        foreach ($line in $diffOutput) {
            if ($line -notmatch '^(?<added>-|\d+)\t(?<deleted>-|\d+)\t(?<path>.+)$') {
                throw "Unexpected git numstat line: $line"
            }

            $added = if ($Matches.added -ceq '-') { 0 } else { [int]$Matches.added }
            $deleted = if ($Matches.deleted -ceq '-') { 0 } else { [int]$Matches.deleted }
            [PSCustomObject]@{
                Path = $Matches.path.Replace('\', '/')
                ChangedLines = $added + $deleted
            }
        })
}

$changed = @(Get-ChangedRecords)
$sharedChanged = $null -ne (
    $changed |
        Where-Object {
            $path = $_.Path
            $null -ne (
                $sharedPatterns |
                    Where-Object { $path -match $_ } |
                    Select-Object -First 1)
        } |
        Select-Object -First 1)

$candidates = @(
    foreach ($scope in $manifest.scopes) {
        $sourceRoot = [string]$scope.sourceRoot
        $sourceChanges = @(
            $changed |
                Where-Object { Test-UnderRoot -Path $_.Path -Root $sourceRoot })
        $testChanges = @(
            $changed |
                Where-Object {
                    $path = $_.Path
                    $null -ne (
                        @($scope.testRoots) |
                            Where-Object {
                                Test-UnderRoot -Path $path -Root ([string]$_)
                            } |
                            Select-Object -First 1)
                })
        $fullTriggerChanges = @(
            $changed |
                Where-Object {
                    $path = $_.Path
                    $null -ne (
                        @($scope.fullTriggerRoots) |
                            Where-Object {
                                Test-UnderRoot -Path $path -Root ([string]$_)
                            } |
                            Select-Object -First 1)
                })

        if (-not $sharedChanged -and
            $sourceChanges.Count -eq 0 -and
            $testChanges.Count -eq 0 -and
            $fullTriggerChanges.Count -eq 0) {
            continue
        }

        $mode = if ($sharedChanged -or $fullTriggerChanges.Count -gt 0) {
            'full'
        } else {
            'diff'
        }

        $mutableSourceChanges = @(
            $sourceChanges |
                Where-Object {
                    Test-Path `
                        -LiteralPath (Join-Path $repoRoot $_.Path) `
                        -PathType Leaf
                })
        $unavailableSourceChanges = @(
            $sourceChanges |
                Where-Object {
                    -not (Test-Path `
                        -LiteralPath (Join-Path $repoRoot $_.Path) `
                        -PathType Leaf)
                })
        $orderedSourceChanges = @(
            $mutableSourceChanges |
                Sort-Object `
                    @{ Expression = 'ChangedLines'; Descending = $true },
                    @{ Expression = 'Path'; Descending = $false })
        $selectedSourceChanges = @($orderedSourceChanges | Select-Object -First $maxFiles)
        $omittedSourceChanges = @($orderedSourceChanges | Select-Object -Skip $maxFiles)

        $mutateFiles = if ($selectedSourceChanges.Count -gt 0) {
            @(
                $selectedSourceChanges |
                    ForEach-Object {
                        $_.Path.Substring($sourceRoot.Length).TrimStart('/')
                    })
        } else {
            @($scope.priorityFiles | Select-Object -First $maxFiles)
        }

        [PSCustomObject]@{
            Scope = [string]$scope.name
            Priority = [int]$scope.priority
            Mode = $mode
            MutateFiles = $mutateFiles
            OmittedFiles = @(
                @($omittedSourceChanges | ForEach-Object Path) +
                @($unavailableSourceChanges | ForEach-Object Path))
        }
    })

$orderedCandidates = @(
    $candidates |
        Sort-Object `
            @{ Expression = 'Priority'; Descending = $false },
            @{ Expression = 'Scope'; Descending = $false })
$selected = @($orderedCandidates | Select-Object -First $maxScopes)
$omittedScopes = @(
    $orderedCandidates |
        Select-Object -Skip $maxScopes |
        ForEach-Object Scope)
$omittedFiles = @(
    $selected |
        ForEach-Object {
            $scopeName = $_.Scope
            @($_.OmittedFiles) |
                ForEach-Object { "${scopeName}::$_" }
        })

$matrixEntries = @(
    $selected |
        ForEach-Object {
            [ordered]@{
                scope = $_.Scope
                mode = $_.Mode
                mutateFiles = @($_.MutateFiles)
            }
        })
$matrix = if ($matrixEntries.Count -gt 0) {
    [ordered]@{ include = $matrixEntries }
} else {
    [ordered]@{
        include = @(
            [ordered]@{
                scope = 'none'
                mode = 'diff'
                mutateFiles = @()
            })
    }
}

$result = [ordered]@{
    run_required = $selected.Count -gt 0
    matrix = $matrix
    selected_scopes = @($selected | ForEach-Object Scope)
    omitted_scopes = $omittedScopes
    omitted_files = $omittedFiles
    changed_count = $changed.Count
}
$matrixJson = ConvertTo-Json $matrix -Depth 8 -Compress

if (-not $NoCiOutput -and
    -not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    Add-Content `
        -Path $env:GITHUB_OUTPUT `
        -Value "run_required=$($result.run_required.ToString().ToLowerInvariant())"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "matrix=$matrixJson"
}

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value @"
## Mutation selection

- Scope limit: $maxScopes
- File limit per scope: $maxFiles
- Selected scopes: $((@($result.selected_scopes) -join ', ') -replace '^$', 'none')
- Omitted scopes: $((@($result.omitted_scopes) -join ', ') -replace '^$', 'none')
- Omitted files: $((@($result.omitted_files) -join ', ') -replace '^$', 'none')
"@
}

$result | ConvertTo-Json -Depth 10 -Compress
