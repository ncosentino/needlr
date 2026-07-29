<#
.SYNOPSIS
    Validates Needlr release candidate packaging, manifest creation, and verification.

.DESCRIPTION
    Exercises pack-release-packages.ps1 project selection plus the write and verify
    halves of the release manifest contract, including the tampering, substitution,
    and mismatch cases that must block publication.
#>

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$packScript = Join-Path $scriptDir 'pack-release-packages.ps1'
$writeScript = Join-Path $scriptDir 'write-release-manifest.ps1'
$verifyScript = Join-Path $scriptDir 'verify-release-manifest.ps1'

$testVersion = '9.9.9-test.1'
$testSha = '0123456789abcdef0123456789abcdef01234567'
$testRunId = '4242'
$testValidatedCiRunId = '1717'
$workspaces = [System.Collections.Generic.List[string]]::new()

function New-Candidate {
    param([string[]]$PackageNames)

    $path = Join-Path ([System.IO.Path]::GetTempPath()) "needlr-release-candidate-$([System.IO.Path]::GetRandomFileName())"
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    $workspaces.Add($path)

    foreach ($name in $PackageNames) {
        Set-Content -LiteralPath (Join-Path $path $name) -Value "content of $name" -NoNewline
    }

    return $path
}

function New-VerifiedCandidate {
    $path = New-Candidate -PackageNames @(
        "NexusLabs.Needlr.$testVersion.nupkg",
        "NexusLabs.Needlr.$testVersion.snupkg",
        "NexusLabs.Needlr.Injection.$testVersion.nupkg"
    )

    & $writeScript `
        -PackageDirectory $path `
        -Version $testVersion `
        -PackageVersion $testVersion `
        -SourceSha $testSha `
        -ProducingRunId $testRunId `
        -ProducingWorkflow 'ci.yml' `
        -ValidatedCiRunId $testValidatedCiRunId 6>&1 | Out-Null

    return $path
}

function New-DotnetShimDirectory {
    $path = Join-Path ([System.IO.Path]::GetTempPath()) "needlr-release-shim-$([System.IO.Path]::GetRandomFileName())"
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    $workspaces.Add($path)

    if ($IsWindows) {
        Set-Content -LiteralPath (Join-Path $path 'dotnet.cmd') -Encoding ascii -Value @'
@echo off
>>"%NEEDLR_PACK_LOG%" echo %*
exit /b 0
'@
    } else {
        Set-Content -LiteralPath (Join-Path $path 'dotnet') -Encoding utf8NoBOM -Value @'
#!/usr/bin/env sh
printf '%s\n' "$*" >> "$NEEDLR_PACK_LOG"
exit 0
'@
        & chmod +x (Join-Path $path 'dotnet')
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not make the dotnet shim executable.'
        }
    }

    return $path
}

function Get-RecordedPackInvocation {
    param([Parameter(Mandatory = $true)][string]$OutputDirectory)

    $shimPath = New-DotnetShimDirectory
    $logPath = Join-Path $shimPath 'dotnet-invocations.txt'

    $originalPath = $env:PATH
    $env:NEEDLR_PACK_LOG = $logPath
    $env:PATH = "$shimPath$([System.IO.Path]::PathSeparator)$originalPath"
    try {
        & $packScript -OutputDirectory $OutputDirectory -NoBuild -PublicRelease 6>&1 | Out-Null
    } finally {
        $env:PATH = $originalPath
        Remove-Item -LiteralPath 'Env:NEEDLR_PACK_LOG' -ErrorAction SilentlyContinue
    }

    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
        throw 'Package packing failed. The pack command was never invoked.'
    }

    return @(Get-Content -LiteralPath $logPath)
}

function Assert-Success {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    & $Action 6>&1 | Out-Null
    Write-Host "PASS: $Name"
}

function Assert-Failure {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$ExpectedMessage
    )

    try {
        & $Action 6>&1 | Out-Null
    } catch {
        $message = $_.Exception.Message
        if ($message -notlike "*$ExpectedMessage*") {
            throw "$Name failed. Expected a failure containing '$ExpectedMessage' but got '$message'."
        }

        Write-Host "PASS: $Name"
        return
    }

    throw "$Name failed. Verification succeeded where a failure was required."
}

try {
    $selectedProjects = @(& $packScript -ListOnly | ConvertFrom-Json)

    if ($selectedProjects.Count -eq 0) {
        throw 'Package selection failed. No projects were selected.'
    }
    if ($selectedProjects -notcontains 'src/NexusLabs.Needlr/NexusLabs.Needlr.csproj') {
        throw 'Package selection failed. The core package project was not selected.'
    }
    $excluded = @(
        $selectedProjects |
            Where-Object {
                $_ -like '*.Tests.csproj' -or
                $_ -like '*.Benchmarks.csproj' -or
                $_ -like '*IntegrationTests.csproj'
            })
    if ($excluded.Count -gt 0) {
        throw "Package selection failed. Unpublishable projects were selected: $($excluded -join ', ')."
    }
    Write-Host "PASS: Package selection excludes test and benchmark projects"

    $packOutput = New-Candidate -PackageNames @()
    $packInvocations = Get-RecordedPackInvocation -OutputDirectory $packOutput
    if ($packInvocations.Count -ne $selectedProjects.Count) {
        throw "Package packing failed. Expected $($selectedProjects.Count) pack invocations but observed $($packInvocations.Count)."
    }
    foreach ($project in $selectedProjects) {
        $expectedFragments = @(
            'pack',
            $project.Replace('/', [System.IO.Path]::DirectorySeparatorChar),
            '--configuration Release',
            "--output $packOutput",
            '--no-build',
            '-p:PublicRelease=true'
        )
        $invocation = @(
            $packInvocations |
                Where-Object {
                    $candidateInvocation = $_
                    $null -eq (
                        $expectedFragments |
                            Where-Object { -not $candidateInvocation.Contains($_) } |
                            Select-Object -First 1)
                } |
                Select-Object -First 1)
        if ($invocation.Count -eq 0) {
            throw "Package packing failed. No pack invocation matched '$project' with the required release arguments."
        }
    }
    Write-Host "PASS: Packing requests a no-build public release for every selected project"

    $candidate = New-VerifiedCandidate
    Assert-Success -Name 'Manifest verifies its own candidate' -Action {
        & $verifyScript `
            -PackageDirectory $candidate `
            -ExpectedVersion $testVersion `
            -ExpectedPackageVersion $testVersion `
            -ExpectedSourceSha $testSha `
            -ExpectedProducingRunId $testRunId `
            -ExpectedValidatedCiRunId $testValidatedCiRunId
    }

    Assert-Failure `
        -Name 'Mismatched validating CI run blocks publication' `
        -ExpectedMessage 'Manifest validatedCiRunId' `
        -Action {
            & $verifyScript `
                -PackageDirectory $candidate `
                -ExpectedValidatedCiRunId '7'
        }

    $unvalidated = New-Candidate -PackageNames @("NexusLabs.Needlr.$testVersion.nupkg")
    & $writeScript `
        -PackageDirectory $unvalidated `
        -Version $testVersion `
        -PackageVersion $testVersion `
        -SourceSha $testSha `
        -ProducingRunId $testRunId `
        -ProducingWorkflow 'release.yml' 6>&1 | Out-Null
    Assert-Failure `
        -Name 'Candidate without a validating CI run blocks publication' `
        -ExpectedMessage 'Manifest validatedCiRunId' `
        -Action {
            & $verifyScript `
                -PackageDirectory $unvalidated `
                -ExpectedValidatedCiRunId $testValidatedCiRunId
        }

    Assert-Failure `
        -Name 'Empty expectation blocks publication' `
        -ExpectedMessage "Expectation 'ExpectedVersion' was requested without a value" `
        -Action {
            & $verifyScript `
                -PackageDirectory $candidate `
                -ExpectedVersion ''
        }

    Assert-Failure `
        -Name 'Mismatched release version blocks publication' `
        -ExpectedMessage 'Manifest version' `
        -Action {
            & $verifyScript `
                -PackageDirectory $candidate `
                -ExpectedVersion '9.9.9-test.2'
        }

    Assert-Failure `
        -Name 'Mismatched source commit blocks publication' `
        -ExpectedMessage 'Manifest sourceSha' `
        -Action {
            & $verifyScript `
                -PackageDirectory $candidate `
                -ExpectedSourceSha 'fedcba9876543210fedcba9876543210fedcba98'
        }

    Assert-Failure `
        -Name 'Mismatched producing run blocks publication' `
        -ExpectedMessage 'Manifest producingRunId' `
        -Action {
            & $verifyScript `
                -PackageDirectory $candidate `
                -ExpectedProducingRunId '99'
        }

    $tampered = New-VerifiedCandidate
    Set-Content `
        -LiteralPath (Join-Path $tampered "NexusLabs.Needlr.$testVersion.nupkg") `
        -Value 'tampered' `
        -NoNewline
    Assert-Failure `
        -Name 'Tampered package content blocks publication' `
        -ExpectedMessage 'digest is' `
        -Action { & $verifyScript -PackageDirectory $tampered }

    $extra = New-VerifiedCandidate
    Set-Content `
        -LiteralPath (Join-Path $extra "Unexpected.Package.$testVersion.nupkg") `
        -Value 'unexpected' `
        -NoNewline
    Assert-Failure `
        -Name 'Unlisted package blocks publication' `
        -ExpectedMessage 'absent from the manifest' `
        -Action { & $verifyScript -PackageDirectory $extra }

    $missing = New-VerifiedCandidate
    Remove-Item -LiteralPath (Join-Path $missing "NexusLabs.Needlr.Injection.$testVersion.nupkg") -Force
    Assert-Failure `
        -Name 'Missing package blocks publication' `
        -ExpectedMessage 'is missing from the candidate' `
        -Action { & $verifyScript -PackageDirectory $missing }

    $unsupported = New-VerifiedCandidate
    $unsupportedManifestPath = Join-Path $unsupported 'release-manifest.json'
    $unsupportedManifest = Get-Content -LiteralPath $unsupportedManifestPath -Raw | ConvertFrom-Json
    $unsupportedManifest.schemaVersion = 2
    Set-Content `
        -LiteralPath $unsupportedManifestPath `
        -Value ($unsupportedManifest | ConvertTo-Json -Depth 5)
    Assert-Failure `
        -Name 'Unsupported manifest schema blocks publication' `
        -ExpectedMessage 'schema version' `
        -Action { & $verifyScript -PackageDirectory $unsupported }

    Assert-Failure `
        -Name 'Absent manifest blocks publication' `
        -ExpectedMessage 'does not exist' `
        -Action { & $verifyScript -PackageDirectory (New-Candidate -PackageNames @()) }

    Assert-Failure `
        -Name 'Mis-versioned package blocks manifest creation' `
        -ExpectedMessage 'do not use expected version' `
        -Action {
            & $writeScript `
                -PackageDirectory (New-Candidate -PackageNames @("NexusLabs.Needlr.1.2.3.nupkg")) `
                -Version $testVersion `
                -PackageVersion $testVersion `
                -SourceSha $testSha `
                -ProducingRunId $testRunId `
                -ProducingWorkflow 'release.yml'
        }

    Assert-Failure `
        -Name 'Empty candidate blocks manifest creation' `
        -ExpectedMessage 'No NuGet packages were produced' `
        -Action {
            & $writeScript `
                -PackageDirectory (New-Candidate -PackageNames @()) `
                -Version $testVersion `
                -PackageVersion $testVersion `
                -SourceSha $testSha `
                -ProducingRunId $testRunId `
                -ProducingWorkflow 'release.yml'
        }

    Assert-Failure `
        -Name 'Partial source SHA blocks manifest creation' `
        -ExpectedMessage 'full 40-character commit SHA' `
        -Action {
            & $writeScript `
                -PackageDirectory (New-Candidate -PackageNames @("NexusLabs.Needlr.$testVersion.nupkg")) `
                -Version $testVersion `
                -PackageVersion $testVersion `
                -SourceSha '0123456' `
                -ProducingRunId $testRunId `
                -ProducingWorkflow 'release.yml'
        }

    Assert-Failure `
        -Name 'Empty validating CI run blocks manifest creation' `
        -ExpectedMessage 'ValidatedCiRunId was supplied without a value' `
        -Action {
            & $writeScript `
                -PackageDirectory (New-Candidate -PackageNames @("NexusLabs.Needlr.$testVersion.nupkg")) `
                -Version $testVersion `
                -PackageVersion $testVersion `
                -SourceSha $testSha `
                -ProducingRunId $testRunId `
                -ProducingWorkflow 'release.yml' `
                -ValidatedCiRunId ''
        }

    Write-Host ''
    Write-Host 'All release artifact checks passed.' -ForegroundColor Green
} finally {
    foreach ($workspace in $workspaces) {
        Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue
    }
}
