<#
.SYNOPSIS
    Validates Needlr's agent root-file budget and redirect contract.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$contractPath = Join-Path (Join-Path $ProjectRoot '.github') 'genesis-guidance.json'
$failures = [System.Collections.Generic.List[string]]::new()

function Measure-TextFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    $fullPath = Join-Path $ProjectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        return $null
    }

    $raw = Get-Content -LiteralPath $fullPath -Raw -Encoding UTF8
    if ($null -eq $raw) {
        $raw = ''
    }
    $canonical = $raw.Replace("`r`n", "`n").Replace("`r", "`n")

    [PSCustomObject]@{
        Path = $RelativePath
        Lines =
            if ($canonical.Length -eq 0) { 0 }
            else { ($canonical.TrimEnd("`n") -split "`n").Count }
        Bytes = [Text.Encoding]::UTF8.GetByteCount($canonical)
        Text = $canonical.TrimEnd("`n")
    }
}

if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) {
    $failures.Add('.github/genesis-guidance.json is missing.')
} else {
    $contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    $agents = Measure-TextFile -RelativePath ([string]$contract.agents.path)
    if ($null -eq $agents) {
        $failures.Add("$($contract.agents.path) is missing.")
    } else {
        $maximumLines = [int]$contract.agents.maxLines
        $maximumBytes = [int]$contract.agents.maxBytes
        if ($agents.Lines -gt $maximumLines) {
            $failures.Add(
                "$($agents.Path) is $($agents.Lines) lines; the budget is $maximumLines.")
        }
        if ($agents.Bytes -gt $maximumBytes) {
            $failures.Add(
                "$($agents.Path) is $($agents.Bytes) UTF-8 bytes; the budget is $maximumBytes.")
        }
    }

    $claude = Measure-TextFile -RelativePath ([string]$contract.agents.redirects.claude)
    if ($null -eq $claude) {
        $failures.Add("$($contract.agents.redirects.claude) is missing.")
    } elseif ($claude.Text -cne '@AGENTS.md') {
        $failures.Add(
            "$($claude.Path) must be the exact one-line '@AGENTS.md' redirect.")
    }

    $copilot = Measure-TextFile -RelativePath ([string]$contract.agents.redirects.copilot)
    if ($null -eq $copilot) {
        $failures.Add("$($contract.agents.redirects.copilot) is missing.")
    } else {
        if ($copilot.Lines -gt 3 -or $copilot.Bytes -gt 128) {
            $failures.Add(
                "$($copilot.Path) is $($copilot.Lines) lines/$($copilot.Bytes) bytes; " +
                'the budget is 3 lines/128 bytes.')
        }
        if ($copilot.Text -notmatch 'AGENTS\.md') {
            $failures.Add("$($copilot.Path) must point at AGENTS.md.")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Agent root-file policy violations:' -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host (
    "Agent root-file policy satisfied. AGENTS.md $($agents.Lines)/" +
    "$maximumLines lines, $($agents.Bytes)/$maximumBytes bytes.") `
    -ForegroundColor Green
