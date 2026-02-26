# BackupDbServer

<div dir="rtl">

A comprehensive Windows desktop application for SQL Server database backup, restore, and management operations with advanced automation features.

</div>

## Features

### 🔄 Backup & Restore
- **Manual Backup** - On-demand database backups with progress tracking
- **Manual Restore** - Restore databases from backup files
- **Multiple Connection Types** - Local SQL Server, Docker containers, and remote servers
- **Compression** - Automatic ZIP compression of backup files
- **Google Drive Integration** - Automatic upload to Google Drive

### ⏰ Automated Operations
- **Auto Backup Service** - Schedule backups at regular intervals or specific daily times
- **Scheduled Backups** - Day-specific backup schedules (e.g., every Friday at 3 AM)
- **Daily Tasks** - Schedule database maintenance tasks with 8 task types:
  - SQL Script Execution
  - Stored Procedure Execution
  - Database Backup
  - Integrity Checks (DBCC CHECKDB)
  - Statistics Updates
  - Index Rebuilds
  - Database Shrinking
  - Custom Operations

### 🚀 Deployment & Management
- **IIS Deployment** - Automated 7-step deployment:
  1. Application Pool creation
  2. IIS site configuration
  3. Database creation
  4. Web.config modification
  5. File deployment
  6. Permission configuration
  7. Site activation
- **SQL Executor** - Run SQL queries directly from the application
- **Database Copy** - Copy databases between servers

### 🐳 Docker Support
- Connect to SQL Server instances running in Docker containers
- Copy backups from/to containers
- Manage containerized databases

### 🛡️ Reliability Features
- **Crash Protection** - Automatic crash detection and recovery
- **Single Instance** - Prevents multiple application instances
- **Health Monitoring** - Service health checks
- **Comprehensive Logging** - Detailed operation logs
- **Windows Service Mode** - Run as a Windows service for unattended operation

## System Requirements

- **Operating System:** Windows 10 or later
- **Framework:** .NET 8.0 Runtime
- **SQL Server:** Any version (local, remote, or Docker)
- **Optional:** Docker Desktop (for Docker connection mode)
- **Optional:** Google Drive account (for cloud backup)

## Installation

### Option 1: Run from Source

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Clone or download this repository
3. Open a terminal in the project directory
4. Build the project:
   ```powershell
   dotnet build BackupDbServer.sln
   ```
5. Run the application:
   ```powershell
   dotnet run --project BackupDbServer.csproj
   ```

### Option 2: Run as Windows Service

For unattended operation, run as a Windows service:

```powershell
# Run with administrator privileges
dotnet run --project BackupDbServer.csproj -- --service
```

**Note:** Service mode requires administrator privileges and is intended for server deployments.

## Configuration

### First-Time Setup

1. **Launch the application**
2. **Open Settings** (الإعدادات button)
3. **Configure connection:**
   - **Server Name:** Your SQL Server instance name (e.g., `localhost`, `192.168.1.100`)
   - **Port:** SQL Server port (default: 1433)
   - **Authentication:**
     - Windows Authentication (recommended for local)
     - SQL Authentication (username/password)
   - **Connection Type:** Local, Docker, or Remote
   - **Docker Container Name:** (if using Docker)
4. **Set backup path:** Choose where to store backups
5. **Select databases:** Choose which databases to backup
6. **Test connection** to verify settings
7. **Save settings**

### Connection Types

#### Local Connection
- SQL Server installed on the same machine
- Typically uses Windows Authentication
- Server name: `localhost` or `(local)`

#### Docker Connection
- SQL Server running in a Docker container
- Requires Docker Desktop to be running
- Provide container name (e.g., `sql-server-container`)
- Uses SQL Authentication (sa account)

#### Remote Connection
- SQL Server on another machine
- Provide IP address or hostname
- Usually requires SQL Authentication
- Ensure firewall allows connections on SQL port (1433)

## Usage

### Manual Backup

1. Open the main window
2. Refresh database list
3. Select databases to backup
4. Click "نسخ احتياطي" (Backup)
5. Monitor progress in the progress window
6. Backups are saved to the configured backup path

### Manual Restore

1. Click "استعادة" (Restore)
2. Select the database name
3. Browse for the backup file (.bak or .zip)
4. Click restore
5. Wait for completion

### Automatic Backups

1. Click "النسخ الاحتياطي التلقائي" (Auto Backup)
2. Enable auto backup
3. Choose schedule type:
   - **Interval-based:** Backup every X minutes
   - **Time-based:** Backup at specific time daily
4. Optionally enable:
   - Google Drive upload
   - Old backup cleanup
5. Save and start service

### Scheduled Backups

1. Click "النسخ المجدول" (Scheduled Backup)
2. Enable scheduled backup
3. Select days of the week
4. Set backup time
5. Configure retention policy (how many backups to keep)
6. Save and start service

### Daily Tasks

1. Click "المهام اليومية" (Daily Tasks)
2. Click "إضافة مهمة" (Add Task)
3. Configure task:
   - Task name
   - Database
   - Execution time
   - Task type (see 8 types above)
   - SQL script (if applicable)
4. Save task
5. Tasks run automatically at scheduled times
6. View countdown timer for next execution
7. Check execution status and results

### IIS Deployment

1. Click "نشر IIS" (Deploy IIS)
2. Fill in deployment details:
   - Site name
   - Application pool name
   - Port
   - Physical path
   - Database name
3. Click deploy
4. Application performs all 7 steps automatically
5. Site will be ready and running

## File Locations

### Configuration Files
All configuration files are stored in:
```
%APPDATA%\BackupDbServer\
```

