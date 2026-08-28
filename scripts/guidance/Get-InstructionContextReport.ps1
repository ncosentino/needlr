#Requires -Version 7.0
<#
.SYNOPSIS
    Measures the complete matched instruction context for repository files.

.DESCRIPTION
    Resolves every instruction once, scans explicit paths or every tracked/untracked
    non-ignored file, and reports target and hard-budget violations. This command is
    read-only. UTF-8 byte counts normalize line endings to LF so checkout settings do
    not change the reported instruction context.

.PARAMETER ProjectRoot
    Repository root. Defaults to the project containing this script.

.PARAMETER InstructionsRoot
    Instruction root. Defaults to <ProjectRoot>/.github/instructions.

.PARAMETER Path
    Optional repository-relative paths. When omitted, Git supplies every tracked and
    untracked non-ignored path.

.PARAMETER TargetLines
    Target matched-context line budget.

.PARAMETER TargetBytes
    Target matched-context UTF-8 byte budget after line-ending normalization.

.PARAMETER MaxLines
    Hard matched-context line ceiling.

.PARAMETER MaxBytes
    Hard matched-context UTF-8 byte ceiling after line-ending normalization.

.PARAMETER Json
    Emit JSON.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [string]$InstructionsRoot,
    [string[]]$Path = @(),
    [ValidateRange(1, [int]::MaxValue)]
    [int]$TargetLines = 300,
    [ValidateRange(1, [int]::MaxValue)]
    [int]$TargetBytes = 16384,
    [ValidateRange(1, [int]::MaxValue)]
    [int]$MaxLines = 600,
    [ValidateRange(1, [int]::MaxValue)]
    [int]$MaxBytes = 32768,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$InstructionsRoot =
    if ([string]::IsNullOrWhiteSpace($InstructionsRoot)) {
        Join-Path $ProjectRoot '.github' 'instructions'
    } else {
        (Resolve-Path -LiteralPath $InstructionsRoot).Path
    }
if (-not (Test-Path -LiteralPath $InstructionsRoot -PathType Container)) {
    throw "Instruction root not found: '$InstructionsRoot'."
}

. (Join-Path $PSScriptRoot 'InstructionGlob.Functions.ps1')

$contextExceptions = @()
$guidanceContractPath = Join-Path $ProjectRoot '.github' 'genesis-guidance.json'
if (Test-Path -LiteralPath $guidanceContractPath -PathType Leaf) {
    $guidanceContract = Get-Content `
        -LiteralPath $guidanceContractPath `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    $matchedContext = $guidanceContract.instructions.matchedContext
    if (-not $PSBoundParameters.ContainsKey('TargetLines')) {
        $TargetLines = [int]$matchedContext.targetLines
    }
    if (-not $PSBoundParameters.ContainsKey('TargetBytes')) {
        $TargetBytes = [int]$matchedContext.targetBytes
    }
    if (-not $PSBoundParameters.ContainsKey('MaxLines')) {
        $MaxLines = [int]$matchedContext.maxLines
    }
    if (-not $PSBoundParameters.ContainsKey('MaxBytes')) {
        $MaxBytes = [int]$matchedContext.maxBytes
    }
    $contextExceptions = @($guidanceContract.contextExceptions)
}
if ($TargetLines -gt $MaxLines -or $TargetBytes -gt $MaxBytes) {
    throw 'Target instruction budgets cannot exceed hard ceilings.'
}

function Get-FrontmatterValue {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Name
    )

    $match = [regex]::Match(
        $Content,
        "(?m)^\s*$([regex]::Escape($Name))\s*:\s*(.+?)\s*$")
    if (-not $match.Success) {
        return ''
    }
    return $match.Groups[1].Value.Trim().Trim('"', "'")
}

function ConvertTo-RepositoryPath {
    param([Parameter(Mandatory)][string]$Value)

    if ([IO.Path]::IsPathRooted($Value) -or $Value -match '^[A-Za-z]:[\\/]') {
        throw "Path '$Value' must be repository-relative."
    }
    $normalized = $Value.Replace('\', '/').TrimStart('/')
    while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }
    if (
        [string]::IsNullOrWhiteSpace($normalized) -or
        $normalized -eq '.' -or
        $normalized.Split('/') -contains '..'
    ) {
        throw "Path '$Value' must identify a file inside the repository."
    }
    return $normalized
}

if ($Path.Count -eq 0) {
    $paths = @(& git -C $ProjectRoot ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate repository files in '$ProjectRoot'."
    }
} else {
    $paths = @($Path)
}
$paths = @(
    $paths |
        ForEach-Object { ConvertTo-RepositoryPath -Value ([string]$_) } |
        Sort-Object -CaseSensitive -Unique
)

