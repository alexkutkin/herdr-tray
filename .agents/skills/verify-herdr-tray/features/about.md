# About

Users can identify the app, release version and author from the tray menu.

## Sub-features

- `about-open`: About displays version, author, license and affiliation information.
- `about-close`: OK closes the dialog while the tray app stays alive.

## How to get to it (user POV)

Right-click the notification-area H icon and choose About.

## Driving it with Windows computer use

Preconditions: an instance started by this run, with its EXE path recorded.

Capture the context menu, choose About, then capture the dialog. Compare displayed
product name, version and author against `Get-Item <recorded-exe-path> | Select-Object
-ExpandProperty VersionInfo`. Expect MIT and the unofficial affiliation note. Click
OK and confirm the recorded process remains alive. Exit through the tray menu.

## Gotchas

EXE metadata checks alone do not prove About opens or displays the expected text.
An old running instance may have a different version from the current ZIP.
