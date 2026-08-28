<#
.SYNOPSIS
    Validates Needlr's project-owned guidance architecture.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$contractPath = Join-Path $repoRoot '.github/genesis-guidance.json'
$schemaPath = Join-Path $repoRoot '.github/genesis-guidance.schema.json'
$instructionsRoot = Join-Path $repoRoot '.github/instructions'
$mirrorsRoot = Join-Path $repoRoot '.claude/rules/generated'
$contextReportPath = Join-Path $repoRoot 'scripts/guidance/Get-InstructionContextReport.ps1'
$scopeResolverPath = Join-Path $repoRoot 'scripts/guidance/Resolve-ValidationScope.ps1'
$rootTestPath = Join-Path $repoRoot 'scripts/test-agent-root-files.ps1'
$reviewSkillPath = Join-Path $repoRoot '.github/skills/review-changes/SKILL.md'
$ciWorkflowPath = Join-Path $repoRoot '.github/workflows/ci.yml'

. (Join-Path $repoRoot 'scripts\guidance\InstructionGlob.Functions.ps1')

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([Parameter(Mandatory)][string]$Message)
    $failures.Add($Message)
}

function Get-CanonicalText {
    param([Parameter(Mandatory)][string]$Path)
    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ($null -eq $content) {
        return ''
    }
    return $content.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Get-InstructionParts {
    param([Parameter(Mandatory)][string]$Path)

    $content = Get-CanonicalText -Path $Path
    $lines = @($content -split "`n")
    if ($lines.Count -lt 3 -or $lines[0] -cne '---') {
        throw "Instruction '$Path' must start with YAML frontmatter."
    }
    $end = -1
    for ($index = 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -ceq '---') {
            $end = $index
            break
        }
    }
    if ($end -lt 0) {
        throw "Instruction '$Path' has no closing frontmatter marker."
    }

    $frontmatter = ($lines[1..($end - 1)] -join "`n")
    $applyToMatch = [regex]::Match(
        $frontmatter,
        '(?m)^\s*applyTo\s*:\s*(.+?)\s*$')
    if (-not $applyToMatch.Success) {
        throw "Instruction '$Path' has no applyTo value."
    }

    $body =
        if ($end + 1 -lt $lines.Count) {
            ($lines[($end + 1)..($lines.Count - 1)] -join "`n").Trim("`n")
        } else {
            ''
        }

    [PSCustomObject]@{
        Content = $content
        Frontmatter = $frontmatter
        ApplyTo = $applyToMatch.Groups[1].Value.Trim().Trim('"', "'")
        Body = $body
        HasReviewThresholdReason = [regex]::IsMatch(
            $frontmatter,
            '(?m)^\s*reviewThresholdReason\s*:')
    }
}

