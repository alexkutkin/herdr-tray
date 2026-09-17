# Herdr Tray

A notification-area icon that shows what Herdr's agents are doing. No window.

- Polls `herdr agent list` (JSON output) every 2 seconds.
- Icon: `H` idle (grey), `H•` working (blue), `H!` needs attention (yellow), `H×` disconnected (red).
- Hover shows one line per agent, agents that need input first, trimmed to the 127 character tooltip limit.
- Left click does nothing. Right click has one item: Exit.

## Build and run

```bash
dotnet build
dotnet run
```

Requires .NET 9 and `herdr` on PATH.
