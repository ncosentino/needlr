#Requires -Version 7.0
<#
.SYNOPSIS
    Shared instruction applyTo glob parsing and matching functions.

.DESCRIPTION
    Supports comma-separated patterns, brace alternatives, *, **, and ? while keeping
    commas inside brace groups intact.
#>

function Split-InstructionGlobPatterns {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ApplyTo
    )

    $patterns = [System.Collections.Generic.List[string]]::new()
    $current = [Text.StringBuilder]::new()
    $braceDepth = 0
    foreach ($character in $ApplyTo.ToCharArray()) {
        if ($character -ceq '{') {
            $braceDepth++
        } elseif ($character -ceq '}') {
            $braceDepth--
            if ($braceDepth -lt 0) {
                throw "Invalid instruction glob '$ApplyTo': unmatched closing brace."
            }
        }

        if ($character -ceq ',' -and $braceDepth -eq 0) {
            $pattern = $current.ToString().Trim()
            if ($pattern) {
                $patterns.Add($pattern)
            }
            [void]$current.Clear()
        } else {
            [void]$current.Append($character)
        }
    }
    if ($braceDepth -ne 0) {
        throw "Invalid instruction glob '$ApplyTo': unmatched opening brace."
    }
    $lastPattern = $current.ToString().Trim()
    if ($lastPattern) {
        $patterns.Add($lastPattern)
    }
    return @($patterns)
}

function Expand-InstructionGlobPattern {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Pattern
    )

    $openIndex = $Pattern.IndexOf('{')
    if ($openIndex -lt 0) {
        return @($Pattern)
    }

    $depth = 0
    $closeIndex = -1
    for ($index = $openIndex; $index -lt $Pattern.Length; $index++) {
        if ($Pattern[$index] -ceq '{') {
            $depth++
        } elseif ($Pattern[$index] -ceq '}') {
            $depth--
            if ($depth -eq 0) {
                $closeIndex = $index
                break
            }
        }
    }
    if ($closeIndex -lt 0) {
        throw "Invalid instruction glob '$Pattern': unmatched opening brace."
    }

    $prefix = $Pattern.Substring(0, $openIndex)
    $body = $Pattern.Substring($openIndex + 1, $closeIndex - $openIndex - 1)
    $suffix = $Pattern.Substring($closeIndex + 1)
    $alternatives = Split-InstructionGlobPatterns -ApplyTo $body
    if ($alternatives.Count -eq 0) {
        throw "Invalid instruction glob '$Pattern': empty brace alternatives."
    }

    $expanded = [System.Collections.Generic.List[string]]::new()
    foreach ($alternative in $alternatives) {
        foreach ($value in @(
            Expand-InstructionGlobPattern `
                -Pattern ($prefix + $alternative + $suffix)
        )) {
            $expanded.Add($value)
        }
    }
    return @($expanded)
}

function ConvertTo-InstructionGlobRegex {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Pattern
    )

    $regex = [regex]::Escape($Pattern)
    $regex = $regex -replace '\\\*\\\*/', '(?:.*/)?'
    $regex = $regex -replace '\\\*\\\*', '.*'
    $regex = $regex -replace '\\\*', '[^/]*'
    $regex = $regex -replace '\\\?', '[^/]'
    return "^$regex$"
}

function Test-InstructionGlobMatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ApplyTo,

        [Parameter(Mandatory)]
        [string]$RelativePath
    )

    $normalized = $RelativePath.Replace('\', '/')
    foreach ($pattern in @(
        Split-InstructionGlobPatterns -ApplyTo $ApplyTo
    )) {
        foreach ($expanded in @(
            Expand-InstructionGlobPattern -Pattern $pattern
        )) {
            if ($normalized -cmatch (ConvertTo-InstructionGlobRegex -Pattern $expanded)) {
                return $true
            }
        }
    }
    return $false
}
