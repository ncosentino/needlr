#Requires -Version 7.0
<#
.SYNOPSIS
    Plans or configures PR-first GitHub delivery for a generated project.

.DESCRIPTION
    Reads `.github/genesis-delivery.json`, probes repository settings and branch
    protection capability, active default-branch rules, and existing protection,
    then selects native protected auto-merge or the private exact-SHA
    workflow-run lane.

    The default is plan-only. `-Apply` is required for GitHub writes. This
    script never commits, pushes, opens a pull request, or merges. Active
    rulesets and incompatible existing protection require manual reconciliation.

.PARAMETER ProjectRoot
    Generated project root. Defaults to the current directory.

.PARAMETER Repository
    Optional `owner/repository`. Defaults to the current gh repository.

.PARAMETER DeliveryMode
    Auto, Native, or WorkflowRun. Auto selects Native whenever protection is
    available and WorkflowRun only for the known private-plan denial.

.PARAMETER CheckSha
    Commit whose check runs establish the required GitHub check contexts.
    Defaults to the remote default branch tip.

.PARAMETER Apply
    Apply the planned GitHub settings. Without this switch, no writes occur.

.PARAMETER AllowUnprotectedPrivate
    Required with `-Apply` when WorkflowRun is selected.

.PARAMETER ReviewPolicy
    None, or CopilotOneApproval. CopilotOneApproval requires one trusted human
    approval on the current SHA before a ready Copilot-authored PR can merge.

.PARAMETER SnapshotPath
    Optional offline repository snapshot used by Genesis contract tests.

.PARAMETER GhCommand
    gh executable path. Defaults to `gh`.

.EXAMPLE
    ./scripts/delivery/Configure-GitHubDelivery.ps1

.EXAMPLE
    ./scripts/delivery/Configure-GitHubDelivery.ps1 -Apply
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ProjectRoot = (Get-Location).Path,
    [string]$Repository,
    [ValidateSet('Auto', 'Native', 'WorkflowRun')]
    [string]$DeliveryMode = 'Auto',
    [string]$CheckSha,
    [switch]$Apply,
    [switch]$AllowUnprotectedPrivate,
    [ValidateSet('None', 'CopilotOneApproval')]
    [string]$ReviewPolicy = 'None',
    [string]$SnapshotPath,
    [string]$GhCommand = 'gh'
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = (Resolve-Path $ProjectRoot).Path
$contractPath = Join-Path $ProjectRoot '.github' 'genesis-delivery.json'
$installerPath = Join-Path $ProjectRoot 'scripts' 'delivery' 'Install-PrivateAutoMerge.ps1'

if (-not (Test-Path $contractPath)) {
    Write-Error "Delivery contract not found at '$contractPath'."
}

$contract = Get-Content $contractPath -Raw | ConvertFrom-Json
$requiredChecks = @($contract.requiredChecks | ForEach-Object { [string]$_ })
$draftMode = [string]$contract.draftValidation.hostedDefault

function Invoke-GhJson {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = & $GhCommand @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
    if ([string]::IsNullOrWhiteSpace(($output -join "`n"))) {
        return $null
    }
    return (($output -join "`n") | ConvertFrom-Json)
}

