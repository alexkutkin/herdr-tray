# Builds Debug and Release, then packages Release as a ZIP.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root 'HerdrTray.csproj'
$project = [xml](Get-Content -LiteralPath $projectPath -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw 'HerdrTray.csproj must declare a version such as 1.0.0 or 1.0.0-beta.1.'
}
$name = "Herdr Tray Utility-$version-portable"
$dist = Join-Path $root 'dist'
$stage = [IO.Path]::GetFullPath((Join-Path $dist $name))
if (-not $stage.StartsWith([IO.Path]::GetFullPath($dist) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The package directory must be inside dist.'
}

$releaseExe = Join-Path $root 'bin\Release\net48\Herdr Tray Utility.exe'
$runningRelease = Get-Process -Name 'Herdr Tray Utility' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $releaseExe }
if ($runningRelease) {
    throw 'The Release executable is running. Choose Exit from its tray menu, then run packaging again.'
}

foreach ($configuration in @('Debug', 'Release')) {
    dotnet build $projectPath -c $configuration
    if ($LASTEXITCODE -ne 0) {
        throw "$configuration build failed. If the executable is running, choose Exit from its tray menu and retry."
    }
}

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $root 'bin\Release\net48\Herdr Tray Utility.exe') $stage
Copy-Item (Join-Path $root 'bin\Release\net48\Herdr Tray Utility.exe.config') $stage
$readmeTemplate = Get-Content -LiteralPath (Join-Path $root 'packaging\README.txt') -Raw -Encoding UTF8
if (-not $readmeTemplate.Contains('{{VERSION}}')) {
    throw 'The package README template must contain {{VERSION}}.'
}
$readme = $readmeTemplate.Replace('{{VERSION}}', $version)
[IO.File]::WriteAllText((Join-Path $stage 'README.txt'), $readme, (New-Object Text.UTF8Encoding($false)))
Copy-Item (Join-Path $root 'LICENSE') $stage

$zip = Join-Path $dist "$name.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Compress-Archive -LiteralPath $stage -DestinationPath $zip
Write-Host "Packaged: $zip"
