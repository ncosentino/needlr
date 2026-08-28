#Requires -Version 7.0
<#
.SYNOPSIS
    Resolve generated-project CI scope for one workflow event.

.PARAMETER EventName
    Caller event name: pull_request, push, or workflow_dispatch.

.PARAMETER RequestedScope
    workflow_dispatch scope: full or subset.

.PARAMETER IsDraft
    Whether the pull request is draft.

.PARAMETER DraftMode
    Existing project draft mode: full, subset, or ready-only.

.PARAMETER ChangedFiles
    Complete pull-request changed-file paths, including previous rename paths.

.PARAMETER Conservative
    Disable guidance-only classification when file discovery is incomplete.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('pull_request','push','workflow_dispatch')]
    [string]$EventName,

    [string]$RequestedScope = '',
    [bool]$IsDraft = $false,

    [ValidateSet('full','subset','ready-only')]
    [string]$DraftMode = 'ready-only',

    [string[]]$ChangedFiles = @(),
    [switch]$Conservative
)

$ErrorActionPreference = 'Stop'

if ($EventName -eq 'workflow_dispatch') {
    $scope = if ($RequestedScope) { $RequestedScope } else { 'full' }
    if ($scope -notin @('full','subset')) {
        Write-Error "Unsupported requested validation scope '$scope'."
    }
    if ($scope -eq 'subset') {
        return 'full'
    }
    return $scope
}
if ($EventName -eq 'push') {
    return 'full'
}

$normalizedFiles = @(
    foreach ($changedFile in $ChangedFiles) {
        $normalized = ([string]$changedFile).Replace('\','/')
        while ($normalized.StartsWith('./',[StringComparison]::Ordinal)) {
            $normalized = $normalized.Substring(2)
        }
        $normalized = $normalized.TrimStart('/')
        if ($normalized) { $normalized }
    }
) | Sort-Object -CaseSensitive -Unique

$guidancePatterns = @(
    '^AGENTS\.md$',
    '^CLAUDE\.md$',
    '^README\.md$',
    '^mkdocs\.yml$',
    '^\.claude/CLAUDE\.md$',
    '^\.claude/rules/generated/',
    '^\.github/agents/',
    '^\.github/copilot-instructions\.md$',
    '^\.github/genesis-guidance(?:\.schema)?\.json$',
    '^\.github/instructions/',
    '^\.github/skills/',
    '^docs/',
    '^overrides/',
    '^scripts/guidance/'
)
$guidanceOnly = (
    -not $Conservative -and
    $normalizedFiles.Count -gt 0 -and
    @(
        $normalizedFiles |
            Where-Object {
                $path = $_
                -not ($guidancePatterns | Where-Object { $path -match $_ })
            }
    ).Count -eq 0
)
if ($guidanceOnly) {
    return 'guidance'
}

if ($IsDraft) {
    return $DraftMode
}
return 'full'
