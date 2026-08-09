#Requires -Version 7.0
<#
.SYNOPSIS
    Inventory repository-declared validation and build surfaces.

.PARAMETER ProjectRoot
    Repository root to inspect. Defaults to the project containing this script.

.PARAMETER Json
    Emit JSON instead of a PSCustomObject.

.OUTPUTS
    A deterministic inventory of package scripts, .NET files, language manifests,
    workflows, and Genesis delivery metadata.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path $ProjectRoot).Path

function Get-RelativePath {
    param([string]$Path)
    return [IO.Path]::GetRelativePath($ProjectRoot, $Path).Replace('\', '/')
}

function Get-ProjectFiles {
    $excludedDirectories = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @(
        '.git',
        '.next',
        '.nuxt',
        '.output',
        'bin',
        'build',
        'coverage',
        'dist',
        'node_modules',
        'obj',
        'site',
        'target'
    )) {
        [void]$excludedDirectories.Add($name)
    }

    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($ProjectRoot)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($item in (Get-ChildItem $directory -Force)) {
            if ($item.PSIsContainer) {
                if (
                    -not $excludedDirectories.Contains($item.Name) -and
                    -not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
                ) {
                    $pending.Push($item.FullName)
                }
            } else {
                $item
            }
        }
    }
}

$projectFiles = @(Get-ProjectFiles)
$packageManifests = @(
    $projectFiles |
        Where-Object Name -CEQ 'package.json' |
        Sort-Object FullName |
        ForEach-Object {
            $manifest = Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            $scripts = [ordered]@{}
            if ($manifest.PSObject.Properties['scripts']) {
                foreach ($script in @($manifest.scripts.PSObject.Properties | Sort-Object Name)) {
                    $scripts[$script.Name] = [string]$script.Value
                }
            }
            [PSCustomObject]@{
                path    = Get-RelativePath $_.FullName
                scripts = $scripts
            }
        }
)

$dotnetSolutions = @(
    $projectFiles |
        Where-Object {
            $_.Extension -in @('.sln', '.slnx')
        } |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePath $_.FullName }
)
$dotnetProjects = @(
    $projectFiles |
        Where-Object Extension -CEQ '.csproj' |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePath $_.FullName }
)

$manifestKinds = [ordered]@{
    'go.mod'       = 'go'
    'Cargo.toml'   = 'rust'
    'pubspec.yaml' = 'dart-flutter'
    'hugo.toml'    = 'hugo'
    'pyproject.toml' = 'python'
    'Makefile'     = 'make'
}
$languageManifests = @(
    foreach ($entry in $manifestKinds.GetEnumerator()) {
        $projectFiles |
            Where-Object Name -CEQ $entry.Key |
            ForEach-Object {
                [PSCustomObject]@{
                    path = Get-RelativePath $_.FullName
                    kind = $entry.Value
                }
            }
    }
) | Sort-Object path, kind

$workflowRoot = Join-Path $ProjectRoot '.github' 'workflows'
$workflows = @(
    if (Test-Path $workflowRoot -PathType Container) {
        Get-ChildItem $workflowRoot -File |
            Where-Object Extension -in @('.yml', '.yaml') |
            Sort-Object Name |
            ForEach-Object {
                $content = Get-Content $_.FullName -Raw -Encoding UTF8
                $name = [regex]::Match($content, '(?m)^name:\s*(.+?)\s*$')
                [PSCustomObject]@{
                    path = Get-RelativePath $_.FullName
                    name = if ($name.Success) { $name.Groups[1].Value.Trim("'`"") } else { $_.BaseName }
                }
            }
    }
)

$deliveryPath = Join-Path $ProjectRoot '.github' 'genesis-delivery.json'
$delivery =
    if (Test-Path $deliveryPath -PathType Leaf) {
        Get-Content $deliveryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    } else {
        $null
    }

$inventory = [PSCustomObject]@{
    projectRoot       = $ProjectRoot
    packageManifests  = $packageManifests
    dotnetSolutions   = $dotnetSolutions
    dotnetProjects    = $dotnetProjects
    languageManifests = $languageManifests
    workflows         = $workflows
    delivery          = $delivery
}

if ($Json) {
    $inventory | ConvertTo-Json -Depth 12
} else {
    $inventory
}
