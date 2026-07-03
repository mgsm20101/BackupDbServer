# Implementation Plan: Modernize UI/UX

**Branch**: `001-modernize-ui` | **Date**: 2026-02-26 | **Spec**: [spec.md](file:///c:/Users/moham/source/repos/BackupDbServer/BackupDbServer/specs/001-modernize-ui/spec.md)
**Input**: Feature specification from `/specs/001-modernize-ui/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

This feature modernizes the user interface of `BackupDbServer` with a "Modern Dark" aesthetic. It introduces a refined color palette, card-based layout for better depth and organization, and reactive micro-interactions (hover effects, animations) to provide a premium, professional experience. The technical approach involves creating global XAML resources, restyling standard controls using Vanilla XAML + Custom Styles, and replacing emoji-based indicators with vector icons (Segoe MDL2/Lucide).

## Technical Context

**Language/Version**: .NET 8.0 (C# 12)
**Primary Dependencies**: Microsoft.Data.SqlClient, Docker.DotNet, Google.Apis.Drive.v3 (existing).
**Storage**: N/A
**Testing**: Manual Visual Verification
**Target Platform**: Windows 10+ (WPF)
**Project Type**: Desktop Application (WPF)
**Performance Goals**: < 100ms UI response time; smooth 60fps animations.
**Constraints**: Must maintain Right-to-Left (RTL) support for Arabic.
**Scale/Scope**: Impacts all Views (MainWindow and sub-windows) and their associated ViewModels (where specific UI state is managed).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Rationale |
|-----------|--------|-----------|
| I. ServiceContainer-First | ✅ Pass | UI will continue to access services via `ServiceContainer`. |
| III. Unified Error & Progress Handling | ✅ Pass | New UI will incorporate `ProgressHelper` displays in modern progress bars. |
| IV. MVVM Methodology | ✅ Pass | All styling will be handled in XAML/ResourceDictionaries; logic remains in ViewModels. |
| V. Multi-Platform SQL Abstraction | ➖ N/A | Feature is UI-specific. |

## Project Structure

### Documentation (this feature)

```text
specs/001-modernize-ui/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
# Single project structure
BackupDbServer/
├── Models/
├── ViewModels/
├── Views/              # Primary location for XAML updates
├── Services/
├── Helpers/
├── Converters/
└── Resources/          # Storage for global Styles and Icon dictionaries
```

**Structure Decision**: Option 1 (Single Project). UI Modernization will primarily affect `Views/`, `Resources/`, and `App.xaml`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