function Get-ExpectedMirrorContent {
    param(
        [Parameter(Mandatory)][string]$InstructionRelativePath,
        [Parameter(Mandatory)]$Parts
    )

    $patterns = @(Split-InstructionGlobPatterns -ApplyTo $Parts.ApplyTo)
    $builder = [Text.StringBuilder]::new()
    [void]$builder.AppendLine('---')
    [void]$builder.AppendLine(
        "# AUTO-GENERATED from .github/instructions/$InstructionRelativePath — do not edit")
    [void]$builder.AppendLine('paths:')
    foreach ($pattern in $patterns) {
        [void]$builder.AppendLine("  - `"$pattern`"")
    }
    [void]$builder.AppendLine('---')
    [void]$builder.Append($Parts.Body)
    return $builder.ToString().Replace("`r`n", "`n").TrimEnd("`n")
}

function Get-MirrorFindings {
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$MirrorRoot
    )

    $findings = [System.Collections.Generic.List[string]]::new()
    $expectedPaths = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)

    foreach ($instruction in @(
            Get-ChildItem -LiteralPath $SourceRoot -Recurse -Filter '*.instructions.md' -File |
                Sort-Object FullName)) {
        $relative = [IO.Path]::GetRelativePath(
            $SourceRoot,
            $instruction.FullName).Replace('\', '/')
        $mirrorRelative =
            $relative.Substring(
                0,
                $relative.Length - '.instructions.md'.Length) + '.md'
        [void]$expectedPaths.Add($mirrorRelative)
        $mirrorPath = Join-Path $MirrorRoot $mirrorRelative
        if (-not (Test-Path -LiteralPath $mirrorPath -PathType Leaf)) {
            $findings.Add("Missing Claude mirror: $mirrorRelative")
            continue
        }

        $parts = Get-InstructionParts -Path $instruction.FullName
        $expected = Get-ExpectedMirrorContent `
            -InstructionRelativePath $relative `
            -Parts $parts
        $actual = (Get-CanonicalText -Path $mirrorPath).TrimEnd("`n")
        if ($actual -cne $expected) {
            $findings.Add("Stale Claude mirror: $mirrorRelative")
        }
    }

    if (Test-Path -LiteralPath $MirrorRoot -PathType Container) {
        foreach ($mirror in @(
                Get-ChildItem -LiteralPath $MirrorRoot -Recurse -Filter '*.md' -File)) {
            $relative = [IO.Path]::GetRelativePath(
                $MirrorRoot,
                $mirror.FullName).Replace('\', '/')
            if (-not $expectedPaths.Contains($relative)) {
                $findings.Add("Orphaned Claude mirror: $relative")
            }
        }
    }

    return @($findings)
}

function Get-UnlistedDocs {
    param(
        [Parameter(Mandatory)][string]$DocsRoot,
        [Parameter(Mandatory)][string]$NavigationPath
    )

    $navigation = Get-Content -LiteralPath $NavigationPath -Raw -Encoding UTF8
    return @(
        Get-ChildItem -LiteralPath $DocsRoot -Recurse -Filter '*.md' -File |
            ForEach-Object {
                [IO.Path]::GetRelativePath($DocsRoot, $_.FullName).Replace('\', '/')
            } |
            Where-Object {
                $path = [regex]::Escape($_)
                -not [regex]::IsMatch(
                    $navigation,
                    "(?m)^\s*-\s+(?:[^:`r`n]+:\s*)?['`"]?$path['`"]?\s*$")
            } |
            Sort-Object
    )
}

function Invoke-NegativeFixtures {
    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) (
        'needlr-guidance-fixture-' + [Guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $fixtureRoot | Out-Null

        $rootFixture = Join-Path $fixtureRoot 'root'
        New-Item -ItemType Directory -Path (Join-Path $rootFixture '.github') -Force |
            Out-Null
        Copy-Item -LiteralPath $contractPath -Destination (
            Join-Path $rootFixture '.github/genesis-guidance.json')
        Set-Content `
            -LiteralPath (Join-Path $rootFixture 'AGENTS.md') `
            -Value ([string]::Join("`n", (@('x') * 61))) `
            -Encoding UTF8 `
            -NoNewline
        Set-Content -LiteralPath (Join-Path $rootFixture 'CLAUDE.md') -Value 'not a redirect' -Encoding UTF8
        Set-Content -LiteralPath (
            Join-Path $rootFixture '.github/copilot-instructions.md') `
            -Value 'See AGENTS.md.' `
            -Encoding UTF8
        $rootOutput = & pwsh -NoProfile -File $rootTestPath -ProjectRoot $rootFixture 2>&1
        if ($LASTEXITCODE -eq 0) {
            Add-Failure 'Negative fixture: root-file validator accepted an oversized AGENTS.md.'
        }
        if (-not ($rootOutput -match '61 lines')) {
            Add-Failure 'Negative fixture: root-file failure did not report the measured line count.'
        }

        $contextInstructions = Join-Path $fixtureRoot 'context-instructions'
        New-Item -ItemType Directory -Path $contextInstructions | Out-Null
        $oversized = @(
            '---',
            'applyTo: "**/*.cs"',
            '---'
        ) + @('rule') * 601
        Set-Content -LiteralPath (
            Join-Path $contextInstructions 'oversized.instructions.md') `
            -Value $oversized `
            -Encoding UTF8
        $contextJson = & $contextReportPath `
            -ProjectRoot $repoRoot `
            -InstructionsRoot $contextInstructions `
            -Path 'fixture.cs' `
            -Json
        $context = $contextJson | ConvertFrom-Json
        if ($context.summary.hard_exceeded -ne 1) {
            Add-Failure 'Negative fixture: context report did not reject a 601-line matched stack.'
        }

        $docsFixture = Join-Path $fixtureRoot 'docs'
        New-Item -ItemType Directory -Path $docsFixture | Out-Null
        Set-Content -LiteralPath (Join-Path $docsFixture 'index.md') -Value '# Index'
        Set-Content -LiteralPath (Join-Path $docsFixture 'orphan.md') -Value '# Orphan'
        $navigationFixture = Join-Path $fixtureRoot 'mkdocs.yml'
        Set-Content -LiteralPath $navigationFixture -Value 'nav: [index.md]'
        $unlisted = Get-UnlistedDocs `
            -DocsRoot $docsFixture `
            -NavigationPath $navigationFixture
        if ($unlisted -notcontains 'orphan.md') {
            Add-Failure 'Negative fixture: docs navigation check did not find an unlisted page.'
        }

        $mirrorSource = Join-Path $fixtureRoot 'mirror-source'
        $mirrorOutput = Join-Path $fixtureRoot 'mirror-output'
        New-Item -ItemType Directory -Path $mirrorSource | Out-Null
        New-Item -ItemType Directory -Path $mirrorOutput | Out-Null
        Set-Content -LiteralPath (
            Join-Path $mirrorSource 'sample.instructions.md') -Value @'
---
applyTo: "**/*.cs"
---
# Current
'@
        Set-Content -LiteralPath (
            Join-Path $mirrorOutput 'sample.md') -Value '# Stale'
        $mirrorFindings = Get-MirrorFindings `
            -SourceRoot $mirrorSource `
            -MirrorRoot $mirrorOutput
        if (-not ($mirrorFindings -match 'Stale Claude mirror')) {
            Add-Failure 'Negative fixture: mirror check did not reject stale generated content.'
        }
    } finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

Invoke-NegativeFixtures

if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) {
    Add-Failure '.github/genesis-guidance.json is missing.'
} elseif (-not (
        Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 |
            Test-Json -SchemaFile $schemaPath)) {
    Add-Failure '.github/genesis-guidance.json does not satisfy its schema.'
}

$rootOutput = & pwsh -NoProfile -File $rootTestPath -ProjectRoot $repoRoot 2>&1
if ($LASTEXITCODE -ne 0) {
    Add-Failure "Agent root-file validation failed:`n$($rootOutput | Out-String)"
}

$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$reviewThresholdLines = [int]$contract.instructions.individualReviewThreshold.lines
$reviewThresholdBytes = [int]$contract.instructions.individualReviewThreshold.bytes

foreach ($instruction in @(
        Get-ChildItem -LiteralPath $instructionsRoot -Recurse -Filter '*.instructions.md' -File)) {
    try {
        $parts = Get-InstructionParts -Path $instruction.FullName
    } catch {
        Add-Failure $_.Exception.Message
        continue
    }

    $canonical = $parts.Content.Replace("`r`n", "`n").Replace("`r", "`n")
    $lines =
        if ($canonical.Length -eq 0) { 0 }
        else { @($canonical.TrimEnd("`n") -split "`n").Count }
    $bytes = [Text.Encoding]::UTF8.GetByteCount($canonical)
    if (
        ($lines -gt $reviewThresholdLines -or $bytes -gt $reviewThresholdBytes) -and
        -not $parts.HasReviewThresholdReason
    ) {
        $relative = [IO.Path]::GetRelativePath(
            $repoRoot,
            $instruction.FullName).Replace('\', '/')
        Add-Failure(
            "$relative is $lines lines/$bytes bytes and requires reviewThresholdReason.")
    }

    $relativeToInstructions = [IO.Path]::GetRelativePath(
        $instructionsRoot,
        $instruction.FullName).Replace('\', '/')
    if (
        -not $relativeToInstructions.StartsWith('genesis/', [StringComparison]::Ordinal) -and
        [regex]::IsMatch($parts.Body, '[A-Za-z0-9_./-]+\.instructions\.md')
    ) {
        Add-Failure(
            "$relativeToInstructions references another instruction file by name.")
    }
}

$unlistedDocs = Get-UnlistedDocs `
    -DocsRoot (Join-Path $repoRoot 'docs') `
    -NavigationPath (Join-Path $repoRoot 'mkdocs.yml')
foreach ($unlistedDoc in $unlistedDocs) {
    Add-Failure("Documentation page is absent from mkdocs.yml navigation: docs/$unlistedDoc")
}

foreach ($page in @($contract.docs.pages)) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $page.path) -PathType Leaf)) {
        Add-Failure("Guidance contract page does not exist: $($page.path)")
    }
}

$contextJson = & $contextReportPath -ProjectRoot $repoRoot -Json
$context = $contextJson | ConvertFrom-Json
if ($context.summary.hard_exceeded -gt 0) {
    $worst = $context.top_contexts | Select-Object -First 5
    Add-Failure(
        "Instruction context hard limit exceeded for $($context.summary.hard_exceeded) " +
        "path(s). Worst:`n$($worst | Format-Table path,lines,bytes | Out-String)")
}

$reviewSkill = Get-Content -LiteralPath $reviewSkillPath -Raw -Encoding UTF8
foreach ($requiredReference in @(
        'Get-ApplicableInstructions.ps1',
        'Get-ValidationInventory.ps1',
        'Get-InstructionContextReport.ps1',
        '.github/genesis-delivery.json'
    )) {
    if (-not $reviewSkill.Contains($requiredReference)) {
        Add-Failure("Review skill does not reference $requiredReference.")
    }
}

$guidanceScope = & $scopeResolverPath `
    -EventName pull_request `
    -DraftMode subset `
    -ChangedFiles @(
        'AGENTS.md',
        'docs/index.md',
        'mkdocs.yml',
        '.claude/rules/generated/docs.md'
    )
if ($guidanceScope -cne 'guidance') {
    Add-Failure(
        "Guidance-only pull requests must resolve to 'guidance'; got '$guidanceScope'.")
}

$sourceScope = & $scopeResolverPath `
    -EventName pull_request `
    -DraftMode subset `
    -ChangedFiles 'src/NexusLabs.Needlr/Syringe.cs'
if ($sourceScope -cne 'full') {
    Add-Failure("Source pull requests must resolve to 'full'; got '$sourceScope'.")
}

$hookScope = & $scopeResolverPath `
    -EventName pull_request `
    -DraftMode subset `
    -ChangedFiles '.claude/hooks/sync-copilot-rules.ps1'
if ($hookScope -cne 'full') {
    Add-Failure("Executable hook changes must resolve to 'full'; got '$hookScope'.")
}

$draftSourceScope = & $scopeResolverPath `
    -EventName pull_request `
    -IsDraft $true `
    -DraftMode subset `
    -ChangedFiles 'src/NexusLabs.Needlr/Syringe.cs'
if ($draftSourceScope -cne 'subset') {
    Add-Failure(
        "Draft source pull requests must preserve 'subset'; got '$draftSourceScope'.")
}

$conservativeScope = & $scopeResolverPath `
    -EventName pull_request `
    -DraftMode subset `
    -ChangedFiles 'docs/index.md' `
    -Conservative
if ($conservativeScope -cne 'full') {
    Add-Failure(
        "Conservative scope resolution must fail closed to 'full'; got " +
        "'$conservativeScope'.")
}

$ciWorkflow = Get-Content -LiteralPath $ciWorkflowPath -Raw -Encoding UTF8
foreach ($requiredWorkflowText in @(
        'guidance-validation-plan.yml',
        'guidance-validation:',
        'scripts/test-guidance.ps1',
        'guidance)'
    )) {
    if (-not $ciWorkflow.Contains($requiredWorkflowText)) {
        Add-Failure("CI workflow is missing guidance routing: $requiredWorkflowText")
    }
}

foreach ($mirrorFinding in @(
        Get-MirrorFindings `
            -SourceRoot $instructionsRoot `
            -MirrorRoot $mirrorsRoot)) {
    Add-Failure $mirrorFinding
}

if ($failures.Count -gt 0) {
    Write-Host 'Guidance validation failures:' -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host (
    "Guidance validation passed. $($context.summary.instruction_count) instructions, " +
    "$($context.summary.path_count) repository paths, " +
    "$($context.summary.target_exceeded) above target, zero above the hard ceiling.") `
    -ForegroundColor Green
