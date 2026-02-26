# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**BackupDbServer** is a WPF application for SQL Server database backup and restore operations with support for Docker, remote connections, IIS deployment automation, scheduled backups, and Google Drive integration.

**Technology Stack:**
- .NET 8.0 (net8.0-windows10.0.17763.0)
- WPF with XAML UI
- Microsoft.Data.SqlClient (SQL Server operations)
- Docker.DotNet (Docker integration)
- Google.Apis.Drive.v3 (Google Drive integration)

## Building and Running

### Build
```powershell
dotnet build BackupDbServer.sln
```

### Run (GUI Mode)
```powershell
dotnet run --project BackupDbServer.csproj
```

### Run as Windows Service
```powershell
dotnet run --project BackupDbServer.csproj -- --service
```

**Note:** The service mode requires administrator privileges.

## Architecture

### Pattern: MVVM + Service Layer + ServiceContainer (Singleton)

**Critical:** This application uses a **ServiceContainer** pattern for centralized service management. All shared services MUST be accessed through `ServiceContainer` - never instantiate them directly.

```csharp
// ✅ CORRECT - Always use ServiceContainer
var logger = ServiceContainer.Logger;
var backupService = ServiceContainer.BackupService;

// ❌ WRONG - Never instantiate directly
var logger = new FileLogger();  // FORBIDDEN
var backupService = new BackupRestoreService();  // FORBIDDEN
```

### ServiceContainer Location: `Helpers/ServiceContainer.cs`

**Managed Services:**
- `Logger` - FileLogger instance for application-wide logging
- `BackupService` - BackupRestoreService for backup/restore operations
- `GoogleDriveService` - Google Drive upload service
- `AutoBackupService` - Automated backup scheduling
- `ScheduledBackupService` - Time-based scheduled backups
- `DailyTaskService` - Daily SQL task execution

**Lifecycle Management:**
- `Initialize()` - Called in `App.xaml.cs` OnStartup
- `Shutdown()` - Called in `App.xaml.cs` OnExit
- `Reset()` - For testing purposes only

## Project Structure

### Core Services (Service/)
- **BackupRestoreService.cs** ⭐ - Central backup/restore service. Supports Local, Docker, and Remote connections. Handles database backup, restore, compression (ZIP), and Google Drive upload.
- **AutoBackupService.cs** - Interval-based or time-scheduled automatic backups
- **ScheduledBackupService.cs** - Day-specific scheduled backups (e.g., every Friday at 3 AM)
- **DailyTaskService.cs** - Scheduled SQL tasks (stored procedures, scripts, DBCC checks, index rebuilds, etc.)
- **DeployService.cs** - 7-step automated IIS deployment (Application Pool, Site, Database, Web.config, Files, Permissions, Start)
- **GoogleDriveService_Fixed.cs** - Google Drive upload service

### ViewModels (ViewModels/)
All ViewModels follow MVVM pattern and inherit from `ViewModelBase`. They use `ServiceContainer` to access services and `RelayCommand`/`AsyncRelayCommand` for command binding.

### Models (Models/)
- **Settings.cs** - Main application settings
- **BackupConfig.cs** - Backup configuration (in Helpers/)
- **AutoBackupConfig.cs** - Auto backup settings
- **ScheduledBackupConfig.cs** - Scheduled backup settings
- **DailyTaskConfig.cs** - Daily task configuration with 8 task types

### Helpers (Helpers/)
- **SettingsManager1.cs** - Settings persistence (JSON at `%APPDATA%\BackupDbServer\settings.json`)
- **ProgressHelper.cs** - Unified progress reporting helper
- **ErrorHandler.cs** - Unified error handling with logging and user notifications
- **FileLogger.cs** - Application logging to `application.log`
- **CrashProtectionManager.cs** - Crash detection and recovery
- **SingleInstanceManager.cs** - Prevents multiple app instances using Mutex
- **CompressionHelper** (in compress.cs) - ZIP compression for backups

## Connection Types

The application supports three connection types (enum `ConnectionType`):
1. **Local** - Local SQL Server instance
2. **Docker** - SQL Server running in Docker container
3. **Remote** - Remote SQL Server instance

Connection strings are built automatically based on settings:
- Windows Authentication or SQL Authentication
- Custom ports supported (default: 1433)
- TrustServerCertificate=True by default for development

## Critical SQL Library

**ALWAYS use:** `Microsoft.Data.SqlClient`
**NEVER use:** `System.Data.SqlClient` (obsolete)

This is enforced throughout the codebase. Any new SQL code must use Microsoft.Data.SqlClient.

## Settings and Configuration

