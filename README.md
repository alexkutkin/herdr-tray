# Herdr Tray Utility

A notification-area icon that shows what Herdr agents are doing. No window unless you ask for one.

- Polls `herdr agent list` (JSON output) every 2 seconds.
- Icon: `H` idle (grey), `H*` working (blue), `H!` needs attention (amber), `Hx` disconnected (red).
- Hover shows one line per agent, agents that need input first, trimmed to the 63 character tooltip limit.
- Click or double-click opens the status wall. Right click has three items: Status, Settings and Exit.

## Settings

The tray menu's Settings opens a small window with three options, saved per user
and applied immediately:

- `Keep the screen awake while the wall is open`, on by default, holds the display on
  while the wall is open (`SetThreadExecutionState`). It does not override a corporate
  inactivity lock policy.
- `Keep the wall always on top`, off by default, keeps the wall above other windows.
- `Start with Windows` writes a per-user `Run` entry pointing at the current exe, so
  no shortcut is needed. If you move the portable folder and launch the exe again, the
  entry follows. Turn the option off before deleting the folder.

## Status wall

Click the icon or choose Status: a resizable wallboard with one tile per agent and a
big coloured circle, sized to the window, so it stays readable from across the room.
Agents that need input come first and their whole tile is filled amber. The cross
hides the wall and the tray icon keeps polling; Esc does the same.

## Build and run

```bash
dotnet build
dotnet run
```

Requires .NET Framework 4.8 (included in Windows 10 and 11) and `herdr` on PATH.

## Package

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package.ps1
```

Builds Release and produces `dist\Herdr Tray Utility-<version>-portable.zip` with the exe,
its config and a short readme.

## License

MIT. Unofficial tool, not affiliated with Herdr.
