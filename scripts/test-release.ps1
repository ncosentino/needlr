<#
.SYNOPSIS
    Validates that release.ps1 finalizes releases by pushing only a tag.

.DESCRIPTION
    Creates an isolated local Git remote and prepared main commit, runs both the
    dry-run and real release paths with command shims, and proves that origin/main
    remains unchanged while the expected tag is pushed.
#>

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$releaseScript = Join-Path $scriptDir 'release.ps1'
$releaseSource = Get-Content $releaseScript -Raw

$forbiddenPatterns = @(
    @{ Pattern = '(?im)^\s*&?\s*nbgv\s+set-version\b'; Description = 'nbgv set-version' },
    @{ Pattern = '(?im)^\s*&?\s*git\s+commit\b'; Description = 'git commit' },
    @{ Pattern = '(?im)^\s*&?\s*git\s+pull\b'; Description = 'git pull' },
    @{ Pattern = '(?im)^\s*&?\s*git\s+push\b(?!\s+origin\s+"refs/tags/\$tagName"\s*$)'; Description = 'a non-tag push' }
)

foreach ($forbidden in $forbiddenPatterns) {
    if ($releaseSource -match $forbidden.Pattern) {
        throw "release.ps1 contains forbidden release behavior: $($forbidden.Description)."
    }
}

$tokens = $null
$parseErrors = $null
[void][System.Management.Automation.Language.Parser]::ParseFile(
    $releaseScript,
    [ref]$tokens,
    [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "release.ps1 has PowerShell parse errors: $($parseErrors.Message -join '; ')"
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

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)][string]$Content,
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Content.Contains($Expected)) {
        throw "$Message Missing '$Expected'. Actual output:`n$Content"
    }
}

function ConvertTo-PlainOutput {
    param(
        [Parameter(Mandatory = $true)]$Output
    )

    $text = $Output | Out-String
    return [regex]::Replace($text, "`e\[[0-?]*[ -/]*[@-~]", '')
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$FailureMessage
    )

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage`n$($output | Out-String)"
    }

    return ,$output
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "needlr-release-test-$([System.IO.Path]::GetRandomFileName())"
$barePath = Join-Path (Join-Path (Join-Path $tempRoot 'github.com') 'ncosentino') 'needlr.git'
$workPath = Join-Path $tempRoot 'work'
$shimPath = Join-Path $tempRoot 'bin'
$workScriptsPath = Join-Path $workPath 'scripts'
$workSrcPath = Join-Path $workPath 'src'
$workAnalyzerPath = Join-Path $workSrcPath 'TestAnalyzer'
$workReleaseScript = Join-Path $workScriptsPath 'release.ps1'
$previousPath = $env:PATH
$previousVersion = $env:TEST_RELEASE_VERSION
$previousSha = $env:TEST_RELEASE_SHA
$previousCiConclusion = $env:TEST_RELEASE_CI_CONCLUSION

try {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $barePath), $shimPath | Out-Null
    Invoke-Git -Arguments @('init', '--bare', $barePath) -FailureMessage 'Could not create the test remote.' | Out-Null

    $remoteUri = ([System.Uri]::new($barePath, [System.UriKind]::Absolute)).AbsoluteUri
    Invoke-Git -Arguments @('clone', $remoteUri, $workPath) -FailureMessage 'Could not clone the test remote.' | Out-Null

    New-Item -ItemType Directory -Force -Path $workScriptsPath, $workAnalyzerPath | Out-Null

    Copy-Item $releaseScript $workReleaseScript
    Set-Content -Path (Join-Path $workScriptsPath 'test-packages.ps1') -Encoding utf8NoBOM -Value @'
param([switch]$NoBuild)
exit 0
'@
    Set-Content -Path (Join-Path $workSrcPath 'NexusLabs.Needlr.slnx') -Encoding utf8NoBOM -Value '<Solution />'
    Set-Content -Path (Join-Path $workAnalyzerPath 'AnalyzerReleases.Unshipped.md') -Encoding utf8NoBOM -Value @'
; Unshipped analyzer releases

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
'@

    $releaseVersion = '1.2.3-alpha.4'
    Set-Content -Path (Join-Path $workPath 'version.json') -Encoding utf8NoBOM -Value @"
{
  "version": "$releaseVersion"
}
"@
    Set-Content -Path (Join-Path $workPath 'CHANGELOG.md') -Encoding utf8NoBOM -Value @"
## [$releaseVersion] - 2030-01-01

### Changed

- Prepared protected-main release fixture.
"@

    if ($IsWindows) {
        Set-Content -Path (Join-Path $shimPath 'nbgv.cmd') -Encoding ascii -Value @'
@echo off
if "%1"=="get-version" (
  echo %TEST_RELEASE_VERSION%
  exit /b 0
)
if "%1"=="tag" (
  git tag v%TEST_RELEASE_VERSION%
  exit /b %errorlevel%
)
exit /b 1
'@
        Set-Content -Path (Join-Path $shimPath 'dotnet.cmd') -Encoding ascii -Value "@echo off`r`nexit /b 0"
        Set-Content -Path (Join-Path $shimPath 'gh.cmd') -Encoding ascii -Value @'
@echo off
echo {"workflow_runs":[{"head_sha":"%TEST_RELEASE_SHA%","head_branch":"main","event":"push","status":"completed","conclusion":"%TEST_RELEASE_CI_CONCLUSION%","html_url":"https://example.invalid/ci","created_at":"2030-01-01T00:00:00Z"}]}
exit /b 0
'@
    } else {
        Set-Content -Path (Join-Path $shimPath 'nbgv') -Encoding utf8NoBOM -Value @'
#!/usr/bin/env sh
if [ "$1" = "get-version" ]; then
  printf '%s\n' "$TEST_RELEASE_VERSION"
  exit 0
fi
if [ "$1" = "tag" ]; then
  git tag "v$TEST_RELEASE_VERSION"
  exit $?
fi
exit 1
'@
        Set-Content -Path (Join-Path $shimPath 'dotnet') -Encoding utf8NoBOM -Value "#!/usr/bin/env sh`nexit 0"
        Set-Content -Path (Join-Path $shimPath 'gh') -Encoding utf8NoBOM -Value @'
