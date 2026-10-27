# Builds the Release and produces dist\Herdr Tray Utility-<version>-portable.zip.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = [xml](Get-Content -LiteralPath (Join-Path $root 'HerdrTray.csproj') -Raw)
$version = ($project.Project.PropertyGroup | Select-Object -First 1).Version
$name = "Herdr Tray Utility-$version-portable"
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist $name

dotnet build (Join-Path $root 'HerdrTray.csproj') -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $root 'bin\Release\net48\Herdr Tray Utility.exe') $stage
Copy-Item (Join-Path $root 'bin\Release\net48\Herdr Tray Utility.exe.config') $stage
Copy-Item (Join-Path $root 'packaging\README.txt') $stage
Copy-Item (Join-Path $root 'LICENSE') $stage

$zip = Join-Path $dist "$name.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Compress-Archive -LiteralPath $stage -DestinationPath $zip
Write-Host "Packaged: $zip"
