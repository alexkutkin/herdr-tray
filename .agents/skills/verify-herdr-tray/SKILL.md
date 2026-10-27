---
name: verify-herdr-tray
description: Verify Herdr Tray Utility packaging and Windows tray behaviour, with saved evidence.
disable-model-invocation: true
---

# Verify Herdr Tray Utility

Read [the feature map](features/README.md), then select the requested journey.
Packaging is automated. Desktop journeys require a Windows desktop automation tool
and separate UI evidence; packaging passes do not verify UI behaviour.

## Launch

From the repository root, on Windows with .NET SDK and Framework 4.8 Developer Pack:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

The helper copies build inputs into an isolated temporary directory, invokes the real
packaging command, and tests the resulting archives. Readiness means the package
command succeeds and the archive can be opened, not merely that an EXE exists.
The helper does not launch or stop a desktop instance. Cleanup is automatic.

For desktop journeys, first check for an existing instance using Doctor. The
`Local\HerdrTray` mutex prevents two copies in one Windows session. If one is running,
report the occupied session and arrange its closure with the owner before driving it.
Once free, build Debug and Release, then launch `bin\Release\net48\Herdr Tray Utility.exe`.
Retain the PID you started. Readiness means the tray icon is visible and its Status
command opens a usable wall, even when Herdr is disconnected.

## Doctor

This read-only check identifies an occupied desktop session and the installed CLI:

```powershell
Get-Process -Name 'Herdr Tray Utility' -ErrorAction SilentlyContinue | Select-Object Id, Path
dotnet --version
herdr --version
herdr agent list
```

Record missing Herdr as a failed precondition for live-status comparisons. Packaging
does not require it. Compare the launched EXE path with the PID and its file version;
an older running copy is not proof of the current build.

## Drive

Use `scripts\verify.ps1` for [packaging](features/package.md). It exercises the same
`scripts\package.ps1` command used for manual releases, on a disposable copy.

For desktop checks, use the host's Windows computer-use tool. Observe the notification
area, locate the H icon by its tooltip `Herdr Tray Utility`, then click it or open its
context menu. Select returned accessibility elements named `Status`, `About`, or `Exit`.
For owner-drawn controls, use a fresh screenshot and its coordinates. Follow the
selected feature recipe; do not substitute reflection or internal method calls for UI input.

## Evidence

The helper prints the exact report directory under `TestResults`. Preserve its test
results and build logs, including failures and exit codes. These files are ignored by Git.
For desktop journeys, create a separate subdirectory there and record the build path,
version, actions, screenshot before/after, observed state and outcome. For options,
include read-only registry observations after a toggle and after restart.

Report each entry point separately as passed, failed or not run. Mocks, compilation,
or archive metadata do not establish that a desktop journey passed.

## Cleanup

The helper removes only its own temporary workspace and retains `TestResults`.
For UI runs, select Exit and wait for the recorded PID to end. If it is unresponsive,
terminate only that PID. Restore any options/startup state changed by the run and
record restoration; never terminate by process name. Do not sign the owner out to
test login startup. Mark that journey not run unless it can be tested in a disposable session.

## Helpers

`scripts\verify.ps1` is the automated helper; its complete invocation is under Launch.
For future updates to this skill and map, explicitly run `verification maintain`.

The proof standard follows the verification skill's Prove It Works principle.
The package checks use native PowerShell and .NET ZIP/file APIs so this small project
does not need a separate test framework or package installation.
