<#
.SYNOPSIS
    Claude Code hook — regenerates Claude rules from Copilot instruction files.
    Intended to run on SessionStart via .claude/settings.local.json.
#>
$ErrorActionPreference = 'Stop'

# Find the compiler script. It may be:
# 1. In a known plugin path (set COPILOT_TO_CLAUDE_COMPILER env var)
# 2. Alongside this hook (development layout)
$compilerScript = $env:COPILOT_TO_CLAUDE_COMPILER
if (-not $compilerScript -or -not (Test-Path $compilerScript)) {
    # Scan the Claude plugin cache for the compiler skill
    $cacheRoot = Join-Path $env:USERPROFILE '.claude' 'plugins' 'cache'
    if (Test-Path $cacheRoot) {
        $compilerScript = Get-ChildItem -Path $cacheRoot -Recurse -Filter 'sync-copilot-to-claude.ps1' -ErrorAction SilentlyContinue |
            Select-Object -First 1 |
            ForEach-Object { $_.FullName }
    }
}
if (-not $compilerScript -or -not (Test-Path $compilerScript)) {
    # Fallback: development layout (skill alongside this hook)
    $compilerScript = Join-Path $PSScriptRoot '..\..\skills\copilot-to-claude-compiler\sync-copilot-to-claude.ps1'
}
if (-not (Test-Path $compilerScript)) {
    Write-Warning "Copilot-to-Claude compiler not found. Install the agentic-plugins plugin or set COPILOT_TO_CLAUDE_COMPILER env var."
    exit 0
}

# Run the compiler against this repo
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { $repoRoot = $PSScriptRoot | Split-Path | Split-Path }

& pwsh -NoProfile -File $compilerScript -SourceRoot $repoRoot