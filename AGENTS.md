# Working agreement

## Communication
- Communicate with the owner in Russian.
- Keep everything the repository stores in English: code and comments, commit
  messages, test names, README, docs, ADRs, AGENTS.md, config comments.
- Content, not documentation, keeps its own language: product strings in locale
  resource files (`Strings/ru-RU` and alike) and personal financial documents.

## Git
- Work directly on `main`. Create branches or pull requests only when requested.
- Use focused Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`.
- Do not add AI co-authorship or tool credits to commits or pull requests.
- Add `Signed-off-by` only when required.
- Commit and push only when requested. Push to `origin/main` unless otherwise specified.
- Do not force-push or rewrite published history without explicit approval.

## Project
- **Build Release together with Debug.** After code changes run `dotnet build -c Release`
  alongside `dotnet build -c Debug`: the desktop shortcut `Herdr Tray Utility` launches
  `bin\Release\net48\Herdr Tray Utility.exe`, so a Debug-only change stays invisible there.
