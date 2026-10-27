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

## Release
- Set `Version` in `HerdrTray.csproj`.
- **Update git tags before releasing.** Run `git fetch --tags` so local tags match
  `origin`, and tag the release commit `v<Version>`.
- Run `powershell -ExecutionPolicy Bypass -File scripts\package.ps1` to build both
  configurations and create `dist\Herdr Tray Utility-<version>-portable.zip`.
- Run `powershell -ExecutionPolicy Bypass -File scripts\verify.ps1` before distributing;
  all 6 checks must pass.
- When publishing is requested, commit and push the matching source and the `v<Version>`
  tag, then create GitHub release `v<Version>` with the ZIP attached.
