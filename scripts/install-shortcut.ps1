# Creates a desktop shortcut to the Release build. Run dotnet build -c Release first.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'bin\Release\net48\Herdr Tray Utility.exe'
if (!(Test-Path -LiteralPath $exe)) {
    throw "Executable not found: $exe. Run dotnet build -c Release first."
}

$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Herdr Tray Utility.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = Split-Path -Parent $exe
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'Herdr agent status in the notification area'
$shortcut.WindowStyle = 1
$shortcut.Save()

Write-Host "Shortcut created: $shortcutPath"
Write-Host "Target: $exe"
