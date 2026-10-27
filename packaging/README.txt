Herdr Tray Utility 1.0.0 — portable
===================================

Shows what your Herdr agents are doing, in the notification area.

Requirements
------------
- Windows 10 22H2 or Windows 11, 64-bit.
- Herdr installed, with the "herdr" command available on PATH.
- .NET Framework 4.8 (already included in Windows 10 and 11).

Run
---
1. Unpack this folder anywhere you like.
2. Start Herdr Tray Utility.exe. Keep Herdr Tray Utility.exe.config next to it.
3. The icon appears in the notification area. Windows may put it in the
   hidden overflow first; drag it out once if you want it always visible.

Click the icon to open the wallboard, or double-click it. Right-click the
icon: Status opens the wallboard, Exit quits.

Options
-------
The bottom of the wallboard holds three switches. Each click applies and is
saved per user at once:
- Keep the screen awake: holds the display on while the wallboard is open.
- Always on top: keeps the wallboard above other windows.
- Start with Windows: no shortcut is needed, and the entry follows the exe
  if you move the folder. Turn it off before deleting this folder; everything
  else is removed with the folder.

Notes
-----
- This is an unofficial tool, not affiliated with Herdr.
- MIT licensed; see LICENSE.
- The build is not code-signed, so Windows may warn about an unknown publisher.
