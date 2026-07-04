# P2 — Duplicate Removal & Namespace Unification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove dead/duplicate files and collapse the split `BackupDbServer.Service` / `BackupDbServer.Services` namespaces (and the twin `Service/` + `Services/` folders) into one, with zero behavior change.

**Architecture:** Pure structural cleanup on top of P1's DI foundation. No logic changes. The public `ServiceContainer.X` API and all runtime behavior stay identical.

**Tech Stack:** .NET 8 WPF, C#. No automated test suite exists — verification is `dotnet build` (0 errors) after every task, plus a manual regression pass at the end. Frequent commits (one per task).

---

## Context: verified ground truth (from code survey)

- **Dead duplicate:** `Service/GoogleDriveService.cs` — class `GoogleDriveService : IGoogleDriveService` (namespace `BackupDbServer.Services`). Referenced **nowhere** except its own definition. The live one is `GoogleDriveServiceFixed` (`Service/GoogleDriveService_Fixed.cs`).
- **Misnamed file (NOT a real duplicate):** `Helpers/SettingsManager1.cs` contains `public static class SettingsManager`. There is no `SettingsManager.cs`. 10+ files call `SettingsManager.` — they reference the **class name**, so renaming the file is zero-impact. No textual reference to `SettingsManager1` exists anywhere.
- **Namespace split:** the service layer is fragmented across two namespaces:
  - `BackupDbServer.Services` (plural) — the **majority**, 13 files (AutoBackupService, BackupRestoreService, BackupWindowsServiceHost, DatabaseCopyService, DockerSqlServerManager, GoogleDriveService, GoogleDriveService_Fixed, IAutoBackupService, IGoogleDriveService, IScheduledBackupService, ScheduledBackupService, WindowsServiceManager, and `Services/LogService.cs`).
  - `BackupDbServer.Service` (singular) — the **minority**, 4 files: `Service/DailyTaskService.cs`, `Service/DeployService.cs`, `Service/IisMaintenanceService.cs`, `Service/XmlMaintenanceService.cs`.
  - **Unify toward the majority (`.Services` plural)** = least churn (change 4 declarations, not 13).
- **Consumers of the singular namespace** (`using BackupDbServer.Service;`) — 7 files:
  - Only-singular (must switch to plural): `ViewModels/DailyTaskViewModel.cs`, `ViewModels/DatabaseSelectionViewModel.cs`, `ViewModels/DeployViewModel.cs`, `ViewModels/IisMaintenanceViewModel.cs`.
  - Have both usings (just drop the singular line): `Core/Infrastructure/ServiceCollectionExtensions.cs`, `Helpers/ServiceContainer.cs`, `ViewModels/MainViewModel.cs`.
- **Twin folders:** `Service/` (16 files) and `Services/` (1 file: `LogService.cs`). Consolidate the stray `LogService.cs` into `Service/` and delete `Services/`.
- **No coupling risk:** no XAML `clr-namespace:BackupDbServer.Service*` references; `.csproj` globs `.cs` (no hardcoded folder paths); `Program.cs` already uses the plural using.
- **Folder rename deferred on purpose:** we do NOT rename `Service/`→`Services/`. The PRD's end state moves services into `Features/<Feature>/` folders (P3/P4), so renaming now would be throwaway work. Interim state after P2: one folder `Service/`, one namespace `BackupDbServer.Services` (the folder-singular/namespace-plural cosmetic mismatch is resolved later when services move to feature folders).

