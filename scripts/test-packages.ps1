<#
.SYNOPSIS
    Validates the contents of key Needlr NuGet packages after packing.

.DESCRIPTION
    Packs the specified projects, extracts each resulting .nupkg, and asserts that
    the nuspec lists the correct transitive dependencies with the correct asset exclusions.

    Exits non-zero on any failure so this can be called from CI or release.ps1.

.PARAMETER NoBuild
    Skip the dotnet pack step (use existing bin/Release output).
#>
param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot  = Join-Path $scriptDir '..'
$failed    = $false

# GitHub Actions file-command env vars ($GITHUB_ENV / $GITHUB_OUTPUT /
# $GITHUB_STEP_SUMMARY) point at runner-owned files that the runner reads
# after this step completes. Nerdbank.GitVersioning's SetCloudBuildVariables
# MSBuild task detects GITHUB_ACTIONS=true and writes to $GITHUB_ENV during
# every project build — and in our per-csproj package-validation invocations
# it produces a malformed line the runner rejects with "Invalid format '2'"
# at post-step time. Confirmed by the NBGV stack trace in run 24288153060.
#
# Redirecting (not clearing) the env vars to throwaway temp files lets NBGV
# write harmlessly. Nulling the env vars makes NBGV throw MSB4018 because it
# can't open a null path. The runner already cached the ORIGINAL path when
# it set the env var for this step, so it reads its own untouched file at
# post-step time — our redirect only affects this pwsh session and its
# child processes, not the runner's post-step processing.
$env:GITHUB_ENV          = [System.IO.Path]::GetTempFileName()
$env:GITHUB_OUTPUT       = [System.IO.Path]::GetTempFileName()
$env:GITHUB_STEP_SUMMARY = [System.IO.Path]::GetTempFileName()

function Assert-NuspecDependency {
    param(
        [string]$NupkgPath,
        [string]$DependencyId,
        [string]$RequiredExclude,   # if set, the exclude attribute must CONTAIN this value
        [string]$ForbiddenExclude   # if set, the exclude attribute must NOT contain this value
    )

    $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "needlr-pkg-test-$([System.IO.Path]::GetRandomFileName())"
    Expand-Archive -Path $NupkgPath -DestinationPath $tmpDir -Force

    $nuspecPath = Get-ChildItem -Path $tmpDir -Filter '*.nuspec' -Recurse | Select-Object -First 1
    if (-not $nuspecPath) {
        Write-Host "  ERROR: No nuspec found in $NupkgPath" -ForegroundColor Red
        Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
        return $false
    }

    [xml]$nuspec = Get-Content $nuspecPath.FullName
    $ns = 'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd'
    $nsmgr = New-Object System.Xml.XmlNamespaceManager($nuspec.NameTable)
    $nsmgr.AddNamespace('ns', $ns)

    $dep = $nuspec.SelectSingleNode("//ns:dependency[@id='$DependencyId']", $nsmgr)

    Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue

    if (-not $dep) {
        Write-Host "  ERROR: dependency '$DependencyId' not found in nuspec" -ForegroundColor Red
        return $false
    }

    $exclude = $dep.GetAttribute('exclude')

    if ($RequiredExclude -and ($exclude -notmatch $RequiredExclude)) {
        Write-Host "  ERROR: dependency '$DependencyId' has exclude='$exclude' but expected it to contain '$RequiredExclude'" -ForegroundColor Red
        return $false
    }

    if ($ForbiddenExclude -and ($exclude -match $ForbiddenExclude)) {
        Write-Host "  ERROR: dependency '$DependencyId' has exclude='$exclude' but must NOT contain '$ForbiddenExclude' (analyzer delivery would be blocked)" -ForegroundColor Red
        return $false
    }

    return $true
}

function Assert-NupkgContainsFile {
    param(
        [string]$NupkgPath,
        [string]$EntryPath,        # path inside the zip e.g. "build/NexusLabs.Needlr.Build.targets"
        [string]$RequiredContent   # if set, the file must contain this string
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($NupkgPath)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -eq $EntryPath } | Select-Object -First 1
        if (-not $entry) {
            Write-Host "  ERROR: '$EntryPath' not found inside package" -ForegroundColor Red
            return $false
        }
        if ($RequiredContent) {
            $reader  = New-Object System.IO.StreamReader($entry.Open())
            $content = $reader.ReadToEnd()
            $reader.Dispose()
            if (-not $content.Contains($RequiredContent)) {
                Write-Host "  ERROR: '$EntryPath' exists but does not contain: $RequiredContent" -ForegroundColor Red
                return $false
            }
        }
        return $true
    } finally {
        $zip.Dispose()
    }
}

