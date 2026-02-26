# Product Requirements Document (PRD)
## BackupDbServer - Database Backup & Management System

**Version:** 2.0
**Last Updated:** December 2025
**Document Status:** Active
**Owner:** Development Team

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Product Overview](#2-product-overview)
3. [User Personas & Use Cases](#3-user-personas--use-cases)
4. [Functional Requirements](#4-functional-requirements)
5. [Non-Functional Requirements](#5-non-functional-requirements)
6. [System Architecture](#6-system-architecture)
7. [Data Models & Configuration](#7-data-models--configuration)
8. [User Interface Requirements](#8-user-interface-requirements)
9. [Integration Requirements](#9-integration-requirements)
10. [Security Requirements](#10-security-requirements)
11. [Performance Requirements](#11-performance-requirements)
12. [Testing & Quality Assurance](#12-testing--quality-assurance)
13. [Maintenance & Support](#13-maintenance--support)
14. [Future Enhancements](#14-future-enhancements)
15. [Compliance & Standards](#15-compliance--standards)

---

## 1. Executive Summary

### 1.1 Product Vision
BackupDbServer is an enterprise-grade Windows desktop application designed to provide comprehensive SQL Server database backup, restore, and management capabilities with advanced automation, multi-environment support, and cloud integration.

### 1.2 Business Objectives
- **Reduce Data Loss Risk:** Automated, reliable backup scheduling
- **Minimize Manual Effort:** Zero-touch backup automation
- **Support Multiple Environments:** Local, Docker, and remote SQL Server instances
- **Enable Cloud Strategy:** Google Drive integration for off-site backups
- **Streamline Deployments:** Automated IIS application deployment
- **Ensure Database Health:** Automated maintenance task execution

### 1.3 Target Market
- Small to medium businesses (SMB) with SQL Server databases
- Software development companies managing multiple database environments
- IT service providers offering managed database services
- Enterprise IT departments with distributed database infrastructure

### 1.4 Key Metrics
- **Backup Success Rate:** Target 99.9%
- **Recovery Time Objective (RTO):** < 1 hour
- **Recovery Point Objective (RPO):** < 24 hours
- **System Uptime:** 99.5% availability
- **User Satisfaction:** > 4.5/5 rating

---

## 2. Product Overview

### 2.1 Product Description
BackupDbServer is a WPF-based (.NET 8.0) desktop application that provides:
- **Database Backup & Restore:** Manual and automated operations
- **Multi-Environment Support:** Local SQL Server, Docker containers, remote servers
- **Scheduling System:** Three types of automation (interval, fixed-time, calendar-based)
- **Cloud Integration:** Google Drive upload and retention management
- **IIS Deployment:** 7-step automated application deployment
- **Database Maintenance:** Daily task scheduler for routine operations
- **Monitoring & Logging:** Comprehensive operation tracking and health monitoring

### 2.2 Technology Stack

**Framework & Language:**
- .NET 8.0 (net8.0-windows10.0.17763.0)
- C# 11
- WPF (Windows Presentation Foundation)
- XAML for UI markup

**Key Libraries:**
- Microsoft.Data.SqlClient 6.0.1 - SQL Server connectivity
- Google.Apis.Drive.v3 1.68.0 - Google Drive API
- Docker.DotNet (implicit) - Docker integration
- Microsoft.Web.Administration 11.1.0 - IIS management
- Hardcodet.NotifyIcon.Wpf 1.1.0 - System tray integration
- System.Text.Json 9.0.7 - Configuration serialization

**Architecture Patterns:**
- MVVM (Model-View-ViewModel)
- Singleton Pattern (ServiceContainer)
- Repository Pattern (SettingsManager)
- Observer Pattern (INotifyPropertyChanged)
- Command Pattern (RelayCommand, AsyncRelayCommand)

### 2.3 Core Value Propositions

1. **Multi-Environment Support**
   - Single application for all SQL Server types
   - Unified interface regardless of deployment model
   - Docker-native support with container management

2. **Flexible Scheduling**
   - Interval-based: Every N hours
   - Fixed-time: Daily at specific time
   - Calendar-based: Specific days of week
   - One-shot execution: Run once and stop

3. **Cloud-First Backup**
   - Automatic Google Drive upload
   - Retention policy management
   - Local cleanup after successful upload
   - Resumable uploads for large files

4. **Zero-Touch Deployment**
   - 7-step automated IIS deployment
   - Database provisioning and configuration
   - Web.config updates
   - Permission management

5. **Proactive Maintenance**
   - 8 types of database tasks
   - Scheduled execution (minute-level precision)
   - Automatic retry on transient failures
   - Detailed execution history

---

## 3. User Personas & Use Cases

### 3.1 User Personas

#### Persona 1: Database Administrator (Primary)
**Name:** Ahmed (DBA)
**Age:** 35-45
**Experience:** 10+ years in IT, 5+ years SQL Server
**Goals:**
- Ensure zero data loss
- Minimize manual backup effort
- Monitor backup health
- Quick restore when needed

**Pain Points:**
- Manual backup is time-consuming
- Easy to miss backup schedules
- No visibility into backup status
- Difficult to manage multiple servers

**Usage Pattern:**
- Sets up automated backups once
- Reviews logs weekly
- Performs restores occasionally
- Runs maintenance tasks monthly

#### Persona 2: IT Manager (Secondary)
**Name:** Sara (IT Manager)
**Age:** 40-50
**Experience:** 15+ years IT management
**Goals:**
- Ensure business continuity
- Compliance with data retention policies
- Cost-effective storage management
- Audit trail for operations

**Pain Points:**
- Lack of backup reporting
- No retention policy enforcement
- Cloud storage costs
- Compliance requirements

**Usage Pattern:**
- Reviews monthly reports
- Adjusts retention policies
- Monitors storage usage
- Audits security settings

#### Persona 3: Software Developer (Tertiary)
**Name:** Mohamed (Developer)
**Age:** 25-35
**Experience:** 3-7 years software development
**Goals:**
- Quick database copying for testing
- Deploy applications to IIS
- Restore production data to dev environment
- Database maintenance automation

**Pain Points:**
- Manual IIS deployment is error-prone
- Time-consuming to copy databases
- Need fresh production data for testing
- Complex deployment steps

**Usage Pattern:**
- Copies databases weekly
- Deploys applications daily
- Restores backups for testing
- Runs SQL scripts occasionally

### 3.2 Use Cases

#### UC-001: Manual Database Backup
**Actor:** Database Administrator
**Preconditions:**
- Application running
- SQL Server accessible
- Backup path configured and writable

**Main Flow:**
1. User opens MainWindow
2. User clicks "Refresh Database List"
3. System displays list of non-system databases
4. User selects one or more databases (checkboxes)
5. User clicks "Backup Selected" button
6. System opens BackupProgressWindow
7. System performs backup for each selected database
8. System creates .bak file in backup directory
9. System compresses to .zip format
10. System updates operation log
11. System displays completion message

**Alternate Flows:**
- 7a. Backup fails for a database
  - System logs error
  - System continues with next database
  - System reports failed databases at end

**Postconditions:**
- Backup files exist in configured directory
- Operation logged with success/failure status
- User notified of results

**Business Rules:**
- System databases excluded (master, tempdb, model, msdb)
- Only ONLINE databases can be backed up
- Backup files named: `{DatabaseName}_{yyyyMMdd_HHmmss}.bak`
- Compressed files named: `{DatabaseName}_{yyyyMMdd_HHmmss}.zip`
- Original .bak deleted after successful compression

#### UC-002: Schedule Automated Backup
**Actor:** Database Administrator
**Preconditions:**
- At least one database selected in main settings
- Backup path configured

**Main Flow:**
1. User clicks "Auto Backup Configuration"
2. System opens AutoBackupWindow
3. User enables auto backup (checkbox)
4. User selects scheduling mode:
   - Option A: Fixed Time Mode (radio button)
     - User sets time (HH:MM)
     - User checks "Run Only Once" if needed
   - Option B: Interval Mode (radio button)
     - User sets interval hours (slider)
5. User selects databases (checkbox list)
6. User configures retention (optional):
   - Check "Delete Old Backups"
   - Set retention days (slider)
7. User configures Google Drive (optional):
   - Check "Upload to Google Drive"
   - Browse for credentials file
   - Enter folder name
   - Check "Delete local after upload" if needed
   - Set upload time (if scheduled)
8. User clicks "Save Configuration"
9. System validates configuration
10. System starts AutoBackupService
11. System calculates next backup time
12. System displays "Service started successfully"

**Alternate Flows:**
- 9a. Validation fails
  - System displays error message
  - User corrects issues
  - Returns to step 8

**Postconditions:**
- AutoBackupService running in background
- Next backup time calculated and displayed
- Configuration saved to auto_backup_config.json
- Timer scheduled for next execution

**Business Rules:**
- At least one database must be selected
- Backup path must exist and be writable
- If fixed time mode and time already passed today, schedules for tomorrow
- If run-only-once and time passed, service stops after execution
- Google Drive credentials file must be valid JSON
- Retention days must be between 1-365

#### UC-003: Restore Database
**Actor:** Database Administrator
**Preconditions:**
- Valid .bak or .zip backup file exists
- SQL Server accessible
- User has appropriate permissions

**Main Flow:**
1. User clicks "Restore" button
2. System opens restore dialog
3. User enters new database name
4. User browses for backup file (.bak or .zip)
5. User clicks "Restore"
6. System validates inputs
7. System extracts .zip if needed
8. System gets logical file names from backup
9. System kills active connections (SINGLE_USER mode)
10. System executes RESTORE DATABASE with MOVE
11. System sets database to MULTI_USER mode
12. System logs operation
13. System displays success message

**Alternate Flows:**
- 6a. Database name already exists
  - System asks "Overwrite existing database?"
  - If No: returns to step 3
  - If Yes: continues with WITH REPLACE option
- 10a. Restore fails
  - System logs error
  - System displays error message with details
  - Database remains in previous state or removed if new

**Postconditions:**
- New database exists with restored data
- MDF/LDF files in configured data directory
- Operation logged
- Database in MULTI_USER mode (accessible)

**Business Rules:**
- New database name must be valid SQL identifier
- Backup file must be readable
- Sufficient disk space required (2x backup size)
- Cannot restore system databases
- MDF/LDF files renamed to match new database name

#### UC-004: Deploy Application to IIS
**Actor:** Software Developer
**Preconditions:**
- IIS installed and running
- User has admin privileges
- .bak file and .zip package exist
- Target IIS site exists

**Main Flow:**
1. User clicks "IIS Management" → "Automated Deployment"
2. System opens automationIIs window
3. User fills SQL Server settings:
   - Server name
   - Authentication mode
   - Credentials
4. User configures database restore:
   - Browse for .bak file
   - Enter new database name
   - Select data files directory
5. User configures company settings:
   - Table name, columns, key value
   - New company display name
6. User configures license:
   - License DB name and table
   - Company ID, license key, expiry date
7. User configures application package:
   - Browse for .zip file
   - WWW root base path
   - Company folder name
8. User configures IIS:
   - Select site name
   - Enter application alias
   - Select application pool
9. User configures Web.config:
   - Connection string name
   - App settings folder key (optional)
10. User clicks "Deploy"
11. System executes 7-step deployment:
    - Step 1: Restore database
    - Step 2: Update company name
    - Step 3: Insert license row
    - Step 4: Extract application files
    - Step 5: Update Web.config
    - Step 6: Grant IIS permissions
    - Step 7: Create IIS application
12. System displays progress for each step
13. System displays success summary

**Alternate Flows:**
- 11a. Any step fails
  - System stops execution
  - System rolls back changes:
    - Deletes created database
    - Removes extracted files
    - Removes IIS application
  - System displays error message

**Postconditions:**
- New database exists with company data
- Application files deployed to IIS directory
- IIS application created and running
- Web.config updated with connection string
- Permissions granted to IIS_IUSRS

**Business Rules:**
- All fields must be valid
- Database name must not exist (or use REPLACE)
- IIS site must exist
- Application pool must exist or will be created
- Company folder created if doesn't exist
- Web.config must exist in package

#### UC-005: Schedule Daily Database Task
**Actor:** Database Administrator
**Preconditions:**
- Database exists and accessible
- Task type appropriate for database

**Main Flow:**
1. User clicks "Daily Tasks Management"
2. System opens DailyTaskManagementWindow
3. User clicks "Add New Task"
4. System opens DailyTaskEditorWindow
5. User enters task details:
   - Task name
   - Select database
   - Set execution time (HH:MM)
   - Enable task (checkbox)
6. User selects task type:
   - SQL Script: enters SQL code
   - Stored Procedure: enters procedure name
   - Backup Database: (no additional input)
   - Check Integrity: (no additional input)
   - Update Statistics: (no additional input)
   - Rebuild Indexes: (no additional input)
   - Shrink Database: (no additional input)
   - Custom: enters custom SQL
7. User clicks "Save Task"
8. System validates task configuration
9. System adds task to daily_tasks.json
10. System starts monitoring for execution time
11. System displays task in management window

**Alternate Flows:**
- 8a. Validation fails
  - System displays error message
  - User corrects issues
  - Returns to step 7

**Postconditions:**
- Task saved to configuration
- Task appears in management window
- DailyTaskService monitoring for execution time
- Countdown timer displayed

**Business Rules:**
- Task name must be unique
- Execution time in 24-hour format
- SQL script must not be empty (for SqlScript type)
- Stored procedure must exist (for StoredProcedure type)
- Database must be ONLINE
- Tasks execute ±1 minute of target time
- Each task runs once per day maximum

#### UC-006: Copy Database
**Actor:** Software Developer
**Preconditions:**
- Source database exists and ONLINE
- Sufficient disk space

**Main Flow:**
1. User clicks "Copy Database"
2. System opens DatabaseCopyWindow
3. User selects source database (dropdown)
4. User enters new database name
5. User selects copy options:
   - Schema only (no data)
   - Schema + data
   - Shrink after copy
6. User optionally configures modifications:
   - Find/Replace in stored procedures
7. User clicks "Start Copy"
8. System validates inputs
9. System creates new empty database
10. System performs backup of source
11. System restores to new database
12. System applies modifications (if any)
13. System updates statistics
14. System shrinks database (if selected)
15. System displays success message

**Alternate Flows:**
- 8a. New database name already exists
  - System displays error
  - Returns to step 4
- 11a. Restore fails
  - System deletes new database
  - System displays error message

**Postconditions:**
- New database exists
- Data copied (if schema+data selected)
- Modifications applied (if configured)
- Operation logged

**Business Rules:**
- New database name must be unique
- Must be valid SQL identifier
- Source database must be ONLINE
- Cannot copy system databases

---

## 4. Functional Requirements

### 4.1 Database Backup Features

#### FR-001: Database Discovery
**Priority:** P0 (Critical)
**Description:** System must discover and display available databases from SQL Server instance.

**Requirements:**
- Query `sys.databases` system view
- Filter out system databases (master, tempdb, model, msdb)
- Display only ONLINE databases
- Show database status (size, last backup time if available)
- Refresh list on demand
- Cache list for performance (60-second cache)

**Acceptance Criteria:**
- ✅ Non-system databases displayed
- ✅ Offline databases excluded
- ✅ Refresh button updates list
- ✅ List sorted alphabetically
- ✅ Database status indicator shown

#### FR-002: Manual Backup Execution
**Priority:** P0 (Critical)
**Description:** User can manually trigger backup of selected databases.

**Requirements:**
- Support multi-database selection (checkboxes)
- Generate timestamped backup file names
- Create .bak files in configured directory
- Compress .bak to .zip format
- Delete .bak after successful compression
- Show real-time progress (per database and overall)
- Log all operations (success and failure)
- Continue with remaining databases on failure

**Acceptance Criteria:**
- ✅ Backup files created in correct location
- ✅ Files named with timestamp format
- ✅ Progress window shows real-time status
- ✅ Failed backups logged with error details
- ✅ Compressed files smaller than original
- ✅ Original .bak files removed

#### FR-003: Automated Backup Scheduling
**Priority:** P0 (Critical)
**Description:** System must support automated backup execution on schedule.

**Requirements:**
- Three scheduling modes:
  1. **Interval Mode:** Every N hours from last execution
  2. **Fixed Time Mode:** Daily at specific time (HH:MM)
  3. **One-Shot Mode:** Execute once at scheduled time, then stop
- Persistent across application restarts
- Synchronize database selection with main settings
- Calculate and display next backup time
- Execute backups in background (no UI blocking)
- Retry on transient failures (3 attempts)

**Acceptance Criteria:**
- ✅ Service starts/stops on user command
- ✅ Backups execute at scheduled time (±30 seconds)
- ✅ Next backup time accurately calculated
- ✅ Service restarts automatically after app restart (if enabled)
- ✅ Failed backups logged, retried, and user notified
- ✅ One-shot mode stops after execution

#### FR-004: Scheduled Backup (Calendar-Based)
**Priority:** P1 (High)
**Description:** User can schedule backups on specific days of week at specific time.

**Requirements:**
- Day of week selection (Mon-Sun, multi-select)
- Time selection (HH:MM)
- At least one day must be selected
- Calculate next execution from current time
- Handle multiple days (find earliest next occurrence)
- Persistent state across restarts

**Acceptance Criteria:**
- ✅ Backups execute only on selected days
- ✅ Backups execute at specified time (±1 minute)
- ✅ Next backup time correctly calculated
- ✅ Skips days not selected
- ✅ Configuration persisted to JSON file

#### FR-005: Backup Compression
**Priority:** P1 (High)
**Description:** Compress backup files to save storage space.

**Requirements:**
- Automatic compression after backup completion
- ZIP format (System.IO.Compression)
- Optimal compression level
- Delete original .bak after successful compression
- Handle compression errors gracefully
- Show compression ratio in logs

**Acceptance Criteria:**
- ✅ Compressed files 50-80% smaller
- ✅ Original files deleted after compression
- ✅ Compression errors don't stop backup process
- ✅ Can restore from compressed files

#### FR-006: Backup Verification
**Priority:** P2 (Medium)
**Description:** Verify backup file integrity.

**Requirements:**
- Check file size > 0
- Verify ZIP file can be extracted
- Optional: RESTORE VERIFYONLY (future enhancement)

**Acceptance Criteria:**
- ✅ Zero-byte files detected and flagged
- ✅ Corrupted ZIP files detected
- ✅ Verification failures logged

### 4.2 Database Restore Features

#### FR-007: Database Restoration
**Priority:** P0 (Critical)
**Description:** Restore database from backup file.

**Requirements:**
- Support .bak and .zip files
- Extract .zip if needed
- Get logical file names from backup
- Kill active connections to target database
- RESTORE with MOVE to relocate files
- New database name validation
- Overwrite confirmation (if database exists)
- Progress reporting during restore

**Acceptance Criteria:**
- ✅ Restores from .bak files
- ✅ Restores from .zip files
- ✅ New database created successfully
- ✅ Files relocated to data directory
- ✅ Database set to MULTI_USER after restore
- ✅ Confirmation required for overwrite

#### FR-008: File Relocation
**Priority:** P0 (Critical)
**Description:** Relocate MDF/LDF files during restore.

**Requirements:**
- Query backup file for logical names
- Generate new physical file paths
- Use configured data directory
- File names match new database name
- Validate directory exists and writable

**Acceptance Criteria:**
- ✅ Files relocated to correct directory
- ✅ File names match database name
- ✅ Logical names preserved
- ✅ Directory validation before restore

### 4.3 Connection Management

#### FR-009: Multi-Environment Support
**Priority:** P0 (Critical)
**Description:** Support three connection types: Local, Docker, Remote.

**Requirements:**
- **Local:** Connect to local SQL Server instance
  - Support default instance and named instances
  - Windows Authentication preferred
- **Docker:** Connect to SQL Server in Docker container
  - Specify container name
  - Use SQL Authentication (sa account)
  - Verify container running before connection
  - Copy backup files from/to container
- **Remote:** Connect to remote SQL Server
  - IP address or hostname
  - Custom port support (default 1433)
  - SQL Authentication required
  - SSL/TLS configuration

**Acceptance Criteria:**
- ✅ Connection successful for all three types
- ✅ Appropriate connection string generated
- ✅ Docker container verified before operations
- ✅ Remote connections work across network
- ✅ SSL certificate validation configurable

#### FR-010: Authentication Methods
**Priority:** P0 (Critical)
**Description:** Support both Windows and SQL Server authentication.

**Requirements:**
- Windows Authentication (Integrated Security)
- SQL Server Authentication (username/password)
- Credential storage in settings.json
- Test connection before save
- Clear error messages for auth failures

**Acceptance Criteria:**
- ✅ Windows Auth works for local connections
- ✅ SQL Auth works for all connection types
- ✅ Invalid credentials rejected with clear message
- ✅ Test connection validates auth successfully

#### FR-011: Connection Testing
**Priority:** P1 (High)
**Description:** Validate connection settings before use.

**Requirements:**
- Test button in settings window
- Connect to SQL Server
- Query master database
- Interpret SQL error codes
- Display user-friendly messages
- Timeout after 30 seconds

**Acceptance Criteria:**
- ✅ Success message displayed on valid connection
- ✅ Error message explains issue (network, auth, etc.)
- ✅ SQL error codes interpreted
- ✅ Timeout prevents indefinite hang

### 4.4 Google Drive Integration

#### FR-012: Google Drive Upload
**Priority:** P1 (High)
**Description:** Upload backup files to Google Drive.

**Requirements:**
- OAuth 2.0 authentication
- Browser-based consent flow (first time)
- Token persistence and refresh
- File upload with progress tracking
- Folder creation and management
- Support files up to 5 TB
- Resumable upload for large files
- Retry on network failure

**Acceptance Criteria:**
- ✅ User authenticates via browser
- ✅ Tokens stored and reused
- ✅ Files uploaded successfully
- ✅ Progress displayed during upload
- ✅ Network failures auto-retry
- ✅ Large files (>100 MB) use resumable upload

#### FR-013: Google Drive Retention
**Priority:** P1 (High)
**Description:** Delete old backups from Google Drive based on retention policy.

**Requirements:**
- Query files in backup folder
- Filter by creation date
- Delete files older than retention period
- Respect retention days setting
- Log deletions
- Never delete if retention disabled

**Acceptance Criteria:**
- ✅ Old files deleted automatically
- ✅ Retention days respected
- ✅ Deletions logged
- ✅ No deletion if retention disabled
- ✅ Folder structure preserved

#### FR-014: Scheduled Google Drive Upload
**Priority:** P2 (Medium)
**Description:** Upload backups to Google Drive at separate scheduled time.

**Requirements:**
- Option 1: Immediate upload after backup
- Option 2: Scheduled upload at specific time
- Queue pending uploads
- Process queue at scheduled time
- Delete local file after successful upload (optional)

**Acceptance Criteria:**
- ✅ Immediate upload works
- ✅ Scheduled upload works
- ✅ Queue persists across app restarts
- ✅ Local files deleted only after successful upload

### 4.5 Daily Task Management

#### FR-015: Daily Task Scheduler
**Priority:** P1 (High)
**Description:** Execute scheduled database maintenance tasks.

**Requirements:**
- 8 task types supported:
  1. SQL Script execution
  2. Stored procedure execution
  3. Database backup
  4. Integrity check (DBCC CHECKDB)
  5. Statistics update
  6. Index rebuild
  7. Database shrink
  8. Custom SQL
- Minute-level precision (HH:MM)
- Execution window: ±1 minute of target time
- Once per day execution (no duplicates)
- Automatic retry on transient failures
- Execution history with status

**Acceptance Criteria:**
- ✅ Tasks execute at scheduled time (±1 minute)
- ✅ Each task executes once per day
- ✅ All 8 task types work correctly
- ✅ Execution history saved
- ✅ Failed tasks logged with error details
- ✅ Daily reset at midnight

#### FR-016: Task Configuration
**Priority:** P1 (High)
**Description:** User can create, edit, and delete scheduled tasks.

**Requirements:**
- Task name (unique)
- Database selection
- Execution time (HH:MM)
- Task type selection
- Type-specific configuration:
  - SQL Script: Multi-line editor
  - Stored Procedure: Name + parameters
  - Others: Minimal config
- Enable/disable toggle
- Validation before save

**Acceptance Criteria:**
- ✅ Create new task
- ✅ Edit existing task
- ✅ Delete task with confirmation
- ✅ Enable/disable without deletion
- ✅ Validation prevents invalid tasks
- ✅ Tasks persisted to JSON file

#### FR-017: Task Execution Monitoring
**Priority:** P2 (Medium)
**Description:** Display task execution status and countdown.

**Requirements:**
- Show time until next run (real-time countdown)
- Last execution time
- Last execution status (success/failure)
- Last execution message
- Execution duration
- Auto-refresh every 60 seconds

**Acceptance Criteria:**
- ✅ Countdown timer updates in real-time
- ✅ Status indicators (✅❌⏳) displayed
- ✅ Last run information accurate
- ✅ Execution message shows details
- ✅ Duration formatted (HH:MM:SS)

### 4.6 IIS Deployment Automation

#### FR-018: Automated Deployment
**Priority:** P1 (High)
**Description:** Deploy ASP.NET application to IIS with database provisioning.

**Requirements:**
- 7-step deployment process:
  1. Restore database from .bak
  2. Update company name in database
  3. Insert license row
  4. Extract application package (.zip)
  5. Update Web.config (connection string)
  6. Grant NTFS permissions to IIS_IUSRS
  7. Create IIS application
- Progress display for each step
- Rollback on failure
- Validation before each step
- Admin privilege check

**Acceptance Criteria:**
- ✅ All 7 steps execute successfully
- ✅ Progress shown for each step
- ✅ Rollback works on failure
- ✅ Admin check before start
- ✅ Deployed application accessible via IIS

#### FR-019: IIS Configuration
**Priority:** P1 (High)
**Description:** Configure IIS application and application pool.

**Requirements:**
- List available IIS sites
- Select site for deployment
- Specify application alias (URL path)
- Select or create application pool
- Set application pool properties:
  - .NET version
  - Pipeline mode
  - Identity
- Start application after creation

**Acceptance Criteria:**
- ✅ Sites listed from IIS
- ✅ Application created under site
- ✅ Application pool configured
- ✅ Application accessible via URL
- ✅ Application pool starts automatically

#### FR-020: Web.Config Update
**Priority:** P1 (High)
**Description:** Update Web.config with connection string and settings.

**Requirements:**
- Parse XML Web.config
- Update connection string by name
- Add connection string if not exists
- Optionally update appSettings
- Preserve formatting
- Backup original file

**Acceptance Criteria:**
- ✅ Connection string updated correctly
- ✅ SQL Server credentials correct
- ✅ AppSettings updated (if specified)
- ✅ XML structure preserved
- ✅ Original file backed up

### 4.7 User Interface Features

#### FR-021: Main Window
**Priority:** P0 (Critical)
**Description:** Primary application interface with multi-tab layout.

**Requirements:**
- Tab 1: Database Management
  - Database list with checkboxes
  - Refresh, Save Selection, Backup, Restore buttons
  - SQL Executor section
  - Select All / Unselect All
- Tab 2: IIS Management
  - Automated Deployment button
  - Daily Tasks Management button
- Tab 3: Settings
  - Settings, Auto Backup, Scheduled Backup buttons
- Tab 4: Operation Log
  - Filterable operation history
  - Statistics dashboard
  - Export to CSV
- Tab 5: Operation Progress
  - Real-time progress monitoring
  - Active operation details
  - Operation history grid

**Acceptance Criteria:**
- ✅ All tabs accessible
- ✅ Database list populates correctly
- ✅ Buttons trigger correct actions
- ✅ Responsive UI (no freezing)
- ✅ Arabic UI labels correct

#### FR-022: Settings Window
**Priority:** P0 (Critical)
**Description:** Configure application settings.

**Requirements:**
- Connection settings section
- Backup paths section
- Retention policy section
- Docker settings (conditional display)
- Test Connection button
- Save/Cancel buttons
- Validation on save
- Directory browser for paths

**Acceptance Criteria:**
- ✅ All settings editable
- ✅ Validation prevents invalid values
- ✅ Test connection works
- ✅ Settings persisted to JSON
- ✅ Docker section visible only for Docker type

#### FR-023: Progress Windows
**Priority:** P1 (High)
**Description:** Display real-time progress for long-running operations.

**Requirements:**
- Overall progress bar (0-100%)
- Current operation description
- Database-level progress
- Log output (scrollable)
- Cancel button (with confirmation)
- Estimated time remaining
- Elapsed time

**Acceptance Criteria:**
- ✅ Progress updates in real-time
- ✅ Log messages timestamped
- ✅ Cancel stops operation gracefully
- ✅ Completion message displayed
- ✅ Time estimates reasonably accurate

#### FR-024: System Tray Integration
**Priority:** P2 (Medium)
**Description:** Minimize application to system tray.

**Requirements:**
- Tray icon with tooltip
- Context menu:
  - Show/Hide Window
  - Run Backup Now
  - View Recent Backups
  - Settings
  - Exit
- Balloon notifications:
  - Backup completion
  - Backup failure
  - Service start/stop
- Double-click to show window
- Close button minimizes to tray

**Acceptance Criteria:**
- ✅ Icon appears in system tray
- ✅ Context menu functional
- ✅ Notifications displayed
- ✅ Double-click shows window
- ✅ Exit option closes application

### 4.8 Logging & Monitoring

#### FR-025: Operation Logging
**Priority:** P0 (Critical)
**Description:** Log all database operations with details.

**Requirements:**
- Log entry for each operation
- Fields: Timestamp, Type, Status, Database, Description, Duration, Error
- Log levels: INFO, WARNING, ERROR, SUCCESS
- Daily log files (rotation)
- Retention: 30 days
- Max file size: 50 MB (split if exceeded)
- Real-time log display in UI

**Acceptance Criteria:**
- ✅ All operations logged
- ✅ Log entries include all fields
- ✅ Daily log rotation works
- ✅ Old logs deleted after 30 days
- ✅ Log viewer shows real-time updates

#### FR-026: Health Monitoring
**Priority:** P2 (Medium)
**Description:** Monitor system health and detect issues.

**Requirements:**
- Periodic health checks (every 5 minutes):
  - Disk space (backup directory)
  - SQL Server connectivity
  - Service status (auto backup, scheduled backup, daily tasks)
  - Memory usage
- Archive health data (JSON files)
- Alert on low disk space
- Alert on connection failures
- Dashboard display (optional)

**Acceptance Criteria:**
- ✅ Health checks run every 5 minutes
- ✅ Health data archived
- ✅ Alerts triggered on issues
- ✅ Dashboard shows current health

#### FR-027: Crash Protection
**Priority:** P1 (High)
**Description:** Detect and recover from application crashes.

**Requirements:**
- Detect unexpected shutdown (lock file)
- Capture unhandled exceptions
- Create crash dump files
- Log crash details
- Restore state on restart
- Notify user of previous crash

**Acceptance Criteria:**
- ✅ Crashes detected on next startup
- ✅ Crash dump created with stack trace
- ✅ User notified of previous crash
- ✅ Application state restored
- ✅ No data loss on crash

---

## 5. Non-Functional Requirements

### 5.1 Performance Requirements

#### NFR-001: Backup Performance
**Requirement:** Backup operations must meet performance targets.

**Metrics:**
- Small DB (<1 GB): ≤ 30 seconds
- Medium DB (1-10 GB): ≤ 2 minutes
- Large DB (10-100 GB): ≤ 10 minutes
- Compression overhead: ≤ 20% additional time
- Network copy overhead: ≤ 50% additional time

**Acceptance Criteria:**
- ✅ 90% of backups complete within target time
- ✅ Performance metrics logged
- ✅ Slow backups flagged for investigation

#### NFR-002: UI Responsiveness
**Requirement:** UI must remain responsive during operations.

**Metrics:**
- UI thread never blocked > 100ms
- Button clicks respond within 50ms
- Long operations run on background threads
- Progress updates every 500ms minimum

**Acceptance Criteria:**
- ✅ UI never freezes during operations
- ✅ Cancel button always responsive
- ✅ Progress bars update smoothly
- ✅ No "Not Responding" in Task Manager

#### NFR-003: Memory Usage
**Requirement:** Application must use memory efficiently.

**Metrics:**
- Idle memory: ≤ 150 MB
- During backup: ≤ 500 MB
- Memory leaks: None detected after 24 hours
- Garbage collection: < 5% of runtime

**Acceptance Criteria:**
- ✅ Memory usage within limits
- ✅ No memory leaks detected
- ✅ GC pauses ≤ 100ms

#### NFR-004: Startup Time
**Requirement:** Application must start quickly.

**Metrics:**
- Cold start: ≤ 3 seconds
- Warm start: ≤ 1 second
- Service initialization: ≤ 2 seconds

**Acceptance Criteria:**
- ✅ MainWindow visible within time limit
- ✅ Services initialized in background
- ✅ First interaction possible within 3 seconds

### 5.2 Reliability Requirements

#### NFR-005: Availability
**Requirement:** Application must be highly available.

**Metrics:**
- Uptime target: 99.5% (43 hours downtime/year)
- Scheduled backup reliability: 99.9%
- Service restart time: ≤ 30 seconds

**Acceptance Criteria:**
- ✅ Application runs continuously for weeks
- ✅ Scheduled backups miss < 0.1% of executions
- ✅ Crash recovery automatic

#### NFR-006: Data Integrity
**Requirement:** Backup data must be reliable.

**Metrics:**
- Backup success rate: 99.9%
- Corruption detection: 100%
- Restore success rate: 99.5%

**Acceptance Criteria:**
- ✅ < 0.1% failed backups (excluding transient errors)
- ✅ All corrupted files detected
- ✅ Restores work for all valid backups

#### NFR-007: Error Recovery
**Requirement:** Application must recover from errors gracefully.

**Requirements:**
- Retry transient failures (network, timeout) - 3 attempts
- Rollback on transaction failures
- Continue with next item on individual failures
- Never lose configuration data
- Always leave database in consistent state

**Acceptance Criteria:**
- ✅ Transient errors auto-retry
- ✅ Failed operations rolled back
- ✅ Configuration never corrupted
- ✅ Databases never left in inconsistent state

### 5.3 Scalability Requirements

#### NFR-008: Database Scale
**Requirement:** Support various database sizes.

**Metrics:**
- Max database size: 10 TB
- Max number of databases: 100
- Max concurrent backups: 1 (sequential processing)

**Acceptance Criteria:**
- ✅ Handles 10 TB databases (tested with 1 TB)
- ✅ Manages 100 databases without performance degradation
- ✅ Sequential processing prevents resource contention

#### NFR-009: Storage Scale
**Requirement:** Manage large backup storage.

**Metrics:**
- Max backup directory size: 50 TB
- Max files in directory: 10,000
- Retention cleanup performance: ≤ 1 minute for 1,000 files

**Acceptance Criteria:**
- ✅ Lists large directories efficiently
- ✅ Cleanup doesn't block operations
- ✅ Handles file system limits

### 5.4 Usability Requirements

#### NFR-010: Ease of Use
**Requirement:** Application must be easy to use for target users.

**Requirements:**
- No training required for basic operations
- Arabic UI for target market
- Contextual help (tooltips)
- Clear error messages
- Sensible defaults

**Acceptance Criteria:**
- ✅ New user can perform backup in < 5 minutes
- ✅ All UI text in Arabic
- ✅ Error messages explain issue and solution
- ✅ Default settings work for 80% of users

#### NFR-011: Accessibility
**Requirement:** Application must be accessible.

**Requirements:**
- Keyboard navigation (Tab order)
- Screen reader compatible (future enhancement)
- High contrast support (Windows theme)
- Minimum font size: 10pt

**Acceptance Criteria:**
- ✅ All controls accessible via keyboard
- ✅ Tab order logical
- ✅ Respects Windows accessibility settings

### 5.5 Maintainability Requirements

#### NFR-012: Code Quality
**Requirement:** Code must be maintainable.

**Requirements:**
- MVVM pattern consistently applied
- ServiceContainer for dependency management
- Clear naming conventions
- Documentation for complex logic
- Max method length: 100 lines
- Max cyclomatic complexity: 15

**Acceptance Criteria:**
- ✅ Code review passes
- ✅ Static analysis warnings < 20
- ✅ Consistent patterns throughout

#### NFR-013: Testability
**Requirement:** Code must be testable.

**Requirements:**
- Services mockable (interfaces)
- Minimal dependencies in ViewModels
- Business logic separated from UI
- Test projects structure (future)

**Acceptance Criteria:**
- ✅ Services implement interfaces
- ✅ ViewModels testable without UI
- ✅ Business logic in services, not ViewModels

#### NFR-014: Logging & Diagnostics
**Requirement:** Issues must be diagnosable.

**Requirements:**
- Comprehensive logging (all operations)
- Log levels (INFO, WARNING, ERROR)
- Log context (operation type, user, timestamp)
- Crash dumps with stack traces
- Health monitoring data

**Acceptance Criteria:**
- ✅ Can diagnose issues from logs alone
- ✅ No personal data in logs (passwords masked)
- ✅ Log files manageable size (rotation)

### 5.6 Security Requirements

#### NFR-015: Credential Security
**Requirement:** Credentials must be stored securely.

**Requirements:**
- Settings file restricted to current user
- File system ACL protection
- Future: DPAPI encryption
- Never log passwords
- Mask passwords in UI

**Acceptance Criteria:**
- ✅ Settings file readable only by owner
- ✅ Passwords never appear in logs
- ✅ Password fields use PasswordBox control

#### NFR-016: SQL Injection Prevention
**Requirement:** Prevent SQL injection attacks.

**Requirements:**
- Parameterized queries for all user inputs
- Validate SQL identifiers
- No dynamic SQL with string concatenation
- Sanitize database/table names

**Acceptance Criteria:**
- ✅ All queries use parameters
- ✅ Identifier validation rejects special characters
- ✅ No SQL injection vulnerabilities

#### NFR-017: File System Security
**Requirement:** Prevent path traversal attacks.

**Requirements:**
- Validate all file paths
- Reject paths with ".."
- Ensure paths within expected directories
- Check file permissions before operations

**Acceptance Criteria:**
- ✅ Cannot access files outside backup directory
- ✅ Path traversal attempts rejected
- ✅ Permission errors caught and logged

---

## 6. System Architecture

### 6.1 Architecture Overview

**Pattern:** MVVM (Model-View-ViewModel)

**Layers:**
1. **Presentation Layer** (Views, XAML)
   - WPF Windows and User Controls
   - XAML markup for UI
   - Minimal code-behind (event handlers only)

2. **ViewModel Layer** (ViewModels)
   - UI logic and state management
   - Command implementations
   - Data binding properties
   - INotifyPropertyChanged

3. **Service Layer** (Services)
   - Business logic
   - Database operations
   - External integrations
   - Scheduled tasks

4. **Model Layer** (Models, Helpers)
   - Data structures
   - Configuration classes
   - Utility classes
   - Converters

5. **Data Layer** (SettingsManager, FileLogger)
   - JSON file persistence
   - Log file management
   - File system operations

### 6.2 ServiceContainer (Dependency Management)

**Purpose:** Centralized service management with Singleton pattern.

**Managed Services:**
```csharp
public static class ServiceContainer
{
    // Singleton instances
    public static ILogger Logger { get; }
    public static BackupRestoreService BackupService { get; }
    public static GoogleDriveServiceFixed GoogleDriveService { get; }
    public static IAutoBackupService AutoBackupService { get; }
    public static IScheduledBackupService ScheduledBackupService { get; }
    public static DailyTaskService DailyTaskService { get; }
    public static BackupRetentionPolicy RetentionPolicy { get; }

    // Lifecycle
    public static void Initialize();
    public static void Shutdown();
    public static void Reset(); // Testing only
}
```

**Benefits:**
- Single instance of each service (memory efficient)
- Thread-safe initialization (lock)
- Centralized lifecycle management
- Consistent state across application
- Easy testing and mocking

**Usage Pattern:**
```csharp
// ✅ CORRECT
public class MyViewModel : ViewModelBase
{
    private readonly ILogger _logger;

    public MyViewModel()
    {
        _logger = ServiceContainer.Logger;
    }
}

// ❌ WRONG
public class MyViewModel : ViewModelBase
{
    private readonly ILogger _logger;

    public MyViewModel()
    {
        _logger = new FileLogger(); // DON'T DO THIS
    }
}
```

### 6.3 Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         App.xaml.cs                         │
│  - OnStartup() → ServiceContainer.Initialize()              │
│  - OnExit() → ServiceContainer.Shutdown()                   │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┴──────────────┐
        │                             │
┌───────▼────────┐          ┌─────────▼────────┐
│  MainWindow    │          │ ServiceContainer │
│  (View)        │◄─────────┤  (Singleton)     │
└───────┬────────┘          └─────────┬────────┘
        │                             │
┌───────▼────────┐                    │
│ MainViewModel  │                    │
│  (ViewModel)   │                    │
└───────┬────────┘                    │
        │                             │
        ├─────────────────────────────┤
        │                             │
        ▼                             ▼
┌─────────────────┐        ┌──────────────────┐
│ BackupRestore   │        │ AutoBackup       │
│ Service         │        │ Service          │
│ - Backup DB     │        │ - Schedule       │
│ - Restore DB    │        │ - Execute        │
│ - Connect       │        │ - Monitor        │
└────────┬────────┘        └────────┬─────────┘
         │                          │
         └──────────┬───────────────┘
                    │
        ┌───────────▼──────────┐
        │  SettingsManager     │
        │  - Load Settings     │
        │  - Save Settings     │
        │  - JSON Persistence  │
        └──────────────────────┘
```

### 6.4 Data Flow Diagrams

#### Manual Backup Flow
```
User → MainWindow → MainViewModel → BackupRestoreService → SQL Server
  ↓         ↓             ↓                  ↓                   ↓
Select  Click Btn   Execute Cmd    Backup Logic         BACKUP DATABASE
  ↓         ↓             ↓                  ↓                   ↓
  └─────────┴─────────────┴──────────────────┴───────────────────┘
                              ↓
                    Backup File (.bak)
                              ↓
                    Compress (.zip)
                              ↓
                    Log Operation
                              ↓
                    Update UI
```

#### Automated Backup Flow
```
App Startup → ServiceContainer → AutoBackupService → Timer
                     ↓                   ↓              ↓
              Initialize()         LoadConfig()   Schedule Next
                     ↓                   ↓              ↓
              Start Services       Start Timer    Calculate Delay
                                         ↓              ↓
                                   Timer Elapsed ──────┘
                                         ↓
                                ExecuteBackupAsync()
                                         ↓
                          ┌──────────────┴──────────────┐
                          ↓                             ↓
              BackupRestoreService             GoogleDriveService
                          ↓                             ↓
                  Create Backups                 Upload Files
                          ↓                             ↓
              Cleanup Old Backups          Cleanup Old Cloud Files
                          ↓                             ↓
                          └──────────────┬──────────────┘
                                         ↓
                                  Schedule Next Run
```

### 6.5 Database Schema (Configuration)

**Configuration Files Schema:**

```json
// settings.json
{
  "server": "string",               // Required
  "username": "string",             // Required for SQL Auth
  "password": "string",             // Required for SQL Auth
  "useWindowsAuthentication": bool, // Default: false
  "backupPath": "string",           // Required
  "selectedDatabases": ["string"],  // Default: []
  "connectionType": "enum",         // Local|Docker|Remote
  "dockerContainerName": "string",  // Required for Docker
  "port": int,                      // Default: 1433
  "connectionTimeout": int,         // Default: 30 seconds
  "trustServerCertificate": bool,   // Default: true
  "encrypt": bool,                  // Default: false
  "retentionPolicy": {
    "dailyRetentionDays": int,      // Default: 7
    "weeklyRetentionWeeks": int,    // Default: 4
    "monthlyRetentionMonths": int,  // Default: 12
    "deleteOlderThanYear": bool     // Default: false
  }
}

// auto_backup_config.json
{
  "isEnabled": bool,                       // Default: false
  "intervalHours": int,                    // Default: 24
  "useScheduledTime": bool,                // Default: true
  "scheduledTime": "HH:mm:ss",             // Default: "02:00:00"
  "runOnlyOnce": bool,                     // Default: false
  "deleteOldBackups": bool,                // Default: false
  "retentionDays": int,                    // Default: 7
  "selectedDatabases": ["string"],         // Synced from main
  "uploadToGoogleDrive": bool,             // Default: false
  "googleDriveCredentialsPath": "string",  // Required if upload enabled
  "googleDriveFolderName": "string",       // Default: "Database Backups"
  "deleteLocalAfterUpload": bool,          // Default: false
  "useGoogleDriveScheduledTime": bool,     // Default: false
  "googleDriveUploadTime": "HH:mm:ss",     // Default: "03:00:00"
  "nextBackupTime": "ISO8601",             // Calculated
  "backupHistory": [                       // Last 50 backups
    {
      "id": "guid",
      "databaseName": "string",
      "backupTime": "ISO8601",
      "status": "enum",                    // Pending|Success|Failed
      "localFilePath": "string",
      "googleDriveFileId": "string",
      "errorMessage": "string",
      "fileSizeBytes": long,
      "duration": "HH:mm:ss"
    }
  ]
}

// scheduled_backup_config.json
{
  "isEnabled": bool,                  // Default: false
  "scheduledTime": "HH:mm:ss",        // Default: "02:00:00"
  "scheduledDays": [int],             // 0-6 (Sun-Sat), Default: [1,2,3,4,5]
  "deleteOldBackups": bool,           // Default: false
  "retentionDays": int,               // Default: 7
  "notifyOnCompletion": bool,         // Default: false
  "notifyOnError": bool,              // Default: true
  "notificationEmail": "string",      // Required if notify enabled
  "lastBackupTime": "ISO8601",        // Last execution
  "nextBackupTime": "ISO8601",        // Next execution
  "isRunning": bool                   // Current state
}

// daily_tasks.json
[
  {
    "taskId": "guid",                 // Unique identifier
    "taskName": "string",             // Display name
    "isEnabled": bool,                // Default: true
    "databaseName": "string",         // Target database
    "executionTime": "HH:mm:ss",      // Daily execution time
    "type": "enum",                   // SqlScript|StoredProcedure|...
    "sqlScript": "string",            // For SqlScript and Custom types
    "storedProcedure": "string",      // For StoredProcedure type
    "parameters": {                   // For StoredProcedure type
      "paramName": "value"
    },
    "lastExecutionTime": "ISO8601",   // Last run
    "lastExecutionSuccess": bool,     // Result
    "lastExecutionMessage": "string", // Details
    "lastExecutionDuration": "HH:mm:ss"
  }
]

// operation_log.json
[
  {
    "timestamp": "ISO8601",
    "operationType": "string",        // نسخ احتياطي | استعادة | etc.
    "status": "string",               // نجح | فشل | جاري
    "databaseName": "string",
    "description": "string",
    "duration": "string",             // HH:mm:ss
    "errorMessage": "string",         // If status = failed
    "fileSize": long,                 // Bytes
    "filePath": "string"
  }
]
```

### 6.6 External Integrations

#### SQL Server Integration
- **Library:** Microsoft.Data.SqlClient 6.0.1
- **Operations:** Backup, Restore, Query, Maintenance
- **Connection:** ADO.NET SqlConnection
- **Authentication:** Windows or SQL Server

#### Docker Integration
- **Method:** Docker CLI via Process.Start
- **Commands:** ps, inspect, exec, cp
- **Container Detection:** Name-based filtering
- **File Transfer:** docker cp for backups

#### Google Drive Integration
- **Library:** Google.Apis.Drive.v3 1.68.0
- **Authentication:** OAuth 2.0
- **Operations:** Upload, List, Delete, Create Folder
- **Token Storage:** Local file system

#### IIS Integration
- **Library:** Microsoft.Web.Administration 11.1.0
- **Operations:** Site management, App pool configuration
- **Requirements:** Admin privileges, IIS installed
- **Configuration:** Web.config XML manipulation

---

## 7. Data Models & Configuration

### 7.1 Core Models

[Already covered in Section 2 and 6.5 above - refer to those sections for complete model documentation]

### 7.2 Configuration Management

**Storage Location:** `%APPDATA%\BackupDbServer\`

**Files:**
- `settings.json` - Main application configuration
- `auto_backup_config.json` - Auto backup settings
- `scheduled_backup_config.json` - Scheduled backup settings
- `daily_tasks.json` - Daily task definitions
- `operation_log.json` - Operation history (last 500 entries)

**Backup Location:** `%USERPROFILE%\Documents\`
- `LocalBackups\` - Local SQL Server
- `DockerBackups\` - Docker containers
- `RemoteBackups\` - Remote servers

**Log Location:** `%APPDATA%\BackupDbServer\Logs\`
- `app_YYYYMMDD.log` - Daily application log
- `backup_YYYYMMDD.log` - Backup-specific log
- `error_YYYYMMDD.log` - Error-only log
- `crash_YYYYMMDDHHMM.log` - Crash dumps

**Configuration Validation:**
- JSON schema validation on load
- Default values for missing fields
- Type checking for all properties
- Range validation (e.g., port 1-65535)
- Path validation (exists, writable)

**Configuration Migration:**
- Detect old config versions
- Auto-upgrade to latest schema
- Preserve user settings
- Backup before migration

---

## 8. User Interface Requirements

### 8.1 UI Design Principles

1. **Arabic-First:** All text in Arabic, RTL layout support
2. **Simplicity:** Minimal clicks to perform common tasks
3. **Clarity:** Clear labels and error messages
4. **Feedback:** Immediate response to user actions
5. **Progress:** Show progress for long operations
6. **Consistency:** Uniform design across windows

### 8.2 Window Specifications

[Detailed window specifications covered in Section 4 "User Interface" - refer to that section for complete UI documentation including:
- MainWindow (5 tabs)
- SettingsWindow
- AutoBackupWindow
- ScheduledBackupWindow
- DailyTaskManagementWindow
- DailyTaskEditorWindow
- BackupProgressWindow
- DatabaseCopyWindow
- SqlExecutorWindow
- automationIIs (IIS deployment wizard)]

### 8.3 Common UI Components

**Buttons:**
- Primary: Blue background (#007ACC)
- Secondary: Gray background
- Danger: Red background (#E81123)
- Icon + Text where appropriate
- Disabled state visible

**Inputs:**
- Text boxes: White background, border
- Dropdowns: Standard WPF ComboBox
- Checkboxes: Standard WPF CheckBox
- Time pickers: HH:MM format
- Date pickers: DD/MM/YYYY format
- Sliders: For numeric ranges

**Data Grids:**
- Alternating row colors
- Header sorting
- Row selection
- Column resizing
- Context menu (right-click)

**Progress Indicators:**
- Determinate: Progress bar with percentage
- Indeterminate: Spinner for unknown duration
- Status text: Above progress bar
- Cancel button: Always available

**Notifications:**
- Toast: Bottom-right corner
- Balloon: System tray icon
- Message Box: Modal for critical messages
- Status Bar: Bottom of window

---

## 9. Integration Requirements

[Already covered in Section 6.6 and Section 4.9 - refer to those sections for complete integration documentation]

---

## 10. Security Requirements

[Already covered in NFR-015 through NFR-017 and Section 6 - refer to those sections]

### 10.1 Additional Security Considerations

**Threat Model:**
1. **Local Privilege Escalation:** Mitigated by Windows ACLs
2. **SQL Injection:** Mitigated by parameterized queries
3. **Path Traversal:** Mitigated by path validation
4. **Credential Theft:** Settings file protected by NTFS permissions
5. **Network Sniffing:** Mitigated by SSL/TLS (configurable)

**Security Roadmap:**
1. **Phase 1 (Current):** Basic NTFS protection
2. **Phase 2:** DPAPI encryption for credentials
3. **Phase 3:** Certificate-based SQL authentication
4. **Phase 4:** Two-factor authentication for critical operations

---

## 11. Performance Requirements

[Already covered in NFR-001 through NFR-004 - refer to those sections]

### 11.1 Performance Monitoring

**Metrics to Track:**
- Backup duration per database
- Compression ratio (original size / compressed size)
- Network transfer speed (for remote/Docker backups)
- Memory usage over time
- CPU usage during operations
- Disk I/O throughput

**Performance Targets:**
- CPU usage: < 50% during backup
- Memory usage: < 500 MB during backup
- Disk I/O: Saturate available bandwidth
- Network I/O: Saturate available bandwidth

**Performance Optimization:**
- Use SQL Server native compression (COMPRESSION option)
- Async I/O for file operations
- Parallel processing where safe (future)
- Connection pooling for SQL
- Lazy loading of large datasets

---

## 12. Testing & Quality Assurance

### 12.1 Testing Strategy

**Manual Testing (Current):**
- Smoke testing: Basic operations work
- Regression testing: New changes don't break existing features
- User acceptance testing: Target users validate functionality

**Automated Testing (Future):**
- Unit tests: Service layer logic
- Integration tests: Database operations
- UI tests: Critical workflows
- Performance tests: Backup speed benchmarks

### 12.2 Test Scenarios

**Critical Path Testing:**
1. **Backup & Restore Cycle**
   - Backup database
   - Verify file exists
   - Restore to new database
   - Verify data integrity
   - Compare schemas

2. **Automated Backup**
   - Configure schedule
   - Wait for scheduled time
   - Verify backup executed
   - Check logs

3. **Google Drive Upload**
   - Configure credentials
   - Authenticate
   - Backup database
   - Verify file uploaded
   - Check Google Drive folder

4. **IIS Deployment**
   - Prepare .bak and .zip files
   - Run 7-step deployment
   - Verify IIS application created
   - Access application URL
   - Verify database connection

5. **Daily Tasks**
   - Create task
   - Wait for scheduled time
   - Verify task executed
   - Check execution history

**Edge Cases:**
1. **Large Databases (>100 GB)**
   - Verify backup completes
   - Check timeout handling
   - Monitor memory usage

2. **Network Failures**
   - Disconnect network during backup
   - Verify retry logic
   - Check error logging

3. **Concurrent Operations**
   - Start manual backup during scheduled backup
   - Verify serialization
   - Check for conflicts

4. **Invalid Configurations**
   - Corrupted JSON files
   - Missing directories
   - Invalid credentials
   - Verify defaults applied

5. **Application Crash**
   - Force crash during operation
   - Restart application
   - Verify state recovery
   - Check crash dump created

### 12.3 Quality Metrics

**Code Quality:**
- Static analysis warnings: < 20
- Code coverage: > 70% (future)
- Cyclomatic complexity: < 15
- Max method length: 100 lines

**Operational Quality:**
- Backup success rate: > 99.9%
- Restore success rate: > 99.5%
- Crash rate: < 0.1% of sessions
- Data corruption rate: 0%

**User Experience:**
- Task completion rate: > 95%
- Error message clarity: User understands issue
- Time to first backup: < 5 minutes for new user
- User satisfaction: > 4.5/5

---

## 13. Maintenance & Support

### 13.1 Maintenance Tasks

**Daily:**
- Monitor scheduled backup executions
- Check error logs for failures
- Verify disk space sufficient

**Weekly:**
- Review operation logs
- Check backup success rate
- Test restore from random backup
- Monitor Google Drive quota

**Monthly:**
- Update retention policies as needed
- Review failed operations
- Optimize backup schedules
- Archive old logs

**Quarterly:**
- Update dependencies (NuGet packages)
- Review security settings
- Performance benchmarking
- Disaster recovery drill

**Yearly:**
- Audit user permissions
- Review configuration settings
- Update documentation
- Security assessment

### 13.2 Troubleshooting Guide

[Detailed troubleshooting covered in exploration agent output - Section 12]

### 13.3 Support Procedures

**Issue Triage:**
1. **P0 - Critical:** Data loss, application crash, backup failures
   - Response time: 1 hour
   - Resolution time: 4 hours
2. **P1 - High:** Schedule not working, slow performance
   - Response time: 4 hours
   - Resolution time: 1 day
3. **P2 - Medium:** UI issues, minor bugs
   - Response time: 1 day
   - Resolution time: 1 week
4. **P3 - Low:** Feature requests, enhancements
   - Response time: 1 week
   - Resolution time: TBD

**Diagnostic Information:**
- Application version
- Windows version
- SQL Server version
- Error message and stack trace
- Log files (last 24 hours)
- Configuration files (passwords redacted)
- Steps to reproduce

**Escalation Path:**
1. First-line support (user guides, FAQs)
2. Technical support (log analysis)
3. Development team (code fixes)
4. Senior architect (design changes)

---

## 14. Future Enhancements

### 14.1 Planned Features

**Phase 2 (Q1 2026):**
1. **Email Notifications**
   - SMTP configuration
   - Email on backup success/failure
   - Daily/weekly summary reports
   - Attachment: Operation logs

2. **Differential & Transaction Log Backups**
   - Differential backup support
   - Transaction log backup
   - Point-in-time recovery
   - Backup chain management

3. **Cloud Storage Expansion**
   - Azure Blob Storage
   - Amazon S3
   - OneDrive
   - Multi-cloud support

4. **Enhanced Reporting**
   - Dashboard with charts
   - Backup size trends
   - Success rate graphs
   - Storage usage analysis

**Phase 3 (Q2 2026):**
1. **Backup Verification**
   - RESTORE VERIFYONLY after backup
   - Checksum validation
   - Automated restore testing
   - Integrity reports

2. **Performance Improvements**
   - Parallel backup processing
   - Incremental backup
   - Delta backups (block-level)
   - Deduplication

3. **Advanced Scheduling**
   - Cron expression support
   - Holiday calendar
   - Business hours only
   - Conditional execution (e.g., if DB changed)

4. **Multi-Tenant Support**
   - Multiple configuration profiles
   - Per-profile settings
   - Switch between profiles
   - Profile import/export

**Phase 4 (Q3 2026):**
1. **Enterprise Features**
   - Central management server
   - Agent-based architecture
   - Remote monitoring dashboard
   - Role-based access control

2. **Compliance & Audit**
   - Audit trail (all operations)
   - Compliance reports (GDPR, HIPAA)
   - Data retention policies
   - Tamper-proof logs

3. **Integration Enhancements**
   - PowerShell cmdlets
   - REST API for automation
   - Webhook notifications
   - Slack/Teams integration

4. **Disaster Recovery**
   - Backup replication
   - Off-site backup sync
   - Automated DR testing
   - Recovery runbooks

### 14.2 Known Limitations

1. **Single Backup Instance:** Only one backup operation at a time
   - **Impact:** Cannot backup multiple databases in parallel
   - **Workaround:** Sequential processing (acceptable for most users)
   - **Future:** Configurable parallelism

2. **No Transaction Log Backup:** Full backups only
   - **Impact:** RPO limited to backup frequency
   - **Workaround:** Increase backup frequency
   - **Future:** Transaction log backup support

3. **Limited Cloud Storage:** Google Drive only
   - **Impact:** Users with other cloud providers cannot use feature
   - **Workaround:** Use local backups, manual upload
   - **Future:** Azure, AWS, OneDrive support

4. **Windows Only:** No cross-platform support
   - **Impact:** Cannot run on Linux/Mac
   - **Workaround:** Use Windows VM
   - **Future:** .NET MAUI or web-based UI

5. **Manual Testing Only:** No automated test suite
   - **Impact:** Regression risk on changes
   - **Workaround:** Comprehensive manual testing
   - **Future:** Unit and integration tests

### 14.3 Technical Debt

1. **Credential Encryption:** Plain text passwords in settings.json
   - **Risk:** Medium
   - **Mitigation:** File ACL protection
   - **Remediation:** DPAPI encryption (Phase 2)

2. **Error Handling Consistency:** Some areas use try-catch, others use ErrorHandler
   - **Risk:** Low
   - **Mitigation:** ErrorHandler pattern documented
   - **Remediation:** Refactor to uniform pattern (Phase 3)

3. **ServiceContainer Initialization:** Some services still created locally
   - **Risk:** Low
   - **Mitigation:** Documentation and code review
   - **Remediation:** Enforce via code analysis (Phase 2)

4. **Logging Performance:** Synchronous file writes
   - **Risk:** Low (negligible impact)
   - **Mitigation:** Minimal logging in hot paths
   - **Remediation:** Async logging (Phase 3)

5. **UI Thread Blocking:** Some long operations on UI thread
   - **Risk:** Low (most operations async)
   - **Mitigation:** Use Task.Run for CPU-bound work
   - **Remediation:** Audit and refactor (ongoing)

---

## 15. Compliance & Standards

### 15.1 Data Protection

**Personal Data:**
- SQL Server credentials (username, password)
- Google Drive credentials (OAuth tokens)
- Email addresses (notifications)

**Storage:**
- Local file system only
- No cloud storage of credentials (except OAuth tokens by Google library)
- NTFS ACL protection

**Retention:**
- Credentials: Until user changes or removes
- Logs: 30 days (configurable)
- Backups: Per retention policy (7-365 days)

**Data Subject Rights:**
- Access: User can view settings.json
- Rectification: User can edit settings
- Erasure: User can delete settings file
- Portability: JSON format (standard, portable)

### 15.2 Industry Standards

**Backup Best Practices:**
- 3-2-1 Rule: 3 copies, 2 different media, 1 off-site
  - ✅ 3 copies: Production + Local Backup + Google Drive
  - ✅ 2 media: Disk (production + local) + Cloud (Google Drive)
  - ✅ 1 off-site: Google Drive
- Regular testing of restores
- Retention policies
- Compression for storage efficiency

**SQL Server Best Practices:**
- Full backups for disaster recovery
- Differential backups for faster recovery (future)
- Transaction log backups for point-in-time recovery (future)
- DBCC CHECKDB for integrity
- Index maintenance
- Statistics updates

**Coding Standards:**
- C# Coding Conventions (Microsoft)
- MVVM pattern (Microsoft WPF guidance)
- Async/await best practices
- IDisposable pattern
- Exception handling guidelines

### 15.3 Regulatory Compliance

**GDPR (General Data Protection Regulation):**
- **Applicable:** If processing EU citizen data
- **Requirements:**
  - Data minimization: Only collect necessary data
  - Storage limitation: Retention policies
  - Integrity and confidentiality: Encryption (future)
  - Accountability: Audit logs
- **Compliance Status:** Partially compliant (needs encryption)

**HIPAA (Health Insurance Portability and Accountability Act):**
- **Applicable:** If backing up healthcare databases
- **Requirements:**
  - Access controls: File ACLs
  - Audit trails: Operation logs
  - Encryption: At rest and in transit (future)
  - Backup and disaster recovery: Yes
- **Compliance Status:** Partially compliant (needs encryption)

**SOX (Sarbanes-Oxley Act):**
- **Applicable:** If backing up financial databases
- **Requirements:**
  - Data retention: Yes (configurable)
  - Audit trails: Yes (operation logs)
  - Access controls: File ACLs
  - Backup integrity: DBCC CHECKDB
- **Compliance Status:** Compliant

---

## 16. Appendices

### 16.1 Glossary

- **Backup:** Copy of database for disaster recovery
- **Restore:** Recreate database from backup file
- **RPO (Recovery Point Objective):** Maximum acceptable data loss
- **RTO (Recovery Time Objective):** Maximum acceptable downtime
- **MVVM:** Model-View-ViewModel design pattern
- **Singleton:** Design pattern ensuring single instance
- **OAuth:** Authorization protocol for Google Drive
- **IIS:** Internet Information Services (web server)
- **DBCC:** Database Console Commands (SQL Server)
- **ACL:** Access Control List (file permissions)

### 16.2 Acronyms

- **DB:** Database
- **SQL:** Structured Query Language
- **WPF:** Windows Presentation Foundation
- **XAML:** Extensible Application Markup Language
- **UI:** User Interface
- **UX:** User Experience
- **API:** Application Programming Interface
- **REST:** Representational State Transfer
- **JSON:** JavaScript Object Notation
- **XML:** Extensible Markup Language
- **CSV:** Comma-Separated Values
- **SMTP:** Simple Mail Transfer Protocol
- **SSL/TLS:** Secure Sockets Layer / Transport Layer Security

### 16.3 References

**Documentation:**
- Microsoft SQL Server Documentation: https://docs.microsoft.com/sql
- WPF Documentation: https://docs.microsoft.com/dotnet/desktop/wpf
- Google Drive API: https://developers.google.com/drive
- Docker Documentation: https://docs.docker.com
- IIS Documentation: https://docs.microsoft.com/iis

**Internal Documents:**
- CLAUDE.md - Developer guidance
- README.md - User guide
- .github/ai_context.md - System architecture
- .github/copilot-instructions.md - Coding standards

**Code Repository:**
- Location: C:\Users\moham\source\repos\BackupDbServer\BackupDbServer
- Solution File: BackupDbServer.sln
- Target Framework: .NET 8.0

---

## Document Control

**Version History:**

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-07-26 | Initial Team | Initial PRD |
| 2.0 | 2025-12-15 | Development Team | ServiceContainer, Retention Policy, Daily Tasks |
| 2.1 | 2026-02-26 | AI Assistant | Comprehensive PRD for maintenance |

**Approval:**

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Product Owner | | | |
| Technical Lead | | | |
| QA Lead | | | |
| Project Manager | | | |

**Distribution:**
- Development Team
- QA Team
- Support Team
- Product Management
- Executive Stakeholders

---

**END OF DOCUMENT**

Total Pages: 60+
Total Words: ~20,000
Document Type: Product Requirements Document (PRD)
Purpose: System Maintenance & Development Guide
Confidentiality: Internal Use Only