#!/usr/bin/env sh
printf '%s\n' "{\"workflow_runs\":[{\"head_sha\":\"$TEST_RELEASE_SHA\",\"head_branch\":\"main\",\"event\":\"push\",\"status\":\"completed\",\"conclusion\":\"$TEST_RELEASE_CI_CONCLUSION\",\"html_url\":\"https://example.invalid/ci\",\"created_at\":\"2030-01-01T00:00:00Z\"}]}"
exit 0
'@
        & chmod +x (Join-Path $shimPath 'nbgv') (Join-Path $shimPath 'dotnet') (Join-Path $shimPath 'gh')
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not make release command shims executable.'
        }
    }

    Push-Location $workPath
    try {
        Invoke-Git -Arguments @('switch', '-c', 'main') -FailureMessage 'Could not create fixture main.' | Out-Null
        Invoke-Git -Arguments @('config', 'user.name', 'Needlr Release Test') -FailureMessage 'Could not configure fixture user name.' | Out-Null
        Invoke-Git -Arguments @('config', 'user.email', 'release-test@example.invalid') -FailureMessage 'Could not configure fixture user email.' | Out-Null
        Invoke-Git -Arguments @('add', '.') -FailureMessage 'Could not stage fixture files.' | Out-Null
        Invoke-Git -Arguments @('commit', '-m', 'chore: prepare release') -FailureMessage 'Could not commit fixture files.' | Out-Null
        Invoke-Git -Arguments @('push', '-u', 'origin', 'main') -FailureMessage 'Could not push fixture main.' | Out-Null

        $expectedSha = (Invoke-Git -Arguments @('rev-parse', 'HEAD') -FailureMessage 'Could not resolve fixture HEAD.').Trim()
        $env:TEST_RELEASE_VERSION = $releaseVersion
        $env:TEST_RELEASE_SHA = $expectedSha
        $env:TEST_RELEASE_CI_CONCLUSION = 'success'
        $env:PATH = "$shimPath$([System.IO.Path]::PathSeparator)$previousPath"

        $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
        $mismatchOutput = & $pwsh -NoProfile -File $workReleaseScript -Version '1.2.3-alpha.5' -DryRun 2>&1
        if ($LASTEXITCODE -eq 0) {
            throw 'Release dry run accepted a version that does not match version.json.'
        }
        $mismatchText = ConvertTo-PlainOutput -Output $mismatchOutput
        Assert-Contains -Content $mismatchText -Expected 'version.json contains' -Message 'Version mismatch did not fail explicitly.'
        Assert-Contains -Content $mismatchText -Expected '1.2.3-alpha.5' -Message 'Version mismatch did not report the requested version.'

        $dryRunOutput = & $pwsh -NoProfile -File $workReleaseScript -Version $releaseVersion -DryRun 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Release dry run failed.`n$($dryRunOutput | Out-String)"
        }

        $dryRunText = ConvertTo-PlainOutput -Output $dryRunOutput
        Assert-Contains -Content $dryRunText -Expected 'git push origin refs/tags/v1.2.3-alpha.4' -Message 'Dry run did not report the tag push.'
        Assert-Contains -Content $dryRunText -Expected 'No branch commit or branch push will be performed.' -Message 'Dry run did not state the protected-main invariant.'

        $tagBeforeRelease = Invoke-Git -Arguments @('ls-remote', '--tags', 'origin', "refs/tags/v$releaseVersion") -FailureMessage 'Could not inspect fixture tags.'
        Assert-Equal -Expected 0 -Actual $tagBeforeRelease.Count -Message 'Dry run created a remote tag.'

        Invoke-Git -Arguments @('switch', '-c', 'release/test-finalization') -FailureMessage 'Could not create the non-main fixture branch.' | Out-Null
        $nonMainOutput = & $pwsh -NoProfile -File $workReleaseScript -Version $releaseVersion 2>&1
        if ($LASTEXITCODE -eq 0) {
            throw 'Release finalization succeeded outside local main.'
        }
        Assert-Contains -Content (ConvertTo-PlainOutput -Output $nonMainOutput) -Expected 'Release finalization must run from local main.' -Message 'Non-main finalization did not fail explicitly.'
        Invoke-Git -Arguments @('switch', 'main') -FailureMessage 'Could not return to fixture main.' | Out-Null

        $env:TEST_RELEASE_CI_CONCLUSION = 'failure'
        $failedCiOutput = & $pwsh -NoProfile -File $workReleaseScript -Version $releaseVersion 2>&1
        if ($LASTEXITCODE -eq 0) {
            throw 'Release finalization succeeded with failed main CI.'
        }
        Assert-Contains -Content (ConvertTo-PlainOutput -Output $failedCiOutput) -Expected 'Main CI must complete successfully before release finalization.' -Message 'Failed CI did not block finalization explicitly.'
        $env:TEST_RELEASE_CI_CONCLUSION = 'success'

        $tagAfterFailures = Invoke-Git -Arguments @('ls-remote', '--tags', 'origin', "refs/tags/v$releaseVersion") -FailureMessage 'Could not inspect fixture tags after failed finalization attempts.'
        Assert-Equal -Expected 0 -Actual $tagAfterFailures.Count -Message 'A failed finalization attempt pushed a tag.'

        $releaseOutput = & $pwsh -NoProfile -File $workReleaseScript -Version $releaseVersion 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Release finalization failed.`n$($releaseOutput | Out-String)"
        }

        $localHead = (Invoke-Git -Arguments @('rev-parse', 'HEAD') -FailureMessage 'Could not resolve local fixture main.').Trim()
        $remoteMainLines = @(Invoke-Git -Arguments @('ls-remote', 'origin', 'refs/heads/main') -FailureMessage 'Could not resolve remote fixture main.')
        $remoteTagLines = @(Invoke-Git -Arguments @('ls-remote', 'origin', "refs/tags/v$releaseVersion") -FailureMessage 'Could not resolve remote fixture tag.')
        $remoteMain = $remoteMainLines[0].Split("`t")[0]
        $remoteTag = $remoteTagLines[0].Split("`t")[0]
        $commitCount = [int](Invoke-Git -Arguments @('rev-list', '--count', 'HEAD') -FailureMessage 'Could not count fixture commits.').Trim()

        Assert-Equal -Expected $expectedSha -Actual $localHead -Message 'Release finalization changed local main.'
        Assert-Equal -Expected $expectedSha -Actual $remoteMain -Message 'Release finalization changed origin/main.'
        Assert-Equal -Expected $expectedSha -Actual $remoteTag -Message 'Release tag does not point at the prepared main commit.'
        Assert-Equal -Expected 1 -Actual $commitCount -Message 'Release finalization created an unexpected commit.'
    } finally {
        Pop-Location
    }

    Write-Host 'Release script validation passed.' -ForegroundColor Green
} finally {
    $env:PATH = $previousPath
    if ($null -eq $previousVersion) {
        Remove-Item Env:TEST_RELEASE_VERSION -ErrorAction SilentlyContinue
    } else {
        $env:TEST_RELEASE_VERSION = $previousVersion
    }
    if ($null -eq $previousSha) {
        Remove-Item Env:TEST_RELEASE_SHA -ErrorAction SilentlyContinue
    } else {
        $env:TEST_RELEASE_SHA = $previousSha
    }
    if ($null -eq $previousCiConclusion) {
        Remove-Item Env:TEST_RELEASE_CI_CONCLUSION -ErrorAction SilentlyContinue
    } else {
        $env:TEST_RELEASE_CI_CONCLUSION = $previousCiConclusion
    }
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
