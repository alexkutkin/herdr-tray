Herdr Tray Utility {{VERSION}} - portable
===================================

Shows what your Herdr agents are doing, in the notification area.
By alexkutkin. MIT licensed. See About in the tray menu for the app version.

Requirements
------------
- Windows 10 22H2 or Windows 11, 64-bit.
- Herdr installed, with the "herdr" command available on PATH.
  Tested with Herdr 0.9.5 on Windows; other versions have not been verified.
- .NET Framework 4.8 or a later 4.x update (included in the Windows versions above).

Run
---
1. Unpack this folder anywhere you like.
2. Start Herdr Tray Utility.exe. Keep Herdr Tray Utility.exe.config next to it.
3. The icon appears in the notification area. Windows may put it in the
   hidden overflow first; drag it out once if you want it always visible.

Click the icon to open the wallboard, or double-click it. Right-click the
icon: Status opens the wallboard, About shows version and author, Exit quits.

Options
-------
The bottom of the wallboard holds three switches. Each click applies and is
saved per user at once:
- Keep the screen awake: holds the display on while the wallboard is open.
- Always on top: keeps the wallboard above other windows.
- Start with Windows: no shortcut is needed, and the entry follows the exe
  after you move the folder and launch the exe again.

Update and remove
-----------------
To update, choose Exit from the tray menu, then replace the application files
with those from the new ZIP. Keep the EXE and its config together.

To remove, turn off Start with Windows, choose Exit, and delete the folder.
Settings remain in HKEY_CURRENT_USER\Software\HerdrTray. To remove them too,
delete that registry key. Autostart uses the HerdrTray value under
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run.
This is an installer-free application; its settings do not travel with the folder.

Notes
-----
- This is an unofficial tool, not affiliated with Herdr.
- MIT licensed; see LICENSE.
- The build is not code-signed, so Windows may warn about an unknown publisher.
