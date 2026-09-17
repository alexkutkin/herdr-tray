# Herdr Tray

A notification-area icon that shows what Herdr agents are doing. No window unless you ask for one.

- Polls `herdr agent list` (JSON output) every 2 seconds.
- Icon: `H` idle (grey), `H*` working (blue), `H!` needs attention (amber), `Hx` disconnected (red).
- Hover shows one line per agent, agents that need input first, trimmed to the 127 character tooltip limit.
- Left click does nothing. Right click has two items: Status and Exit.

## Status wall

Right click, Status opens a resizable wallboard: one tile per agent with a big coloured
circle, sized to the window, so it stays readable from across the room. Agents that need
input come first and their whole tile is filled amber. The cross hides the wall and the
tray icon keeps polling; Esc does the same.

Two options along the bottom:

- `Keep the screen awake`, on by default, holds the display on while the wall is open
  (`SetThreadExecutionState`). It does not override a corporate inactivity lock policy.
- `Always on top`, off by default, keeps the wall above other windows.

## Build and run

```bash
dotnet build
dotnet run
```

Requires .NET 9 and `herdr` on PATH.
