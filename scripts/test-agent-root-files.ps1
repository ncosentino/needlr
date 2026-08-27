<#
.SYNOPSIS
    Validates Needlr's agent root-file budget.

.DESCRIPTION
    AGENTS.md loads in every agent session, so every byte competes with the task for
    context. The budget in .github/instructions/genesis/agent-root-files.instructions.md
    caps it at 60 lines and 3072 UTF-8 bytes. Redirect files must stay redirects rather
    than accumulating copies of guidance that already lives elsewhere.

    Without this check the budget is advisory, and a file that every agent appends one
    reasonable section to grows without bound.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$maximumLines = 60
$maximumBytes = 3072
$instructionsUrl = '.github/instructions/genesis/agent-root-files.instructions.md'

$failures = [System.Collections.Generic.List[string]]::new()

function Measure-File {
    param(
        [Parameter(Mandatory)]
        [string]$RelativePath)

    $fullPath = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        return $null
    }

    $raw = Get-Content -LiteralPath $fullPath -Raw
    if ($null -eq $raw) {
        $raw = ''
    }

    return [pscustomobject]@{
        Path  = $RelativePath
        Lines = ($raw -split "`r?`n").Count
        Bytes = [System.Text.Encoding]::UTF8.GetByteCount($raw)
        Text  = $raw
    }
}

$agents = Measure-File -RelativePath 'AGENTS.md'
if ($null -eq $agents) {
    $failures.Add('AGENTS.md is missing.')
}
else {
    if ($agents.Lines -gt $maximumLines) {
        $failures.Add(
            "AGENTS.md is $($agents.Lines) lines; the budget is $maximumLines. " +
            "Move technical rules into a path-scoped file under .github/instructions/ " +
            "whose glob matches the code the rule governs. See $instructionsUrl.")
    }

    if ($agents.Bytes -gt $maximumBytes) {
        $failures.Add(
            "AGENTS.md is $($agents.Bytes) UTF-8 bytes; the budget is $maximumBytes. " +
            "Move architecture and rationale into docs/ and exact technical rules into " +
            "path-scoped instructions. See $instructionsUrl.")
    }
}

# Redirects exist to point at AGENTS.md. Duplicating guidance in them means an agent
# reads the same rule twice and the copies drift apart.
foreach ($redirect in @('CLAUDE.md', '.github/copilot-instructions.md')) {
    $file = Measure-File -RelativePath $redirect
    if ($null -eq $file) {
        continue
    }

    if ($file.Text -notmatch 'AGENTS\.md') {
        $failures.Add("$redirect must point at AGENTS.md.")
    }

    if ($file.Text -match '(?m)^\s*```') {
        $failures.Add(
            "$redirect contains a code block. Redirects must not restate build, test, " +
            "or workflow guidance that belongs in AGENTS.md or a path-scoped instruction.")
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Agent root-file policy violations:' -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

$summary = if ($null -ne $agents) {
    "AGENTS.md $($agents.Lines)/$maximumLines lines, $($agents.Bytes)/$maximumBytes bytes."
}
else {
    'AGENTS.md not measured.'
}

Write-Host "Agent root-file policy satisfied. $summary" -ForegroundColor Green
