$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$evidence = Join-Path $root "TestResults\$runId"
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('herdr-tray-verify-' + [guid]::NewGuid().ToString('N'))
$results = New-Object 'System.Collections.Generic.List[object]'
$suiteError = $null
$packageReady = $false

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Check([string]$Name, [scriptblock]$Check, [bool]$Ready = $true) {
    $status = 'Skipped'
    $detail = 'The initial package build failed.'
    if ($Ready) {
        try {
            & $Check | Out-Null
            $status = 'Passed'
            $detail = ''
        }
        catch {
            $status = 'Failed'
            $detail = $_.Exception.Message
        }
    }
    $results.Add([pscustomobject]@{ Name = $Name; Status = $status; Detail = $detail })
    Write-Host "[$($status.ToUpperInvariant())] $Name $detail"
}

function Invoke-Package([string]$Label) {
    $log = Join-Path $evidence "$Label.log"
    $errors = Join-Path $evidence "$Label.stderr.log"
    $packageScript = Join-Path $scratch 'scripts\package.ps1'
    $process = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$packageScript`""
    ) -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $log -RedirectStandardError $errors
    Add-Content -LiteralPath $log -Value "Exit code: $($process.ExitCode)"
    Get-Content -LiteralPath $log | ForEach-Object { Write-Host $_ }
    return $process.ExitCode
}

function Read-Package([string]$Version, [string]$Destination) {
    $zip = Join-Path $scratch "dist\Herdr Tray Utility-$Version-portable.zip"
    Expand-Archive -LiteralPath $zip -DestinationPath $Destination
    return Join-Path $Destination "Herdr Tray Utility-$Version-portable"
}

New-Item -ItemType Directory -Path $evidence, $scratch | Out-Null
try {
    Get-ChildItem -LiteralPath $root -File | Where-Object {
        $_.Extension -in @('.cs', '.csproj', '.config', '.manifest', '.ico') -or $_.Name -eq 'LICENSE'
    } | Copy-Item -Destination $scratch
    New-Item -ItemType Directory -Path (Join-Path $scratch 'scripts'), (Join-Path $scratch 'packaging') | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'scripts\package.ps1') -Destination (Join-Path $scratch 'scripts')
    Copy-Item -LiteralPath (Join-Path $root 'packaging\README.txt') -Destination (Join-Path $scratch 'packaging')
    $projectPath = Join-Path $scratch 'HerdrTray.csproj'
    $project = [xml](Get-Content -LiteralPath $projectPath -Raw)
    $version = [string]$project.Project.PropertyGroup.Version
    $zipPath = Join-Path $scratch "dist\Herdr Tray Utility-$version-portable.zip"

    Invoke-Check 'BuildsDebugAndRelease' {
        Assert-True ((Invoke-Package 'build') -eq 0) 'Packaging failed; see build logs.'
        foreach ($configuration in @('Debug', 'Release')) {
            Assert-True (Test-Path -LiteralPath (Join-Path $scratch "bin\$configuration\net48\Herdr Tray Utility.exe")) "$configuration EXE is missing."
        }
        $script:packageReady = $true
    }

    Invoke-Check 'ArchiveContainsOnlyRuntimeFiles' {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $actual = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') } | Sort-Object)
            $expected = @('Herdr Tray Utility.exe', 'Herdr Tray Utility.exe.config', 'LICENSE', 'README.txt') |
                ForEach-Object { "Herdr Tray Utility-$version-portable/$_" } | Sort-Object
            Assert-True (@(Compare-Object $expected $actual).Count -eq 0) 'The ZIP contains unexpected or missing files.'
        }
        finally { $archive.Dispose() }
        $script:extracted = Read-Package $version (Join-Path $scratch 'extracted')
        Copy-Item -LiteralPath $zipPath -Destination $evidence
    } $packageReady

    Invoke-Check 'ExecutableMetadataMatchesProject' {
        $info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $extracted 'Herdr Tray Utility.exe'))
        Assert-True ($info.ProductVersion -eq $version) 'EXE version does not match the project.'
        Assert-True ($info.ProductName -eq $project.Project.PropertyGroup.Product) 'EXE product name does not match the project.'
        Assert-True ($info.CompanyName -eq $project.Project.PropertyGroup.Company) 'EXE author does not match the project.'
        $built = Get-FileHash -LiteralPath (Join-Path $scratch 'bin\Release\net48\Herdr Tray Utility.exe')
        $packed = Get-FileHash -LiteralPath (Join-Path $extracted 'Herdr Tray Utility.exe')
        Assert-True ($built.Hash -eq $packed.Hash) 'The packaged EXE differs from the Release build.'
        $exeBytes = [IO.File]::ReadAllBytes((Join-Path $extracted 'Herdr Tray Utility.exe'))
        $exeText = [Text.Encoding]::ASCII.GetString($exeBytes)
        Assert-True ($exeText -notmatch '(?i)[a-z]:\\[^\x00\r\n]*\.pdb') 'The packaged EXE exposes an absolute debug-symbol path.'
    } $packageReady

    Invoke-Check 'PackageIncludesRuntimeConfigurationAndVersionedReadme' {
        $config = [xml](Get-Content -LiteralPath (Join-Path $extracted 'Herdr Tray Utility.exe.config') -Raw)
        Assert-True ($config.configuration.startup.supportedRuntime.sku -eq '.NETFramework,Version=v4.8') 'The runtime configuration is incorrect.'
        $dpi = $config.configuration.'System.Windows.Forms.ApplicationConfigurationSection'.add
        Assert-True ($dpi.key -eq 'DpiAwareness' -and $dpi.value -eq 'PerMonitorV2') 'DPI configuration is missing.'
        $readme = Get-Content -LiteralPath (Join-Path $extracted 'README.txt') -Raw
        Assert-True ($readme.Contains("Herdr Tray Utility $version") -and -not $readme.Contains('{{VERSION}}')) 'The README version was not rendered.'
        Assert-True ((Get-FileHash -LiteralPath (Join-Path $extracted 'LICENSE')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $root 'LICENSE')).Hash) 'The packaged license differs from the source license.'
    } $packageReady

    Invoke-Check 'ChangingProjectVersionUpdatesPackageAndReadme' {
        $changedVersion = if ($version -eq '1.0.123') { '1.0.124' } else { '1.0.123' }
        $project.Project.PropertyGroup.Version = $changedVersion
        $project.Save($projectPath)
        Assert-True ((Invoke-Package 'changed-version') -eq 0) 'Packaging a changed version failed.'
        $changedFolder = Read-Package $changedVersion (Join-Path $scratch 'changed-extracted')
        $info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $changedFolder 'Herdr Tray Utility.exe'))
        $readme = Get-Content -LiteralPath (Join-Path $changedFolder 'README.txt') -Raw
        Assert-True ($info.ProductVersion -eq $changedVersion -and $readme.Contains("Herdr Tray Utility $changedVersion")) 'Changing the project version did not update the EXE and README.'
    } $packageReady

    Invoke-Check 'InvalidVersionIsRejected' {
        $before = @(Get-ChildItem -LiteralPath (Join-Path $scratch 'dist') -Recurse | ForEach-Object { $_.FullName } | Sort-Object)
        $project.Project.PropertyGroup.Version = '../outside'
        $project.Save($projectPath)
        Assert-True ((Invoke-Package 'invalid-version') -ne 0) 'An invalid version was accepted.'
        $errorText = Get-Content -LiteralPath (Join-Path $evidence 'invalid-version.stderr.log') -Raw
        Assert-True ($errorText.Contains('must declare a version')) 'Packaging failed for an unrelated reason.'
        $after = @(Get-ChildItem -LiteralPath (Join-Path $scratch 'dist') -Recurse | ForEach-Object { $_.FullName } | Sort-Object)
        Assert-True (@(Compare-Object $before $after).Count -eq 0) 'Invalid input changed package output.'
    } $packageReady
}
catch {
    $suiteError = $_.Exception.Message
    Write-Host "[FAILED] Verification setup: $suiteError"
}
finally {
    $report = [pscustomobject]@{
        RunId = $runId
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
        Scope = 'Manual ZIP packaging; desktop UI and Windows sign-in are not exercised.'
        SuiteError = $suiteError
        Tests = @($results.ToArray())
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding UTF8
    $summary = @('# Verification results', '', $report.Scope, '', '| Test | Result |', '| --- | --- |')
    foreach ($result in $results) { $summary += "| $($result.Name) | $($result.Status) |" }
    if ($suiteError) { $summary += "`nSetup error: $suiteError" }
    $summary | Set-Content -LiteralPath (Join-Path $evidence 'summary.md') -Encoding UTF8
    $safeTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolvedScratch = [IO.Path]::GetFullPath($scratch)
    if ($resolvedScratch.StartsWith($safeTemp, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedScratch -Leaf) -match '^herdr-tray-verify-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
    }
}

$passed = @($results | Where-Object { $_.Status -eq 'Passed' }).Count
Write-Host "`n$passed/$($results.Count) checks passed. Evidence: $evidence"
if ($suiteError -or $results.Count -ne 6 -or $passed -ne 6) { exit 1 }
exit 0
