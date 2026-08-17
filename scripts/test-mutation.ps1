<#
.SYNOPSIS
    Validates Needlr's bounded, ephemeral pull-request mutation contract.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolManifestPath = Join-Path $repoRoot '.config\dotnet-tools.json'
$manifestPath = Join-Path $PSScriptRoot 'mutation\scopes.json'
$runnerPath = Join-Path $PSScriptRoot 'run-mutation-tests.ps1'
$classifierPath = Join-Path $PSScriptRoot 'get-mutation-scope.ps1'
$workflowPath = Join-Path $repoRoot '.github\workflows\mutation-testing.yml'
$deliveryPath = Join-Path $repoRoot '.github\genesis-delivery.json'

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-PowerShellSyntax {
    param([Parameter(Mandatory)][string]$Path)

    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$parseErrors)
    Assert-Condition `
        -Condition ($parseErrors.Count -eq 0) `
        -Message "$Path has PowerShell parse errors: $($parseErrors.Message -join '; ')"
}

foreach ($path in @(
        $toolManifestPath,
        $manifestPath,
        $runnerPath,
        $classifierPath,
        $workflowPath,
        $deliveryPath)) {
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $path -PathType Leaf) `
        -Message "Mutation-testing surface is missing: $path"
}

$toolManifest = Get-Content -LiteralPath $toolManifestPath -Raw | ConvertFrom-Json
$strykerTool = $toolManifest.tools.'dotnet-stryker'
Assert-Condition `
    -Condition ([string]$strykerTool.version -ceq '4.16.0') `
    -Message 'dotnet-stryker must be pinned to 4.16.0.'
Assert-Condition `
    -Condition ((@($strykerTool.commands) -join ',') -ceq 'dotnet-stryker') `
    -Message 'The local tool manifest must expose the dotnet-stryker command.'

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-Condition `
    -Condition ([int]$manifest.limits.maxScopesPerPullRequest -eq 2) `
    -Message 'Pull requests must run at most two mutation scopes.'
Assert-Condition `
    -Condition ([int]$manifest.limits.maxFilesPerScope -eq 5) `
    -Message 'Each scope must mutate at most five changed files.'
Assert-Condition `
    -Condition ([int]$manifest.limits.timeoutMinutes -eq 10) `
    -Message 'Each mutation job must have a ten-minute ceiling.'
Assert-Condition `
    -Condition ([int]$manifest.limits.maxParallel -eq 2) `
    -Message 'Mutation workflow parallelism must remain capped at two.'
Assert-Condition `
    -Condition ([string]$manifest.defaults.testRunner -ceq 'mtp') `
    -Message 'Mutation scopes must use MTP for xUnit v3.'
Assert-Condition `
    -Condition ([int]$manifest.defaults.thresholds.break -eq 0) `
    -Message 'Mutation score must remain nonblocking.'
Assert-Condition `
    -Condition (
        @($manifest.defaults.reporters) -contains 'json' -and
        @($manifest.defaults.reporters) -contains 'markdown' -and
        @($manifest.defaults.reporters) -notcontains 'dashboard') `
    -Message 'Mutation reporting must remain local and dashboard-free.'

$scopeNames = @($manifest.scopes | ForEach-Object { [string]$_.name })
Assert-Condition `
    -Condition ($scopeNames.Count -eq 14) `
    -Message 'The mutation manifest must cover the fourteen directly tested runtime packages.'
Assert-Condition `
    -Condition (($scopeNames | Sort-Object -Unique).Count -eq $scopeNames.Count) `
    -Message 'Mutation scope names must be unique.'
Assert-Condition `
    -Condition (
        (@($manifest.scopes | ForEach-Object { [int]$_.priority }) |
            Sort-Object -Unique).Count -eq $scopeNames.Count) `
    -Message 'Mutation scope priorities must be unique.'

foreach ($scope in $manifest.scopes) {
    $workingDirectory = Join-Path $repoRoot ([string]$scope.workingDirectory)
    $sourceRoot = Join-Path $repoRoot ([string]$scope.sourceRoot)
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $workingDirectory -PathType Container) `
        -Message "Mutation working directory is missing for '$($scope.name)'."
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $sourceRoot -PathType Container) `
        -Message "Mutation source root is missing for '$($scope.name)'."
    Assert-Condition `
        -Condition (
            @($scope.priorityFiles).Count -gt 0 -and
            @($scope.priorityFiles).Count -le [int]$manifest.limits.maxFilesPerScope) `
        -Message "Mutation priority files are invalid for '$($scope.name)'."

    $testProjectName = (Split-Path -Leaf $workingDirectory) + '.csproj'
    $testProject = Join-Path $workingDirectory $testProjectName
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $testProject -PathType Leaf) `
        -Message "Mutation test project is missing for '$($scope.name)'."
    $testProjectContent = Get-Content -LiteralPath $testProject -Raw
    Assert-Condition `
        -Condition (
            $testProjectContent -match
                [regex]::Escape([string]$scope.project)) `
        -Message "Mutation test project does not directly reference '$($scope.project)'."

    foreach ($file in $scope.priorityFiles) {
        Assert-Condition `
            -Condition (
                Test-Path `
                    -LiteralPath (Join-Path $sourceRoot ([string]$file)) `
                    -PathType Leaf) `
            -Message "Mutation priority file is missing: $($scope.sourceRoot)/$file"
    }
}

Assert-PowerShellSyntax -Path $runnerPath
Assert-PowerShellSyntax -Path $classifierPath