**Settings Location:** `%APPDATA%\BackupDbServer\`

**Configuration Files:**
- `settings.json` - Main app settings (BackupConfig)
- `auto_backup_config.json` - Auto backup configuration
- `scheduled_backup_config.json` - Scheduled backup configuration
- `daily_tasks.json` - Daily task definitions
- `application.log` - Application log file

**Backup Paths (default in Documents):**
- `LocalBackups/` - Local connection backups
- `DockerBackups/` - Docker container backups
- `RemoteBackups/` - Remote server backups

## Coding Conventions

### Async Operations
All I/O operations use async/await. Methods are suffixed with `Async`:
```csharp
public async Task BackupDatabasesAsync(List<string> databases, IProgress<string> progress)
```

### Progress Reporting
Use `ProgressHelper` for consistent progress reporting:
```csharp
var progress = ProgressHelper.CreateProgress(
    statusUpdate: msg => StatusText = msg,
    percentageUpdate: p => ProgressValue = p,
    logger: ServiceContainer.Logger
);
```

### Error Handling
Use `ErrorHandler` for consistent error handling:
```csharp
var result = await ErrorHandler.TryExecuteAsync(
    async () => await SomeOperationAsync(),
    "Operation Name",
    ServiceContainer.Logger
);
```

### Naming Conventions
- Fields: `_camelCase` (e.g., `_logger`, `_backupService`)
- Properties: `PascalCase` (e.g., `DatabaseName`, `IsEnabled`)
- Methods: `PascalCase` with `Async` suffix for async methods
- Services: `*Service.cs`
- ViewModels: `*ViewModel.cs`
- Views: `*Window.xaml`

### UI Thread Updates
Always use Dispatcher for UI updates from background threads:
```csharp
Application.Current.Dispatcher.Invoke(() =>
{
    StatusText.Text = "Operation complete";
});
```

## Daily Task Types

The `DailyTaskService` supports 8 task types (enum `TaskType`):
1. **SqlScript** - Execute SQL script
2. **StoredProcedure** - Execute stored procedure
3. **BackupDatabase** - Perform database backup
4. **CheckIntegrity** - Run DBCC CHECKDB
5. **UpdateStatistics** - Update database statistics
6. **RebuildIndexes** - Rebuild all indexes
7. **ShrinkDatabase** - Shrink database files
8. **Custom** - Custom SQL operation

Tasks run automatically at specified times and display countdown timers in the UI.

## Docker Integration

Docker operations use `Docker.DotNet` to:
- List running containers
- Copy backup files from containers using `GetArchiveFromContainerAsync`
- Copy backup files to containers using `ExtractArchiveToContainerAsync`

**Requirements:**
- Docker Desktop must be running
- Container must be active
- SQL Server must be accessible within container

## Important Files to NOT Recreate

These files were intentionally deleted and should not be recreated:
- `BackupExecutor.cs` (replaced by BackupRestoreService)
- `CompressionHelper.cs` (empty, code is in compress.cs)
- `SettingsRepairTool.cs` (empty)
- `TestSettings.cs` (obsolete test file)
- `Testing/` folder
- `Tools/` folder
- `Test-*.ps1` files (14 PowerShell test scripts)

## Copilot Instructions Integration

The project has strict operational protocols defined in `.github/copilot-instructions.md`:
- Zero-tolerance error policy
- Enterprise-grade code quality requirements
- Structured debugging protocol (5 stages: Problem Reconstruction, Root Cause Isolation, Verified Repair, Test Generation, Validation)
- Terminal discipline for Windows/PowerShell (use separate windows for long-running servers)
- Context integrity enforcement

When making changes, always respect these protocols and the architecture documented in `.github/ai_context.md`.

## Logs and Diagnostics

**Application Log:** `%APPDATA%\BackupDbServer\application.log`

**Log Levels:**
- `[INFO]` - General information
- `[WARNING]` - Warning messages
- `[ERROR]` - Error messages

**Crash Logs:** `crash_YYYYMMDD.log` files in project root

For troubleshooting, always check:
1. Application log
2. SQL Server connection (use SQL Server Management Studio)
3. Docker Desktop status (if using Docker connection type)
4. Windows Event Log (for service mode)

## Testing

There is no automated test suite. Manual testing is performed through the UI:
- Test connections in Settings window
- Verify backup operations in MainWindow
- Check scheduled tasks in DailyTaskManagementWindow
- Monitor logs in application.log

## Google Drive Integration

OAuth 2.0 credentials are stored in: `DriveCredentials/Google.Apis.Auth.OAuth2.Responses.TokenResponse-user`

First-time use requires browser authentication. Token is persisted for subsequent runs.