- `settings.json` - Main application settings
- `auto_backup_config.json` - Auto backup configuration
- `scheduled_backup_config.json` - Scheduled backup settings
- `daily_tasks.json` - Daily task definitions
- `application.log` - Application log file

### Backup Files
Default backup locations (in Documents folder):
```
%USERPROFILE%\Documents\
├── LocalBackups\    - Local SQL Server backups
├── DockerBackups\   - Docker container backups
└── RemoteBackups\   - Remote server backups
```

You can change the backup path in Settings.

### Google Drive Credentials
```
DriveCredentials\Google.Apis.Auth.OAuth2.Responses.TokenResponse-user
```

## Troubleshooting

### Cannot Connect to SQL Server

**Problem:** "A network-related or instance-specific error occurred"

**Solutions:**
1. Verify SQL Server is running:
   - Open SQL Server Configuration Manager
   - Check SQL Server service status
2. Check server name is correct
3. Verify port number (default: 1433)
4. Disable firewall temporarily to test
5. For SQL Authentication:
   - Ensure SQL Server Authentication is enabled
   - Verify username and password are correct
   - Run the included script: `Enable_SQL_Authentication.sql`

### Docker Container Not Found

**Problem:** "Container not found" or "Docker is not running"

**Solutions:**
1. Start Docker Desktop
2. Verify container is running: `docker ps`
3. Check container name is correct
4. Ensure SQL Server in container is accessible

### Permission Denied

**Problem:** "Access denied" or "UnauthorizedAccessException"

**Solutions:**
1. Run application as administrator
2. Check backup folder permissions
3. Ensure SQL Server has write access to backup path
4. See: `حل_مشكلة_Access_Denied.md` (Arabic documentation)

### Backup Service Not Running

**Problem:** Scheduled backups not executing

**Solutions:**
1. Check service is enabled and started
2. Review `application.log` for errors
3. Verify settings are saved correctly
4. Restart the application
5. Check system time is correct

### Google Drive Upload Fails

**Problem:** Cannot upload to Google Drive

**Solutions:**
1. Re-authenticate Google Drive account
2. Check internet connection
3. Verify credentials file exists
4. Review log file for specific error

## Logs and Diagnostics

### Application Log
Location: `%APPDATA%\BackupDbServer\application.log`

The log file contains:
- `[INFO]` - Normal operations
- `[WARNING]` - Potential issues
- `[ERROR]` - Errors that occurred

### Crash Logs
Location: Project directory `crash_YYYYMMDD.log`

Created when application crashes unexpectedly.

### Viewing Logs
1. Open File Explorer
2. Type in address bar: `%APPDATA%\BackupDbServer`
3. Open `application.log` with any text editor
4. Review recent entries for errors

## Advanced Features

### Backup Retention Policies
Configure automatic cleanup of old backups:
- Keep last N backups
- Delete backups older than X days
- Maintains separate counts per database
- See: `RETENTION_POLICY_GUIDE.md`

### Network Path Support
Backup to network shares:
- Use UNC paths: `\\server\share\backups`
- Ensure proper network permissions
- See: `NetworkPathHelper.cs` implementation

### Crash Recovery
- Application detects unexpected shutdowns
- Restores previous state on restart
- Managed by `CrashProtectionManager`

## Documentation

Additional documentation files (in Arabic):
- `إرشادات_BackupService_الجديد.md` - BackupRestoreService guide
- `إرشادات_الإعدادات_الجديدة.md` - Settings system guide
- `إصلاح_مشاكل_النسخ_التلقائي.md` - Auto backup troubleshooting
- `إصلاح_مشكلة_جدولة_النسخ_الاحتياطي.md` - Schedule synchronization
- `حل_مشكلة_Access_Denied.md` - Permission issues
- `حل_مشكلة_حفظ_الإعدادات.md` - Settings save issues
- `دليل_حل_مشاكل_SQL_Authentication.md` - SQL authentication guide
- `RETENTION_POLICY_GUIDE.md` - Backup retention policies
- `ServiceContainer-QuickGuide.md` - Service container pattern guide

## Technology Stack

- **.NET 8.0** - Application framework
- **WPF** - User interface
- **XAML** - UI markup
- **Microsoft.Data.SqlClient** - SQL Server connectivity
- **Docker.DotNet** - Docker integration
- **Google.Apis.Drive.v3** - Google Drive integration
- **Microsoft.Web.Administration** - IIS management
- **System.Text.Json** - Configuration serialization

## Architecture

The application uses:
- **MVVM Pattern** - Separation of UI and business logic
- **Service Layer** - Centralized business logic
- **ServiceContainer Pattern** - Singleton service management
- **Repository Pattern** - Settings management
- **Dependency Injection** - Service dependencies

## Security Considerations

- **Credentials Storage:** SQL passwords are stored in local JSON files
  - Location: `%APPDATA%\BackupDbServer\settings.json`
  - Ensure proper file system permissions
  - Consider encrypting sensitive files
- **Windows Authentication:** Recommended for local/domain scenarios (no password storage)
- **Network Security:** Use encrypted connections for remote SQL Server
- **Backup Files:** Contain database data - secure backup storage locations
- **Google Drive:** OAuth tokens stored locally - delete `DriveCredentials` to revoke access

## Contributing

For development guidelines, see `CLAUDE.md` and `.github/copilot-instructions.md`.

## License

[Specify your license here]

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Review log files
3. Consult Arabic documentation files
4. Check `.github/ai_context.md` for technical details

---

**Version:** 2.0
**Last Updated:** December 2025
**Platform:** Windows 10+ with .NET 8.0