function Get-LiveSnapshot {
    & $GhCommand auth status *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI authentication is required. Run gh auth login.'
    }

    $resolvedRepository =
        if ([string]::IsNullOrWhiteSpace($Repository)) {
            $repoView = Invoke-GhJson -Arguments @(
                'repo', 'view', '--json', 'nameWithOwner')
            [string]$repoView.nameWithOwner
        } else {
            $Repository
        }
    if ($resolvedRepository -notmatch '^[^/]+/[^/]+$') {
        throw "Repository '$resolvedRepository' must be owner/name."
    }

    $repositoryState = Invoke-GhJson -Arguments @(
        'api', '-H', 'X-GitHub-Api-Version: 2026-03-10',
        "repos/$resolvedRepository")
    $defaultBranch = [string]$repositoryState.default_branch
    $branchSha =
        if ([string]::IsNullOrWhiteSpace($CheckSha)) {
            $commit = Invoke-GhJson -Arguments @(
                'api', '-H', 'X-GitHub-Api-Version: 2026-03-10',
                "repos/$resolvedRepository/commits/$defaultBranch")
            [string]$commit.sha
        } else {
            $CheckSha
        }

    $protectionStatus = 'unknown'
    $protection = $null
    $protectionOutput = & $GhCommand api `
        -H 'X-GitHub-Api-Version: 2026-03-10' `
        "repos/$resolvedRepository/branches/$defaultBranch/protection" 2>&1
    if ($LASTEXITCODE -eq 0) {
        $protectionStatus = 'protected'
        $protection = ($protectionOutput -join "`n") | ConvertFrom-Json
    } else {
        $message = $protectionOutput -join "`n"
        if ($message -match 'HTTP 404') {
            $protectionStatus = 'available-unprotected'
        } elseif (
            [bool]$repositoryState.private -and
            $message -match '(?i)upgrade.*pro|make this repository public') {
            $protectionStatus = 'unavailable-private-plan'
        } else {
            throw "Unable to determine branch protection capability: $message"
        }
    }

    $activeRulesStatus = 'available'
    $activeRules = @()
    $activeRulesOutput = & $GhCommand api --paginate --slurp `
        -H 'X-GitHub-Api-Version: 2026-03-10' `
        "repos/$resolvedRepository/rules/branches/${defaultBranch}?per_page=100" 2>&1
    if ($LASTEXITCODE -eq 0) {
        $activeRulePages = ($activeRulesOutput -join "`n") | ConvertFrom-Json
        $activeRules = @(
            foreach ($page in @($activeRulePages)) {
                @($page)
            }
        )
    } else {
        $message = $activeRulesOutput -join "`n"
        if (
            [bool]$repositoryState.private -and
            $message -match '(?i)upgrade.*pro|make this repository public'
        ) {
            $activeRulesStatus = 'unavailable-private-plan'
        } else {
            throw "Unable to inspect active default-branch rules: $message"
        }
    }

    $checkRuns = Invoke-GhJson -Arguments @(
        'api', '--paginate', '--slurp',
        "repos/$resolvedRepository/commits/$branchSha/check-runs?filter=latest&per_page=100")
    $flattenedChecks = @(
        foreach ($page in @($checkRuns)) {
            @($page.check_runs)
        }
    )

    $privateWorkflowPresent = $false
    $workflowOutput = & $GhCommand api `
        -H 'X-GitHub-Api-Version: 2026-03-10' `
        "repos/$resolvedRepository/contents/.github/workflows/private-auto-merge.yml?ref=$defaultBranch" 2>&1
    if ($LASTEXITCODE -eq 0) {
        $privateWorkflowPresent = $true
    } elseif (($workflowOutput -join "`n") -notmatch 'HTTP 404') {
        throw "Unable to inspect private auto-merge workflow: $($workflowOutput -join "`n")"
    }

    return [PSCustomObject]@{
        repository             = $resolvedRepository
        visibility             = [string]$repositoryState.visibility
        private                = [bool]$repositoryState.private
        defaultBranch          = $defaultBranch
        checkSha               = $branchSha
        protectionStatus       = $protectionStatus
        protection             = $protection
        activeRulesStatus      = $activeRulesStatus
        activeRules            = @($activeRules)
        checkRuns              = @($flattenedChecks)
        privateWorkflowPresent = $privateWorkflowPresent
    }
}

$snapshot =
    if ([string]::IsNullOrWhiteSpace($SnapshotPath)) {
        Get-LiveSnapshot
    } else {
        Get-Content (Resolve-Path $SnapshotPath) -Raw | ConvertFrom-Json
    }

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $Repository = [string]$snapshot.repository
}

$selectedMode =
    if ($DeliveryMode -ne 'Auto') {
        $DeliveryMode
    } elseif ([string]$snapshot.protectionStatus -eq 'unavailable-private-plan') {
        'WorkflowRun'
    } else {
        'Native'
    }

if ($selectedMode -eq 'WorkflowRun' -and -not [bool]$snapshot.private) {
    throw 'WorkflowRun delivery is only supported for private repositories.'
}
if ($selectedMode -eq 'Native' -and [string]$snapshot.protectionStatus -eq 'unavailable-private-plan') {
    throw 'Native delivery was requested but branch protection is unavailable for this private repository.'
}

$successfulCheckNames = @(
    @($snapshot.checkRuns) |
        Where-Object { [string]$_.conclusion -eq 'success' } |
        ForEach-Object { [string]$_.name } |
        Sort-Object -Unique
)
$missingChecks = @($requiredChecks | Where-Object { $_ -notin $successfulCheckNames })

$operations = [System.Collections.Generic.List[string]]::new()
$status = 'ready'
$nextAction = $null
$activeRules = @($snapshot.activeRules)

if ($activeRules.Count -gt 0) {
    $status = 'manual'
    $nextAction = 'Active repository or inherited rulesets apply to the default branch. Reconcile them manually; Genesis will not mutate overlapping policy.'
} elseif ($selectedMode -eq 'Native' -and $missingChecks.Count -gt 0) {
    $status = 'deferred'
    $nextAction = "Run the required workflows, then rerun with -CheckSha <sha>. Missing: $($missingChecks -join ', ')."
} elseif ($selectedMode -eq 'WorkflowRun' -and -not [bool]$snapshot.privateWorkflowPresent) {
    $status = 'deferred'
    $nextAction = 'Install and merge .github/workflows/private-auto-merge.yml, then rerun.'
    if (Test-Path $installerPath) {
        if ($Apply) {
            & $installerPath -ProjectRoot $ProjectRoot -Confirm:$false | Out-Null
        } else {
            & $installerPath -ProjectRoot $ProjectRoot -WhatIf | Out-Null
        }
    }
}

if ($status -eq 'ready') {
    $reviewPolicyValue =
        if ($ReviewPolicy -eq 'CopilotOneApproval') {
            'copilot-one-approval'
        } else {
            'none'
        }
    $operations.Add('Configure squash-only repository merge settings') | Out-Null
    $operations.Add('Set read-only Actions workflow permissions') | Out-Null
    if (-not [bool]$snapshot.private) {
        $operations.Add('Require workflow approval for all external contributors') | Out-Null
    }
    $operations.Add("Set CI_DRAFT_MODE=$draftMode") | Out-Null
    $operations.Add("Set GENESIS_REVIEW_POLICY=$reviewPolicyValue") | Out-Null
    if ($selectedMode -eq 'Native') {
        if ([string]$snapshot.protectionStatus -eq 'protected') {
            $existingChecks = @($snapshot.protection.required_status_checks.checks)
            $missingProtectionChecks = @(
                $requiredChecks |
                    Where-Object {
                        $requiredCheck = $_
                        -not ($existingChecks | Where-Object {
                            [string]$_.context -ceq $requiredCheck -and
                            [int]$_.app_id -eq 15368
                        })
                    }
            )
            $reviewBypasses = $snapshot.protection.required_pull_request_reviews.bypass_pull_request_allowances
            $reviewBypassActors = @(
                if ($null -ne $reviewBypasses) {
                    @($reviewBypasses.users) | Where-Object { $null -ne $_ }
                    @($reviewBypasses.teams) | Where-Object { $null -ne $_ }
                    @($reviewBypasses.apps) | Where-Object { $null -ne $_ }
                }
            )
            $hasReviewBypasses = $reviewBypassActors.Count -gt 0
            $compatibleProtection = (
                $missingProtectionChecks.Count -eq 0 -and
                [bool]$snapshot.protection.required_status_checks.strict -and
                [bool]$snapshot.protection.enforce_admins.enabled -and
                $null -ne $snapshot.protection.required_pull_request_reviews -and
                -not $hasReviewBypasses -and
                [bool]$snapshot.protection.required_conversation_resolution.enabled -and
                -not [bool]$snapshot.protection.allow_force_pushes.enabled -and
                -not [bool]$snapshot.protection.allow_deletions.enabled
            )
            if (-not $compatibleProtection) {
                throw 'Existing branch protection differs from the Genesis safety contract; update it manually rather than allowing Genesis to overwrite it.'
            }
            $operations.Add('Keep existing compatible branch protection') | Out-Null
        } else {
            $operations.Add('Create strict default-branch protection with required checks') | Out-Null
        }
        $operations.Add('Enable native repository auto-merge') | Out-Null
        $operations.Add('Set GENESIS_DELIVERY_MODEL=native') | Out-Null
    } else {
        $operations.Add('Set GENESIS_DELIVERY_MODEL=workflow-run') | Out-Null
    }
}

$result = [ordered]@{
    schemaVersion    = 1
    repository       = [string]$snapshot.repository
    defaultBranch    = [string]$snapshot.defaultBranch
    visibility       = [string]$snapshot.visibility
    selectedMode     = $selectedMode
    status           = $status
    requiredChecks   = @($requiredChecks)
    missingChecks    = @($missingChecks)
    operations       = @($operations)
    draftMode        = $draftMode
    reviewPolicy     = $ReviewPolicy
    activeRulesStatus = [string]$snapshot.activeRulesStatus
    activeRules      = @($activeRules)
    nextAction       = $nextAction
}

if (-not $Apply -or $status -ne 'ready') {
    [PSCustomObject]$result
    return
}
if ($selectedMode -eq 'WorkflowRun' -and -not $AllowUnprotectedPrivate) {
    throw '-AllowUnprotectedPrivate is required to apply workflow-run delivery.'
}
if (-not $PSCmdlet.ShouldProcess(
    $Repository,
    "Apply Genesis $selectedMode delivery settings, variables, and local default-branch configuration"
)) {
    $result.status = if ($WhatIfPreference) { 'what-if' } else { 'cancelled' }
    [PSCustomObject]$result
    return
}

Write-Host "[1/3] Applying squash-only repository settings"
$autoMergeValue = if ($selectedMode -eq 'Native') { 'true' } else { 'false' }
& $GhCommand api --method PATCH `
    -H 'X-GitHub-Api-Version: 2026-03-10' `
    "repos/$Repository" `
    -F allow_squash_merge=true `
    -F allow_merge_commit=false `
    -F allow_rebase_merge=false `
    -F "allow_auto_merge=$autoMergeValue" `
    -F delete_branch_on_merge=true `
    -f squash_merge_commit_title=PR_TITLE `
    -f squash_merge_commit_message=PR_BODY *> $null
if ($LASTEXITCODE -ne 0) { throw 'Failed to configure repository merge settings.' }

Write-Host "[2/3] Applying delivery model '$selectedMode'"
if ($selectedMode -eq 'Native' -and [string]$snapshot.protectionStatus -ne 'protected') {
    $checks = @(
        $requiredChecks | ForEach-Object {
            [ordered]@{ context = $_; app_id = 15368 }
        }
    )
    $protectionBody = [ordered]@{
        required_status_checks = [ordered]@{ strict = $true; checks = $checks }
        enforce_admins = $true
        required_pull_request_reviews = [ordered]@{
            dismiss_stale_reviews = $false
            require_code_owner_reviews = $false
            required_approving_review_count = 0
            require_last_push_approval = $false
        }
        restrictions = $null
        required_linear_history = $false
        allow_force_pushes = $false
        allow_deletions = $false
        block_creations = $false
        required_conversation_resolution = $true
        lock_branch = $false
        allow_fork_syncing = $false
    }
    $protectionBody | ConvertTo-Json -Depth 8 -Compress |
        & $GhCommand api --method PUT `
            -H 'X-GitHub-Api-Version: 2026-03-10' `
            "repos/$Repository/branches/$($snapshot.defaultBranch)/protection" `
            --input - *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to configure branch protection.' }
}

$workflowPermissionsBody = [ordered]@{
    default_workflow_permissions = 'read'
    can_approve_pull_request_reviews = $false
}
$workflowPermissionsBody | ConvertTo-Json -Compress |
    & $GhCommand api --method PUT `
        -H 'X-GitHub-Api-Version: 2026-03-10' `
        "repos/$Repository/actions/permissions/workflow" `
        --input - *> $null
if ($LASTEXITCODE -ne 0) { throw 'Failed to configure Actions workflow permissions.' }

if (-not [bool]$snapshot.private) {
    [ordered]@{ approval_policy = 'all_external_contributors' } |
        ConvertTo-Json -Compress |
        & $GhCommand api --method PUT `
            -H 'X-GitHub-Api-Version: 2026-03-10' `
            "repos/$Repository/actions/permissions/fork-pr-contributor-approval" `
            --input - *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to configure public fork workflow approval.' }
}

$modelValue = if ($selectedMode -eq 'Native') { 'native' } else { 'workflow-run' }
& $GhCommand variable set GENESIS_DELIVERY_MODEL `
    --repo $Repository `
    --body $modelValue *> $null
if ($LASTEXITCODE -ne 0) { throw 'Failed to set GENESIS_DELIVERY_MODEL.' }

& $GhCommand variable set GENESIS_REVIEW_POLICY `
    --repo $Repository `
    --body $reviewPolicyValue *> $null
if ($LASTEXITCODE -ne 0) { throw 'Failed to set GENESIS_REVIEW_POLICY.' }

& $GhCommand variable set CI_DRAFT_MODE `
    --repo $Repository `
    --body $draftMode *> $null
if ($LASTEXITCODE -ne 0) { throw 'Failed to set CI_DRAFT_MODE.' }

Write-Host "[3/3] Recording default branch locally"
git -C $ProjectRoot config genesis.defaultBranch ([string]$snapshot.defaultBranch)

$result.status = 'applied'
[PSCustomObject]$result