$workflow = Get-Content -LiteralPath $workflowPath -Raw
Assert-Condition `
    -Condition (
        $workflow -match '(?m)^  pull_request:\r?$' -and
        $workflow -notmatch
            '(?m)^  (?:push|schedule|workflow_dispatch|workflow_run|repository_dispatch):') `
    -Message 'Mutation testing must remain pull-request-only.'
Assert-Condition `
    -Condition ($workflow -notmatch 'actions/upload-artifact|dashboard|baseline|cache') `
    -Message 'Mutation testing must not persist reports or baselines.'
Assert-Condition `
    -Condition ($workflow -notmatch 'continue-on-error') `
    -Message 'Mutation tool, build, and initial-test failures must remain visible.'
Assert-Condition `
    -Condition ($workflow -match 'github\.event\.pull_request\.draft == false') `
    -Message 'Mutation testing must not occupy runners for draft pull requests.'
Assert-Condition `
    -Condition ($workflow -match 'head\.repo\.full_name != github\.repository') `
    -Message 'External fork mutation runs must use GitHub-hosted infrastructure.'
Assert-Condition `
    -Condition (
        $workflow -match 'max-parallel:\s*2' -and
        $workflow -match 'timeout-minutes:\s*10') `
    -Message 'Mutation workflow execution bounds have drifted.'
Assert-Condition `
    -Condition (
        ([regex]::Matches($workflow, 'fetch-depth:\s*0')).Count -eq 2) `
    -Message 'Every mutation workflow checkout must retain full NBGV/SourceLink history.'

$runner = Get-Content -LiteralPath $runnerPath -Raw
Assert-Condition `
    -Condition ($runner -match '2>&1\s*\|\s*[\r\n\s]*Out-Host') `
    -Message 'Stryker output must remain visible when the native command fails.'
Assert-Condition `
    -Condition (
        $runner -match [regex]::Escape('target = $SinceTarget')) `
    -Message 'Pull-request mutation must support Stryker changed-code analysis.'
Assert-Condition `
    -Condition ($runner -match 'Actionable mutants') `
    -Message 'Mutation summaries must expose survivors and uncovered mutants.'

$coreOnly = (
    & $classifierPath `
        -ChangedPaths @('src/NexusLabs.Needlr/TypeExtensions.cs') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        $coreOnly.run_required -and
        (@($coreOnly.selected_scopes) -join ',') -ceq 'core' -and
        $coreOnly.matrix.include[0].mutateFiles[0] -ceq 'TypeExtensions.cs') `
    -Message 'Core source changes must select their exact changed file.'

$priorityTrim = (
    & $classifierPath `
        -ChangedPaths @(
            'src/NexusLabs.Needlr/ServiceCollectionExtensions.cs',
            'src/NexusLabs.Needlr/ServiceProviderExtensions.cs',
            'src/NexusLabs.Needlr/ServiceCollectionVerificationExtensions.cs',
            'src/NexusLabs.Needlr/LifetimeMismatchExtensions.cs',
            'src/NexusLabs.Needlr/DumpExtensions.cs',
            'src/NexusLabs.Needlr/TypeExtensions.cs') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        @($priorityTrim.matrix.include[0].mutateFiles).Count -eq 5 -and
        @($priorityTrim.omitted_files).Count -eq 1) `
    -Message 'Changed source files must be trimmed and disclosed at the five-file limit.'

$deletedSource = (
    & $classifierPath `
        -ChangedPaths @('src/NexusLabs.Needlr/RemovedService.cs') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        $deletedSource.matrix.include[0].mutateFiles[0] -ceq
            'ServiceCollectionExtensions.cs' -and
        @($deletedSource.omitted_files) -contains
            'core::src/NexusLabs.Needlr/RemovedService.cs') `
    -Message 'Deleted source files must fall back to priority files and remain disclosed.'

$scopeTrim = (
    & $classifierPath `
        -ChangedPaths @(
            'src/NexusLabs.Needlr/TypeExtensions.cs',
            'src/NexusLabs.Needlr.Injection/SyringeExtensions.cs',
            'src/NexusLabs.Needlr.Hosting/HostFactory.cs') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        (@($scopeTrim.selected_scopes) -join ',') -ceq 'core,injection' -and
        (@($scopeTrim.omitted_scopes) -join ',') -ceq 'hosting') `
    -Message 'Changed scopes must be selected by committed priority and capped at two.'

$shared = (
    & $classifierPath `
        -ChangedPaths @('scripts/mutation/scopes.json') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        (@($shared.selected_scopes) -join ',') -ceq 'core,injection' -and
        $shared.matrix.include[0].mode -ceq 'full' -and
        $shared.matrix.include[1].mode -ceq 'full') `
    -Message 'Shared mutation changes must select the two highest-priority representative scopes.'

$generatorChange = (
    & $classifierPath `
        -ChangedPaths @('src/NexusLabs.Needlr.Generators/GeneratorHelpers.cs') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        (@($generatorChange.selected_scopes) -join ',') -ceq 'carter' -and
        $generatorChange.matrix.include[0].mode -ceq 'full') `
    -Message 'Generator changes must select the source-generated Carter consumer.'

$delivery = Get-Content -LiteralPath $deliveryPath -Raw | ConvertFrom-Json
$component = @($delivery.componentWorkflows) |
    Where-Object path -eq '.github/workflows/mutation-testing.yml' |
    Select-Object -First 1
Assert-Condition `
    -Condition ($null -ne $component) `
    -Message 'The delivery contract must declare the mutation workflow.'
Assert-Condition `
    -Condition (@($component.requiredChecks).Count -eq 0) `
    -Message 'Mutation testing must remain outside required branch checks.'

Write-Host 'Mutation testing contract validation passed.' -ForegroundColor Green
