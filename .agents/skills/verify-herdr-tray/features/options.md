# Options

Users can keep the wall on top, hold the display awake and start at Windows login.

## Sub-features

- `options-display`: the two display switches persist across restart.
- `options-startup`: toggling startup adds/removes the per-user Run value.

## How to get to it (user POV)

Open the status wall from the tray and use its three labelled switches.

## Driving it with Windows computer use

Preconditions: own instance; record all original switch and registry values first.

Toggle Always on top and Keep the screen awake through their visible controls.
Read `Get-ItemProperty HKCU:\Software\HerdrTray` to confirm AlwaysOnTop/KeepAwake.
Exit, restart that EXE and capture the persisted switches. Toggle Start with Windows
on, then read `Get-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run`
and confirm HerdrTray is the quoted EXE path. Toggle off and confirm its removal.
Restore original options and startup state and capture the restored values.

## Gotchas

The registry belongs to the Windows user and is shared by all copies. A visible
keep-awake switch does not prove the display stays on over time. Login startup
needs a disposable login session; do not sign out the owner. Report both limitations.
