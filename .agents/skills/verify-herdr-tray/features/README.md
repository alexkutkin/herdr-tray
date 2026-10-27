# Herdr Tray verification map

Run Doctor in the parent skill first. Packaging uses an isolated copy and can run
alongside the owner's app. Desktop journeys require a free Windows session, a PID
owned by the run, and fresh UI observations. Do not drive someone else's instance.

| Feature | Harness | Proof |
| --- | --- | --- |
| [Manual ZIP release](package.md) | `scripts/verify.ps1` | Six automated checks and persisted reports |
| [Tray and status wall](status.md) | Windows computer use | Actions, screenshots and CLI comparison |
| [About](about.md) | Windows computer use | Dialog text compared with EXE metadata |
| [Options](options.md) | Windows computer use and read-only registry queries | Visible setting, persistence and restored state |

Capture actions and their results. Record missing preconditions as failures or not
run, never as a pass through another entry point. Only the automated packaging
journey is guaranteed by the helper; desktop journeys need their own evidence.
