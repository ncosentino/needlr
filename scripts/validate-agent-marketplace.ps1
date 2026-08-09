<#
.SYNOPSIS
    Validates the Needlr agent marketplace manifests, profiles, and shared skill.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$expectedVersion = '1.0.1'
$expectedAgents = @('application', 'integrations', 'source-generation')

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "Required JSON file '$Path' does not exist."
    }

    try {
        return Get-Content $Path -Raw | ConvertFrom-Json
    } catch {
        throw "JSON file '$Path' is invalid: $($_.Exception.Message)"
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', actual '$Actual'."
    }
}

$pluginPath = Join-Path $repoRoot 'plugin.json'
$claudePluginPath = Join-Path (Join-Path $repoRoot '.claude-plugin') 'plugin.json'
$marketplacePath = Join-Path (Join-Path $repoRoot '.claude-plugin') 'marketplace.json'

$plugin = Read-JsonFile -Path $pluginPath
$claudePlugin = Read-JsonFile -Path $claudePluginPath
$marketplace = Read-JsonFile -Path $marketplacePath

Assert-Equal -Expected 'needlr' -Actual $plugin.name -Message 'Root plugin name is incorrect.'
Assert-Equal -Expected $expectedVersion -Actual $plugin.version -Message 'Root plugin version is incorrect.'
Assert-Equal -Expected 'agents/' -Actual $plugin.agents -Message 'Root agent path is incorrect.'
if ('skills/' -notin @($plugin.skills)) {
    throw "Root plugin must include the 'skills/' path."
}

Assert-Equal -Expected $plugin.name -Actual $claudePlugin.name -Message 'Claude plugin name differs from the root plugin.'
Assert-Equal -Expected $expectedVersion -Actual $claudePlugin.version -Message 'Claude plugin version is incorrect.'
Assert-Equal -Expected 'ncosentino-needlr' -Actual $marketplace.name -Message 'Marketplace name is incorrect.'
Assert-Equal -Expected $expectedVersion -Actual $marketplace.metadata.version -Message 'Marketplace version is incorrect.'

$marketplacePlugins = @($marketplace.plugins)
Assert-Equal -Expected 1 -Actual $marketplacePlugins.Count -Message 'Marketplace must expose exactly one plugin.'
Assert-Equal -Expected 'needlr' -Actual $marketplacePlugins[0].name -Message 'Marketplace plugin name is incorrect.'
Assert-Equal -Expected $expectedVersion -Actual $marketplacePlugins[0].version -Message 'Marketplace plugin version is incorrect.'
Assert-Equal -Expected './' -Actual $marketplacePlugins[0].source -Message 'Marketplace plugin source is incorrect.'

$agentsDir = Join-Path $repoRoot 'agents'
$agentFiles = @(Get-ChildItem $agentsDir -Filter '*.agent.md' -File | Sort-Object Name)
Assert-Equal -Expected $expectedAgents.Count -Actual $agentFiles.Count -Message 'Unexpected agent profile count.'

$actualAgents = @()
$forbiddenContent = @(
    'github.devleader.ca',
    'NexusLabs.Needlr.AgentFramework',
    'NexusLabs.Needlr.Copilot',
    'NDLRMAF'
)

foreach ($agentFile in $agentFiles) {
    $content = Get-Content $agentFile.FullName -Raw
    if ($content.Length -gt 5000) {
        throw "Agent '$($agentFile.Name)' exceeds the 5,000-character thin-profile limit."
    }

    if ($content -notmatch '(?ms)\A---\s*\r?\n(?<frontmatter>.*?)\r?\n---') {
        throw "Agent '$($agentFile.Name)' has malformed front matter."
    }

    $frontmatter = $Matches['frontmatter']
    if ($frontmatter -notmatch '(?m)^name:\s*(?<name>[a-z0-9-]+)\s*$') {
        throw "Agent '$($agentFile.Name)' has no valid kebab-case name."
    }

    $agentName = $Matches['name']
    $actualAgents += $agentName
    Assert-Equal -Expected "$agentName.agent.md" -Actual $agentFile.Name -Message 'Agent file name does not match its profile name.'

    if ($frontmatter -notmatch '(?m)^description:\s*>') {
        throw "Agent '$agentName' must have a folded routing description."
    }
    if (-not $content.Contains('needlr-research')) {
        throw "Agent '$agentName' does not invoke the shared needlr-research skill."
    }

    foreach ($forbidden in $forbiddenContent) {
        if ($content.Contains($forbidden)) {
            throw "Agent '$agentName' contains stale identity '$forbidden'."
        }
    }
}

$sortedActual = @($actualAgents | Sort-Object)
for ($index = 0; $index -lt $expectedAgents.Count; $index++) {
    Assert-Equal -Expected $expectedAgents[$index] -Actual $sortedActual[$index] -Message 'Agent roster differs from the marketplace contract.'
}
if (@($actualAgents | Group-Object | Where-Object Count -gt 1).Count -gt 0) {
    throw 'Agent profile names must be unique.'
}

$researchSkillPath = Join-Path (Join-Path (Join-Path $repoRoot 'skills') 'needlr-research') 'SKILL.md'
if (-not (Test-Path $researchSkillPath)) {
    throw 'The shared needlr-research skill is missing.'
}
$researchSkill = Get-Content $researchSkillPath -Raw
foreach ($requiredPhrase in @(
    'Needlr source checkout',
    'Consumer repository with Needlr references',
    'Greenfield planning without a reference',
    'current web research')) {
    if (-not $researchSkill.Contains($requiredPhrase)) {
        throw "The shared research skill is missing required context '$requiredPhrase'."
    }
}

$readmePath = Join-Path $agentsDir 'README.md'
if (-not (Test-Path $readmePath)) {
    throw 'Agent marketplace installation guidance is missing.'
}
$readme = Get-Content $readmePath -Raw
foreach ($command in @(
    'copilot plugin marketplace add ncosentino/needlr',
    'copilot plugin install needlr@ncosentino-needlr')) {
    if (-not $readme.Contains($command)) {
        throw "Agent marketplace guidance is missing '$command'."
    }
}

Write-Host 'Agent marketplace validation passed.' -ForegroundColor Green