$instructions = @(
    Get-ChildItem -LiteralPath $InstructionsRoot -Recurse -Filter '*.instructions.md' -File |
        Sort-Object FullName |
        ForEach-Object {
            $content = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
            $canonicalContent = $content.Replace("`r`n", "`n").Replace("`r", "`n")
            $applyTo = Get-FrontmatterValue -Content $content -Name 'applyTo'
            if ([string]::IsNullOrWhiteSpace($applyTo)) {
                throw "Instruction '$($_.FullName)' has no applyTo value."
            }
            $regexes = @(
                foreach ($pattern in @(Split-InstructionGlobPatterns -ApplyTo $applyTo)) {
                    foreach ($expanded in @(Expand-InstructionGlobPattern -Pattern $pattern)) {
                        [regex]::new(
                            (ConvertTo-InstructionGlobRegex -Pattern $expanded),
                            [Text.RegularExpressions.RegexOptions]::CultureInvariant)
                    }
                }
            )
            $relative = [IO.Path]::GetRelativePath(
                $InstructionsRoot,
                $_.FullName
            ).Replace('\', '/')
            [PSCustomObject]@{
                path = $relative
                apply_to = $applyTo
                lines = @(Get-Content -LiteralPath $_.FullName -Encoding UTF8).Count
                bytes = [Text.Encoding]::UTF8.GetByteCount($canonicalContent)
                managed = $relative.StartsWith(
                    'genesis/',
                    [StringComparison]::Ordinal)
                regexes = $regexes
                match_count = 0
            }
        }
)

$contexts = [System.Collections.Generic.List[object]]::new()
foreach ($relativePath in $paths) {
    $lines = 0
    $bytes = 0
    $managedLines = 0
    $managedBytes = 0
    $matched = [System.Collections.Generic.List[string]]::new()
    foreach ($instruction in $instructions) {
        $isMatch = $false
        foreach ($regex in $instruction.regexes) {
            if ($regex.IsMatch($relativePath)) {
                $isMatch = $true
                break
            }
        }
        if (-not $isMatch) {
            continue
        }
        $lines += [int]$instruction.lines
        $bytes += [int]$instruction.bytes
        if ($instruction.managed) {
            $managedLines += [int]$instruction.lines
            $managedBytes += [int]$instruction.bytes
        }
        $instruction.match_count++
        $matched.Add([string]$instruction.path) | Out-Null
    }
    $exception = @(
        $contextExceptions |
            Where-Object {
                Test-InstructionGlobMatch `
                    -ApplyTo ([string]$_.pattern) `
                    -RelativePath $relativePath
            } |
            Select-Object -First 1
    )
    $limitLines =
        if ($exception.Count) { [int]$exception[0].maxLines }
        else { $MaxLines }
    $limitBytes =
        if ($exception.Count) { [int]$exception[0].maxBytes }
        else { $MaxBytes }
    $contexts.Add([PSCustomObject][ordered]@{
        path = $relativePath
        lines = $lines
        bytes = $bytes
        managed_lines = $managedLines
        managed_bytes = $managedBytes
        target_exceeded = ($lines -gt $TargetLines -or $bytes -gt $TargetBytes)
        hard_exceeded = ($lines -gt $limitLines -or $bytes -gt $limitBytes)
        managed_hard_exceeded = (
            $managedLines -gt $limitLines -or
            $managedBytes -gt $limitBytes)
        limit_lines = $limitLines
        limit_bytes = $limitBytes
        exception =
            if ($exception.Count) {
                [PSCustomObject][ordered]@{
                    pattern = [string]$exception[0].pattern
                    owner = [string]$exception[0].owner
                    reason = [string]$exception[0].reason
                }
            } else {
                $null
            }
        matched = @($matched)
    }) | Out-Null
}

$targetExceeded = @($contexts | Where-Object target_exceeded)
$hardExceeded = @($contexts | Where-Object hard_exceeded)
$managedHardExceeded = @($contexts | Where-Object managed_hard_exceeded)
$top = @(
    $contexts |
        Sort-Object `
            @{ Expression = { $_.lines }; Descending = $true }, `
            @{ Expression = { $_.bytes }; Descending = $true }, `
            @{ Expression = { $_.path }; Descending = $false } |
        Select-Object -First 25
)
$topInstructions = @(
    $instructions |
        ForEach-Object {
            [PSCustomObject][ordered]@{
                path = [string]$_.path
                apply_to = [string]$_.apply_to
                lines = [int]$_.lines
                bytes = [int]$_.bytes
                match_count = [int]$_.match_count
                weighted_lines = [int]$_.lines * [int]$_.match_count
                weighted_bytes = [int]$_.bytes * [int]$_.match_count
            }
        } |
        Sort-Object `
            @{ Expression = { $_.weighted_lines }; Descending = $true }, `
            @{ Expression = { $_.path }; Descending = $false } |
        Select-Object -First 25
)
$report = [PSCustomObject][ordered]@{
    schema_version = '1'
    status =
        if ($hardExceeded.Count -gt 0) { 'hard-limit-exceeded' }
        elseif ($targetExceeded.Count -gt 0) { 'target-exceeded' }
        else { 'clean' }
    budgets = [ordered]@{
        target_lines = $TargetLines
        target_bytes = $TargetBytes
        max_lines = $MaxLines
        max_bytes = $MaxBytes
    }
    summary = [ordered]@{
        instruction_count = $instructions.Count
        path_count = $contexts.Count
        target_exceeded = $targetExceeded.Count
        hard_exceeded = $hardExceeded.Count
        managed_hard_exceeded = $managedHardExceeded.Count
        max_lines = ($contexts | Measure-Object lines -Maximum).Maximum
        max_bytes = ($contexts | Measure-Object bytes -Maximum).Maximum
    }
    top_contexts = $top
    top_instructions = $topInstructions
    contexts = @($contexts)
}

if ($Json) {
    $report | ConvertTo-Json -Depth 12
} else {
    $report
}
