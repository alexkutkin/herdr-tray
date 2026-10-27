# Manual ZIP release

A maintainer builds an archive that users can extract without an installer.

## Sub-features

- `package-build`: Debug and Release compile successfully.
- `package-content`: ZIP contains the EXE, config, README and license in one folder.
- `package-version`: metadata, archive name and README follow the project version.
- `package-invalid`: an invalid version fails rather than creating an unsafe package path.

## How to get to it (user POV)

Run `powershell -ExecutionPolicy Bypass -File scripts\package.ps1` from the repository root.

## Driving it with PowerShell

Preconditions: Windows, .NET SDK and Framework 4.8 reference assemblies are installed.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1`.
It invokes the real packaging command in a temporary copy, checks the extracted
runtime files, changes only the copy's version and repackages, then checks rejection
of an invalid version. Require six PASS results, exit code zero, and saved reports.
After cleanup, open the printed report directory and confirm its logs still exist.

## Gotchas

The root `dist` ZIP is not replaced by verification. No desktop app is launched;
an archive passing these tests does not prove tray rendering or startup-at-login.