$assertions = @(
    @{
        Project    = 'src/NexusLabs.Needlr.Build/NexusLabs.Needlr.Build.csproj'
        PackageName = 'NexusLabs.Needlr.Build'
        Checks     = @(
            @{
                Id              = 'NexusLabs.Needlr.Generators'
                RequiredExclude = 'Compile'    # compile/runtime excluded — DLL is an Analyzer, not a lib
                ForbiddenExclude = 'Analyzers' # must NOT exclude Analyzers — otherwise generator DLL never flows
                Description     = 'Generators must be a dependency with Analyzers NOT excluded'
            },
            @{
                Id              = 'NexusLabs.Needlr.Generators.Attributes'
                RequiredExclude = $null
                ForbiddenExclude = $null
                Description     = 'Generators.Attributes must be a dependency'
            }
        )
        FileChecks = @(
            @{ Entry = 'build/NexusLabs.Needlr.Build.props';             Content = 'NeedlrAutoGenerate';               Description = 'build/props defines NeedlrAutoGenerate' },
            @{ Entry = 'build/NexusLabs.Needlr.Build.targets';           Content = 'RootNamespace';                    Description = 'build/targets falls back to RootNamespace for NeedlrNamespacePrefix' },
            @{ Entry = 'build/NexusLabs.Needlr.Build.targets';           Content = 'NeedlrWriteTypeRegistryAttributeFile'; Description = 'build/targets contains NeedlrWriteTypeRegistryAttributeFile target' },
            @{ Entry = 'buildTransitive/NexusLabs.Needlr.Build.props';   Content = 'NeedlrAutoGenerate';               Description = 'buildTransitive/props defines NeedlrAutoGenerate' },
            @{ Entry = 'buildTransitive/NexusLabs.Needlr.Build.targets'; Content = 'RootNamespace';                    Description = 'buildTransitive/targets falls back to RootNamespace for NeedlrNamespacePrefix' },
            @{ Entry = 'buildTransitive/NexusLabs.Needlr.Build.targets'; Content = 'NeedlrWriteTypeRegistryAttributeFile'; Description = 'buildTransitive/targets contains NeedlrWriteTypeRegistryAttributeFile target' }
        )
    }
)