**Build note (recurring):** a running elevated instance of `BackupDbServer.exe` locks the output binary and makes the build fail with MSB3027/MSB3021. If that happens, the app must be closed first (it can't be killed from a non-elevated shell). Check with `tasklist | grep -i backup`.

---

### Task 1: Delete the dead `GoogleDriveService`

**Files:**
- Delete: `Service/GoogleDriveService.cs`

- [ ] **Step 1: Re-confirm it is unreferenced**

Run: `grep -rn "\bGoogleDriveService\b" --include=*.cs . | grep -v "GoogleDriveServiceFixed\|GoogleDriveService_Fixed\|IGoogleDriveService\|Service/GoogleDriveService.cs"`
Expected: no output (the only definition/uses are the `Fixed` class, the interface, and the file itself).

- [ ] **Step 2: Delete the file**

Run: `git rm Service/GoogleDriveService.cs`

- [ ] **Step 3: Build**

Run: `dotnet build BackupDbServer.csproj -nologo -clp:ErrorsOnly`
Expected: `0 Errors`.

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(P2): remove dead GoogleDriveService (superseded by GoogleDriveServiceFixed)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Rename `SettingsManager1.cs` → `SettingsManager.cs`

**Files:**
- Rename: `Helpers/SettingsManager1.cs` → `Helpers/SettingsManager.cs` (contents unchanged; class already named `SettingsManager`)

- [ ] **Step 1: Rename via git (preserves history)**

Run: `git mv Helpers/SettingsManager1.cs Helpers/SettingsManager.cs`

- [ ] **Step 2: Confirm no code references the old filename**

Run: `grep -rn "SettingsManager1" --include=*.cs --include=*.csproj .`
Expected: no output.

- [ ] **Step 3: Build**

Run: `dotnet build BackupDbServer.csproj -nologo -clp:ErrorsOnly`
Expected: `0 Errors` (class name and namespace are unchanged, so all `SettingsManager.` callers still resolve).

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(P2): rename SettingsManager1.cs to SettingsManager.cs (file name only)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Unify the service namespace to `BackupDbServer.Services`

Change the 4 singular-namespace declarations to plural, then fix the 7 consumers. **All 11 edits are in one commit** — the build only goes green once every reference is consistent.

**Files:**
- Modify (namespace declaration `BackupDbServer.Service` → `BackupDbServer.Services`):
  - `Service/DailyTaskService.cs`
  - `Service/DeployService.cs`
  - `Service/IisMaintenanceService.cs`
  - `Service/XmlMaintenanceService.cs`
- Modify (change `using BackupDbServer.Service;` → `using BackupDbServer.Services;`):
  - `ViewModels/DailyTaskViewModel.cs`
  - `ViewModels/DatabaseSelectionViewModel.cs`
  - `ViewModels/DeployViewModel.cs`
  - `ViewModels/IisMaintenanceViewModel.cs`
- Modify (remove the now-dead `using BackupDbServer.Service;` line; a plural using already exists in each):
  - `Core/Infrastructure/ServiceCollectionExtensions.cs`
  - `Helpers/ServiceContainer.cs`
  - `ViewModels/MainViewModel.cs`

- [ ] **Step 1: Change the 4 namespace declarations**

In each of the 4 service files, change the single line:
```csharp
namespace BackupDbServer.Service
```
to:
```csharp
namespace BackupDbServer.Services
```
(Match brace style already in the file — these use block-scoped `namespace ... { }`, so only the one declaration line changes.)

- [ ] **Step 2: Fix the 4 only-singular consumers**

In each of `ViewModels/DailyTaskViewModel.cs`, `ViewModels/DatabaseSelectionViewModel.cs`, `ViewModels/DeployViewModel.cs`, `ViewModels/IisMaintenanceViewModel.cs`, change:
```csharp
using BackupDbServer.Service;
```
to:
```csharp
using BackupDbServer.Services;
```

- [ ] **Step 3: Drop the dead using from the 3 dual-using consumers**

In `Core/Infrastructure/ServiceCollectionExtensions.cs`, `Helpers/ServiceContainer.cs`, and `ViewModels/MainViewModel.cs`, delete the line:
```csharp
using BackupDbServer.Service;
```
(Each already has `using BackupDbServer.Services;`, so the referenced types still resolve.)

- [ ] **Step 4: Verify nothing still references the singular namespace**

Run: `grep -rn "BackupDbServer\.Service\b" --include=*.cs . | grep -v "BackupDbServer\.Services"`
Expected: no output. (Catches any remaining `using BackupDbServer.Service;` or fully-qualified `BackupDbServer.Service.Type` reference.)

- [ ] **Step 5: Build**

Run: `dotnet build BackupDbServer.csproj -nologo -clp:ErrorsOnly`
Expected: `0 Errors`.

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor(P2): unify service layer under BackupDbServer.Services namespace

Collapse the split BackupDbServer.Service (singular, 4 files) into the
majority BackupDbServer.Services (plural). No behavior change.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Collapse the twin folders (move `LogService` into `Service/`)

`Services/LogService.cs` is the only file in the `Services/` folder; its namespace is already `BackupDbServer.Services` (unchanged). Move it beside the rest of the service classes and remove the empty folder.

**Files:**
- Move: `Services/LogService.cs` → `Service/LogService.cs`
- Delete: empty `Services/` folder

- [ ] **Step 1: Move the file with git**

Run: `git mv Services/LogService.cs Service/LogService.cs`

- [ ] **Step 2: Remove the now-empty folder (if git left it)**

Run: `rmdir Services 2>/dev/null; ls Services 2>/dev/null || echo "Services/ gone"`
Expected: `Services/ gone`.

- [ ] **Step 3: Build**

Run: `dotnet build BackupDbServer.csproj -nologo -clp:ErrorsOnly`
Expected: `0 Errors` (folder location doesn't affect compilation; namespace unchanged).

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(P2): move LogService into Service/ and drop the empty Services/ folder

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: Verify — build, run, republish

No automated tests exist; this task is the safety net. The controller coordinates the interactive parts (the app runs elevated and cannot be driven headlessly).

- [ ] **Step 1: Clean build**

Run: `dotnet build BackupDbServer.csproj -nologo -clp:ErrorsOnly`
Expected: `0 Errors`.

- [ ] **Step 2: Launch + startup-log check** (controller/user)

Launch `bin/Debug/net8.0-windows10.0.17763.0/BackupDbServer.exe`, approve UAC, confirm the process stays alive, then check `application.log` (repo root) for `✅ تم تهيئة ServiceContainer بنجاح` and **no new `[ERROR]`** lines for the new PID. Close the app cleanly.

- [ ] **Step 3: Manual regression** (user) — quick pass over the paths that touch the moved/renamed types:
  - Open **Daily Tasks Management** (uses `DailyTaskService`).
  - Open **Settings** and **Test Connection** (uses `SettingsManager`).
  - Run a **manual backup** (uses `BackupRestoreService` + `GoogleDriveServiceFixed`).
  - Right-click a DB → **toggle maintenance** (uses `XmlMaintenanceService`).
  - Open the **IIS automation / deploy** tab (uses `DeployService` + `IisMaintenanceService`).
  - View the **log panel** (uses `LogService`).

- [ ] **Step 4: Republish the client release** (confirms nothing broke packaging)

Run:
```bash
dotnet publish BackupDbServer.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:Version=1.0.0 -p:AssemblyVersion=1.0.0.0 -p:FileVersion=1.0.0.0 \
  -p:DebugType=none -p:DebugSymbols=false \
  --nologo -o "publish/ClientRelease-x64"
```
Expected: `0 Errors`, single-file `publish/ClientRelease-x64/BackupDbServer.exe` (~79 MB).

---

## Self-review

- **Spec coverage:** duplicate removal (Task 1 GoogleDriveService, Task 2 SettingsManager1) + folder unification (Task 4) + namespace unification (Task 3) — all PRD §5.2 items covered. Full `Core/Features` physical move is intentionally deferred to later phases (documented above), not silently dropped.
- **Placeholder scan:** none — every edit names exact files and exact before/after text.
- **Type/name consistency:** target namespace is `BackupDbServer.Services` everywhere; the 4 moved files and their 7 consumers are enumerated explicitly and cross-checked by the Step-4 grep in Task 3.
- **Ambiguity:** the "which direction to unify" decision is fixed (toward plural) with the file lists that make it unambiguous.

## Execution note

Order matters only for Task 3 (all 11 edits land together or the build breaks). Tasks 1, 2, 4 are independent and could be reordered. Each task is one commit; the whole phase is one PR stacked on `002-refactor-p1-di-foundation`.
