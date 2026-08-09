#Requires -Version 7.0
<#
.SYNOPSIS
    Resolve project instruction files that apply to repository-relative paths.

.PARAMETER Path
    One or more repository-relative paths to resolve.

.PARAMETER InstructionsRoot
    Optional instruction root override. Defaults to <project>/.github/instructions.

.OUTPUTS
    PSCustomObject records with Path and InstructionPath.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string[]]$Path,

    [string]$InstructionsRoot
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
. (Join-Path $PSScriptRoot 'InstructionGlob.Functions.ps1')
if (-not $InstructionsRoot) {
    $InstructionsRoot = Join-Path $projectRoot '.github' 'instructions'
}
if (-not (Test-Path $InstructionsRoot -PathType Container)) {
    Write-Error "Instruction root not found at '$InstructionsRoot'."
}
$InstructionsRoot = (Resolve-Path $InstructionsRoot).Path

function Test-InstructionMatch {
    param(
        [string]$InstructionPath,
        [string]$RelativePath
    )

    $content = Get-Content $InstructionPath -Raw -Encoding UTF8
    $applyToLine = $content -split "`n" |
        ForEach-Object { $_.TrimEnd("`r") } |
        Where-Object { $_ -match '^\s*applyTo\s*:' } |
        Select-Object -First 1
    if (-not $applyToLine) {
        Write-Error "Instruction file '$InstructionPath' has no applyTo value."
    }

    $applyTo = (
        $applyToLine -replace '^\s*applyTo\s*:\s*', ''
    ).Trim().Trim('"', "'")
    return Test-InstructionGlobMatch `
        -ApplyTo $applyTo `
        -RelativePath $RelativePath
}

$instructions = @(
    Get-ChildItem $InstructionsRoot -Recurse -Filter '*.instructions.md' -File |
        Sort-Object FullName
)
foreach ($candidate in ($Path | Sort-Object -CaseSensitive -Unique)) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        Write-Error 'Repository-relative paths must not be empty.'
    }
    if (
        [IO.Path]::IsPathRooted($candidate) -or
        $candidate -match '^[A-Za-z]:[\\/]'
    ) {
        Write-Error "Path '$candidate' must be repository-relative."
    }

    $normalized = $candidate.Replace('\', '/')
    while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }
    $normalized = $normalized.TrimStart('/')
    if (
        [string]::IsNullOrWhiteSpace($normalized) -or
        $normalized -eq '.' -or
        $normalized.Split('/') -contains '..'
    ) {
        Write-Error "Path '$candidate' must identify a file inside the repository."
    }

    foreach ($instruction in $instructions) {
        if (Test-InstructionMatch $instruction.FullName $normalized) {
            [PSCustomObject]@{
                Path            = $normalized
                InstructionPath = [IO.Path]::GetRelativePath(
                    $projectRoot,
                    $instruction.FullName
                ).Replace('\', '/')
            }
        }
    }
}
