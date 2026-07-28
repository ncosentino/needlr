#Requires -Version 7.0
<#
.SYNOPSIS
    Installs the generated private-repository exact-SHA auto-merge workflow.

.DESCRIPTION
    Reads `.github/genesis-delivery.json`, discovers the workflow display names
    that own required merge gates, renders the inactive Genesis workflow template,
    and writes `.github/workflows/private-auto-merge.yml`.

    This script only edits the local working tree. It never commits, pushes,
    opens a pull request, changes GitHub settings, or merges.

.PARAMETER ProjectRoot
    Generated project root. Defaults to the current directory.

.PARAMETER Force
    Replace an existing different workflow. Without this switch, a differing
    target is treated as an error.

.EXAMPLE
    ./scripts/delivery/Install-PrivateAutoMerge.ps1

.EXAMPLE
    ./scripts/delivery/Install-PrivateAutoMerge.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ProjectRoot = (Get-Location).Path,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = (Resolve-Path $ProjectRoot).Path
$contractPath = Join-Path $ProjectRoot '.github' 'genesis-delivery.json'
$templatePath = Join-Path $ProjectRoot '.genesis' 'delivery' 'private-auto-merge.yml'
$targetPath = Join-Path $ProjectRoot '.github' 'workflows' 'private-auto-merge.yml'

if (-not (Test-Path $contractPath)) {
    Write-Error "Delivery contract not found at '$contractPath'."
}
if (-not (Test-Path $templatePath)) {
    Write-Error "Private auto-merge template not found at '$templatePath'."
}

Write-Host "[1/3] Reading delivery contract"
$contract = Get-Content $contractPath -Raw | ConvertFrom-Json
$workflowNames = [System.Collections.Generic.List[string]]::new()
$seen = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)

function Add-WorkflowName {
    param([string]$Name)
    if ([string]::IsNullOrWhiteSpace($Name)) {
        Write-Error 'A required workflow has an empty display name.'
    }
    if ($seen.Add($Name)) {
        $workflowNames.Add($Name) | Out-Null
    }
}

Add-WorkflowName ([string]$contract.ciWorkflow)
Add-WorkflowName 'PR title'
Add-WorkflowName 'Review policy evaluator'

foreach ($workflow in @($contract.componentWorkflows)) {
    if (@($workflow.roles) -notcontains 'merge-gate') {
        continue
    }

    $workflowPath = Join-Path $ProjectRoot ([string]$workflow.path)
    if (-not (Test-Path $workflowPath)) {
        Write-Error "Required component workflow not found at '$workflowPath'."
    }
    $workflowText = Get-Content $workflowPath -Raw
    $nameMatch = [regex]::Match($workflowText, '(?m)^name:\s*(.+?)\r?$')
    if (-not $nameMatch.Success) {
        Write-Error "Required component workflow '$workflowPath' has no top-level name."
    }
    Add-WorkflowName ($nameMatch.Groups[1].Value.Trim().Trim('"', "'"))
}

Write-Host "[2/3] Rendering workflow listeners: $($workflowNames -join ', ')"
$template = Get-Content $templatePath -Raw
$marker = '      # __GENESIS_WORKFLOW_NAMES__'
if (-not $template.Contains($marker)) {
    Write-Error "Workflow template marker is missing from '$templatePath'."
}
$renderedNames = @(
    $workflowNames |
        ForEach-Object {
            $escapedName = $_.Replace("'", "''")
            "      - '$escapedName'"
        }
) -join "`n"
$rendered = $template.Replace($marker, $renderedNames)

if (Test-Path $targetPath) {
    $current = Get-Content $targetPath -Raw
    if ($current -ceq $rendered) {
        Write-Host "[3/3] Workflow already current: $targetPath"
        return [PSCustomObject]@{
            Path      = $targetPath
            Changed   = $false
            Workflows = @($workflowNames)
        }
    }
    if (-not $Force) {
        Write-Error "A different workflow already exists at '$targetPath'. Re-run with -Force to replace it."
    }
}

$changed = $false
if ($PSCmdlet.ShouldProcess($targetPath, 'Install private exact-SHA auto-merge workflow')) {
    $targetDir = Split-Path $targetPath -Parent
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    [IO.File]::WriteAllText(
        $targetPath,
        $rendered,
        [Text.UTF8Encoding]::new($false))
    Write-Host "[3/3] Installed workflow: $targetPath"
    $changed = $true
}

[PSCustomObject]@{
    Path      = $targetPath
    Changed   = $changed
    Workflows = @($workflowNames)
}
