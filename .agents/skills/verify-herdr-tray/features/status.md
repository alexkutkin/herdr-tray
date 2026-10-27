# Tray and status wall

Users see combined agent status and open a wall of individual agents.

## Sub-features

- `status-tray`: tooltip and icon represent current agents.
- `status-open`: single-click, double-click and the Status menu open the wall.
- `status-hide`: Esc and close hide the wall while the app keeps running.

## How to get to it (user POV)

Launch the EXE, locate its notification-area H icon, then click, double-click,
or right-click and choose Status. Check the overflow if Windows hides it.

## Driving it with Windows computer use

Preconditions: an instance started by this run; `herdr agent list` returns JSON.

Record CLI output, hover over H and capture its tooltip. Open the wall through each
entry point, returning to hidden state between trials. Compare visible names and
states to fresh CLI output. Resize the wall. Press Esc, reopen, then close using X.
Capture each action/result and confirm the recorded process stays alive after hiding.
Choose Exit and confirm that PID ends.

## Gotchas

Polling is every two seconds; a CLI timeout takes five seconds. Agent state may
change between observations. Zero agents cannot prove multi-tile layout or ordering;
record those sub-features as not run instead of manufacturing an input state.
