<!-- 
Sync Impact Report
- Version change: N/A → 1.0.0
- List of modified principles:
    - [PRINCIPLE_1_NAME] → I. ServiceContainer-First
    - [PRINCIPLE_2_NAME] → II. Modern SQL Connectivity
    - [PRINCIPLE_3_NAME] → III. Unified Error & Progress Handling
    - [PRINCIPLE_4_NAME] → IV. MVVM Methodology
    - [PRINCIPLE_5_NAME] → V. Multi-Platform SQL Abstraction
- Added sections: Performance & Reliability Standards, Deployment & Maintenance Workflow
- Removed sections: None
- Templates requiring updates:
    - ✅ updated / .specify/templates/plan-template.md
    - ✅ updated / .specify/templates/spec-template.md
    - ✅ updated / .specify/templates/tasks-template.md
- Follow-up TODOs: None
-->
# BackupDbServer Constitution

## Core Principles

### I. ServiceContainer-First
All shared services MUST be accessed through the centralized `ServiceContainer` helper located in `Helpers/ServiceContainer.cs`. Never instantiate services directly (e.g., `new FileLogger()`) to ensure single instance integrity and lifecycle management.

### II. Modern SQL Connectivity
Use `Microsoft.Data.SqlClient` for all SQL Server operations. `System.Data.SqlClient` is obsolete and forbidden. Ensure all I/O operations are asynchronous and suffixed with `Async`.

### III. Unified Error & Progress Handling
Use `ErrorHandler` for consistent exception management and `ProgressHelper` for reporting status updates and percentages to the UI. This ensures a standard user experience and robust logging.

### IV. MVVM Methodology
Adhere strictly to the MVVM pattern. ViewModels must inherit from `ViewModelBase` and use `ServiceContainer` for logic. User interface logic must stay in ViewModels, avoiding code-behind where possible.

### V. Multi-Platform SQL Abstraction
Maintain support for Local, Docker, and Remote SQL Server connections using the `ConnectionType` abstraction. Operations must be agnostically handled by `BackupRestoreService`.

## Performance & Reliability Standards
Implement zero-tolerance error policies. All critical settings must be persisted in JSON format. Use `CrashProtectionManager` to detect and recover from unexpected shutdowns.

## Deployment & Maintenance Workflow
Automate IIS deployments via `DeployService` using the 7-step protocol. Database maintenance (integrity checks, index rebuilds) must be handled by `DailyTaskService`.

## Governance
This constitution is the source of truth for repository standards. Amendments require a version bump and an update to the Sync Impact Report. All changes must be verified against `CLAUDE.md` and `.github/copilot-instructions.md`.

**Version**: 1.0.0 | **Ratified**: 2026-02-26 | **Last Amended**: 2026-02-26
