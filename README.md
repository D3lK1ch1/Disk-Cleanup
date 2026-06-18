# disk-cleanup

A personal Windows disk-space cleanup tool. Scans common sources of reclaimable space, shows you a checklist, and only deletes what you explicitly tick and confirm. Nothing runs automatically.

## Features

- Scanner + checklist printer
- Selection input + action executor
- WPF widget with checklist, checkboxes, risk filtering, and Clean Selected

The scheduled/background check work is still pending:

- `DiskCleanup.exe --check`
- low-disk-space Windows toast notification
- Task Scheduler setup instructions
- notification launch flow back into the normal interactive checklist

## What it scans

- Recycle Bin
- Windows Temp / `SoftwareDistribution\Download`
- VS Code `CachedExtensionVSIXs`
- WSL: `~/.cache`, `~/.npm`, and `node_modules`/`target` dirs under `~/projects`
- Docker reclaimable space (`docker system df`)
- Top largest folders in `Downloads`
- `AppData\Local\Packages` folders untouched 6+ months
- `.claude` / `.codex` AI tool folders
- Top installed apps by size (registry) — **informational only**

## Risk levels

Every scanned item is tagged:

- **SAFE** — fully regenerable caches/build artifacts (Recycle Bin, temp files, npm/VSIX caches, `node_modules`/`target`). Still requires your confirmation before deletion — nothing is auto-deleted, even SAFE items.
- **REVIEW** — needs your judgement (Downloads folders, stale AppData packages, Docker prune, AI tool folders). You decide case by case. Folders in this category are moved to the Recycle Bin rather than permanently deleted, so a wrong pick is recoverable.
- **INFO** — installed apps list. Checkbox is disabled; no delete action exists for this category.

### Why installed apps are INFO-only, not deletable

Deleting a cache folder removes bytes nothing else depends on, and it's fully regenerable. Uninstalling an app means running an external uninstaller that can touch the registry, shared DLLs, services, and licensing — a much larger and less predictable blast radius, often needing admin rights. This tool deliberately keeps that out of scope: it shows you the list for awareness only.

## Safety

- **Self-deletion guard** — the Downloads scan never lists (or offers to delete) the folder this tool is running from, even if it ranks in the top N by size.
- **Recycle Bin, not permanent delete** — REVIEW-risk folders go through `SHFileOperation` with `FOF_ALLOWUNDO`, so they're recoverable. SAFE items (temp/cache contents) are still deleted directly, since they're fully regenerable by design.
- **Symlinks and junctions are never followed** — both when computing folder sizes and when deleting a folder, a symlink or junction found inside it is removed as a link only. Its target (e.g. a pnpm-style `node_modules` link, or a WSL mount) is left untouched.
- **WSL space accounting** — deleting files under a `\\wsl.localhost\...` path frees space inside that distro's virtual disk, not on `C:` directly. The result log notes this so the before/after free-space numbers aren't confusing.

## Project structure

- `DiskCleanup.Core/` — scanners, action executor, selection parsing (shared library)
- `DiskCleanup/` — console app (checklist + numeric selection)
- `DiskCleanup.Wpf/` — desktop widget (checklist grid, risk filter, checkboxes)
- `DiskCleanup.Tests/` — xUnit tests for `Core`

## Running it

Requires the .NET 10 SDK on Windows.

**Widget (recommended):**
```powershell
cd DiskCleanup.Wpf
dotnet run
```
Click **Scan**, tick items, optionally filter by risk, then **Clean Selected**.
A confirmation dialog lists exactly what will be processed before anything happens.

**Console:**
```powershell
cd DiskCleanup
dotnet run
```

**Tests:**
```powershell
dotnet test
```

## Roadmap

- `--check` mode: silent scan + Windows toast notification when free space drops below 45GB, triggered via Task Scheduler (setup command printed, not
  auto-configured)
- Filter by category/size in the widget
- Cross-platform support (Linux, Mac) via an Avalonia UI rewrite of the widget, alongside the existing WPF version