foreach ($entry in $assertions) {
    $projPath = Join-Path $repoRoot $entry.Project
    $projPath = [System.IO.Path]::GetFullPath($projPath)
    $projDir  = Split-Path $projPath

    Write-Host ""
    Write-Host "=== $($entry.PackageName) ===" -ForegroundColor Cyan

    if (-not $NoBuild) {
        Write-Host "  Packing..." -NoNewline
        $packOut = & dotnet pack $projPath -c Release -v q 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host " FAILED" -ForegroundColor Red
            Write-Host $packOut
            $failed = $true
            continue
        }
        Write-Host " OK" -ForegroundColor Green
    }

    $nupkg = Get-ChildItem -Path (Join-Path $projDir 'bin/Release') -Filter '*.nupkg' |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

    if (-not $nupkg) {
        Write-Host "  ERROR: No .nupkg found in $(Join-Path $projDir 'bin/Release')" -ForegroundColor Red
        $failed = $true
        continue
    }

    Write-Host "  Package: $($nupkg.Name)"

    foreach ($check in $entry.Checks) {
        Write-Host "  Checking: $($check.Description)..." -NoNewline
        $ok = Assert-NuspecDependency `
            -NupkgPath       $nupkg.FullName `
            -DependencyId    $check.Id `
            -RequiredExclude $check.RequiredExclude `
            -ForbiddenExclude $check.ForbiddenExclude

        if ($ok) {
            Write-Host " OK" -ForegroundColor Green
        } else {
            $failed = $true
        }
    }

    foreach ($fc in $entry.FileChecks) {
        Write-Host "  Checking: $($fc.Description)..." -NoNewline
        $ok = Assert-NupkgContainsFile `
            -NupkgPath      $nupkg.FullName `
            -EntryPath      $fc.Entry `
            -RequiredContent $fc.Content

        if ($ok) {
            Write-Host " OK" -ForegroundColor Green
        } else {
            $failed = $true
        }
    }
}

function Test-AttributeFileTarget {
    param(
        [string]$RepoRoot,
        [string]$Description,
        [hashtable]$Properties = @{},
        [switch]$IncludeGeneratorAnalyzer,  # add a fake NexusLabs.Needlr.Generators Analyzer item
        $ExpectedContains = $null,  # $null = expect file NOT created
        [ref]$Failed
    )

    Write-Host "  Test: $Description..." -NoNewline

    $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "needlr-build-test-$([System.IO.Path]::GetRandomFileName())"
    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

    # Use forward slashes so the paths are valid in MSBuild XML
    $buildPropsPath   = (Join-Path $RepoRoot 'src/NexusLabs.Needlr.Build/build/NexusLabs.Needlr.Build.props')   -replace '\\', '/'
    $buildTargetsPath = (Join-Path $RepoRoot 'src/NexusLabs.Needlr.Build/build/NexusLabs.Needlr.Build.targets') -replace '\\', '/'
    $objDirFwd        = (Join-Path $tmpDir 'obj') -replace '\\', '/'

    $propLines = $Properties.GetEnumerator() | ForEach-Object {
        "    <$($_.Key)>$([System.Security.SecurityElement]::Escape($_.Value))</$($_.Key)>"
    }
    $propBlock = ($propLines -join "`n")

    # When testing the generator-presence guard we add a fake Analyzer item whose %(Filename) is
    # NexusLabs.Needlr.Generators. MSBuild derives %(Filename) from the Include path at evaluation
    # time — the file does not need to exist on disk.
    $analyzerBlock = if ($IncludeGeneratorAnalyzer) {
        "`n  <ItemGroup>`n    <Analyzer Include=`"$($tmpDir -replace '\\', '/')/NexusLabs.Needlr.Generators.dll`" />`n  </ItemGroup>"
    } else { '' }

    $projContent = @"
<Project>
  <PropertyGroup>
    <IntermediateOutputPath>$objDirFwd/</IntermediateOutputPath>
$propBlock
  </PropertyGroup>
  <Import Project="$buildPropsPath" />
  <Import Project="$buildTargetsPath" />$analyzerBlock
</Project>
"@

    $projPath = Join-Path $tmpDir 'test.proj'
    Set-Content -Path $projPath -Value $projContent -Encoding UTF8

    $msbuildOut = & dotnet msbuild $projPath /t:NeedlrWriteTypeRegistryAttributeFile /v:q /nologo 2>&1

    $generatedFile = Join-Path $tmpDir 'obj' 'NeedlrGeneratedTypeRegistry.g.cs'
    $fileExists    = Test-Path $generatedFile

    try {
        if ($null -eq $ExpectedContains) {
            if ($fileExists) {
                Write-Host " FAIL - file generated when it should not have been" -ForegroundColor Red
                $Failed.Value = $true
            } else {
                Write-Host " OK" -ForegroundColor Green
            }
        } else {
            if (-not $fileExists) {
                Write-Host " FAIL - file not generated" -ForegroundColor Red
                Write-Host "    MSBuild output: $($msbuildOut -join ' ')"
                $Failed.Value = $true
            } else {
                $content = Get-Content $generatedFile -Raw
                if (-not $content.Contains($ExpectedContains)) {
                    Write-Host " FAIL - unexpected content" -ForegroundColor Red
                    Write-Host "    Expected to contain: $ExpectedContains"
                    Write-Host "    Actual: $content"
                    $Failed.Value = $true
                } else {
                    Write-Host " OK" -ForegroundColor Green
                }
            }
        }
    } finally {
        Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "=== NeedlrWriteTypeRegistryAttributeFile target ==" -ForegroundColor Cyan

$failedRef = [ref]$failed

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "No generator analyzer present — file not written (safety guard)" `
    -Properties @{} `
    -ExpectedContains $null `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "NeedlrAutoGenerate=false suppresses file even when generator present" `
    -Properties @{ NeedlrAutoGenerate = 'false' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains $null `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "Generator present, RootNamespace set — writes single-element array" `
    -Properties @{ RootNamespace = 'MyProject' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains 'IncludeNamespacePrefixes = new[] { "MyProject" }' `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "Explicit multi-prefix overrides RootNamespace default" `
    -Properties @{ RootNamespace = 'MyProject'; NeedlrNamespacePrefix = 'Foo;Bar' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains '"Foo", "Bar"' `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "NeedlrAutoGenerateAttribute=false suppresses file generation" `
    -Properties @{ RootNamespace = 'MyProject'; NeedlrAutoGenerateAttribute = 'false' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains $null `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "No RootNamespace and no explicit prefix — writes no-arg attribute (all types)" `
    -Properties @{} `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains '[assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry()]' `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "IsRoslynComponent=true suppresses file even when generator present" `
    -Properties @{ IsRoslynComponent = 'true' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains $null `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "IsAspireHost=true suppresses file even when generator present" `
    -Properties @{ IsAspireHost = 'true' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains $null `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "ExcludeNamespacePrefix emits ExcludeNamespacePrefixes in attribute" `
    -Properties @{ RootNamespace = 'MyProject'; NeedlrExcludeNamespacePrefix = 'Avalonia' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains 'ExcludeNamespacePrefixes = new[] { "Avalonia" }' `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "Both include and exclude prefixes emitted together" `
    -Properties @{ RootNamespace = 'MyProject'; NeedlrExcludeNamespacePrefix = 'Avalonia;Microsoft.Maui' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains '"Avalonia", "Microsoft.Maui"' `
    -Failed $failedRef

Test-AttributeFileTarget `
    -RepoRoot $repoRoot `
    -Description "Exclude-only (no include prefix, no RootNamespace) emits exclude arg" `
    -Properties @{ NeedlrExcludeNamespacePrefix = 'Avalonia' } `
    -IncludeGeneratorAnalyzer `
    -ExpectedContains 'ExcludeNamespacePrefixes = new[] { "Avalonia" }' `
    -Failed $failedRef

Write-Host ""
Write-Host "=== Example projects (source-mode simulation of NexusLabs.Needlr.Build) ===" -ForegroundColor Cyan
Write-Host "  (Sync guarantee: if Directory.Build.targets drifts from the NuGet package, these fail)"

$examplesRoot = Join-Path $repoRoot 'src/Examples'
$allExampleProjects = Get-ChildItem -Path $examplesRoot -Recurse -Filter "*.csproj" |
    Where-Object {
        # Skip projects that opt out of CI validation (e.g., SDK examples
        # with large bundled binaries that aren't in the solution).
        $content = Get-Content $_.FullName -Raw
        $content -notmatch '<NeedlrExcludeFromValidation>true</NeedlrExcludeFromValidation>'
    } |
    Sort-Object FullName

# Build all example projects in a single MSBuild invocation via a temporary
# traversal project. MSBuild constructs one dependency graph, builds each unique
# project exactly once (even shared deps like Generators), and parallelizes
# across CPU cores. The previous per-project foreach loop spawned 48 separate
# MSBuild processes that each independently rebuilt shared dependencies.
$projItems = $allExampleProjects | ForEach-Object {
    "    <ProjectReference Include=`"$($_.FullName.Replace('\', '/'))`" />"
}
$traversalContent = @"
<Project>
  <ItemGroup>
$($projItems -join "`n")
  </ItemGroup>
  <Target Name="Restore">
    <MSBuild Projects="@(ProjectReference)" Targets="Restore" BuildInParallel="true" />
  </Target>
  <Target Name="Build">
    <MSBuild Projects="@(ProjectReference)" Targets="Build" BuildInParallel="true" Properties="Configuration=Debug" />
  </Target>
</Project>
"@
$traversalPath = Join-Path ([System.IO.Path]::GetTempPath()) "needlr-examples-$([System.IO.Path]::GetRandomFileName()).proj"
Set-Content -Path $traversalPath -Value $traversalContent -Encoding UTF8

Write-Host "  Building $($allExampleProjects.Count) example projects (parallel, with restore)..."
$buildOut = & dotnet msbuild $traversalPath -t:Restore -v:q -nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Example restore FAILED" -ForegroundColor Red
    $buildOut | Where-Object { $_ -match ':\s*error\s' } | ForEach-Object {
        Write-Host "    $_" -ForegroundColor Red
    }
    $failed = $true
}

$buildOut = & dotnet msbuild $traversalPath -t:Build -v:q -nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Example builds FAILED" -ForegroundColor Red
    $buildOut | Where-Object { $_ -match ':\s*error\s' } | ForEach-Object {
        Write-Host "    $_" -ForegroundColor Red
    }
    $failed = $true
} else {
    Write-Host "  All $($allExampleProjects.Count) example projects built OK" -ForegroundColor Green
}

Remove-Item $traversalPath -ErrorAction SilentlyContinue

$testProjects = $allExampleProjects | Where-Object { $_.Name -like '*Tests.csproj' }

if ($testProjects.Count -gt 0) {
    # Test projects run sequentially because dotnet test does not accept multiple
    # project arguments. However the builds are already done (--no-build), so each
    # invocation is fast (test execution only).
    foreach ($proj in $testProjects) {
        Write-Host -NoNewline "  Running $($proj.Name)..."
        $testOut = & dotnet test $proj.FullName -c Debug --no-build 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host " FAIL" -ForegroundColor Red
            Write-Host ($testOut | Select-Object -Last 20 | Out-String)
            $failed = $true
        } else {
            Write-Host " OK" -ForegroundColor Green
        }
    }
}

Write-Host ""

if ($failed) {
    Write-Host "Package validation FAILED." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Package validation passed." -ForegroundColor Green
    exit 0
}
