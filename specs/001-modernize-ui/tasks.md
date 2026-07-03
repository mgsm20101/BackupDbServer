# Tasks: UI Modernization

**Input**: Design documents from `/specs/001-modernize-ui/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Manual visual verification is requested in `spec.md`. No automated UI tests (TDD) are requested.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Project Root**: `Models/`, `ViewModels/`, `Views/`, `Services/`, `Helpers/`
- **Resources**: `Resources/`
- Paths assume the single project structure confirmed in `plan.md`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create `Resources/ModernDarkTheme.xaml` resource dictionary
- [x] T002 Update `App.xaml` to merge `ModernDarkTheme.xaml` in `Application.Resources`
- [x] T003 Define global `SolidColorBrush` resources in `Resources/ModernDarkTheme.xaml` (MainBackground, Surface, Primary, etc.)
- [x] T004 Define typography resources (FontFamily, FontSize) in `Resources/ModernDarkTheme.xaml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T005 [P] Create `Resources/IconDictionary.xaml` for vector-based icon data
- [x] T006 [P] Implement `ModernCard` style in `Resources/ModernDarkTheme.xaml` (Border-based with DropShadow)
- [x] T007 Implement `ActionButtonStyle` in `Resources/ModernDarkTheme.xaml` (Primary theme)
- [x] T008 Implement `DataGridModern` style in `Resources/ModernDarkTheme.xaml`
- [x] T009 [P] Implement `ModernHeaderStyle` for `TextBlock` in `Resources/ModernDarkTheme.xaml`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Premium Aesthetic Refresh (Priority: P1) 🎯 MVP

**Goal**: Implement the core Modern Dark theme across the main window and typography.

**Independent Test**: Launch application and verify the background is `#1E1E2E`, accents are Emerald Green, and typography is consistent.

### Implementation for User Story 1

- [x] T010 [US1] Apply `MainBackgroundBrush` to `MainWindow.xaml` root Grid
- [x] T011 [US1] Update `MainWindow.xaml` title border to use `PrimaryAccentBrush` and modern rounded corners
- [x] T012 [P] [US1] Replace text-based emojis with vector icons from `IconDictionary.xaml` in `MainWindow.xaml` (Refresh, Save, Backup)
- [x] T013 [US1] Apply `ModernHeaderStyle` to all header TextBlocks in `MainWindow.xaml`
- [x] T014 [US1] Ensure `FlowDirection="RightToLeft"` remains visually balanced with new spacing in `MainWindow.xaml`

**Checkpoint**: User Story 1 functional - basic beautification complete.

---

## Phase 4: User Story 2 - Card-Based Layout & Depth (Priority: P2)

**Goal**: Group related information into elevated cards for better scanning.

**Independent Test**: Verify "Databases" and "IIS Management" segments are contained in elevated cards with shadows.

### Implementation for User Story 2

- [x] T015 [US2] Wrap Database list in `MainWindow.xaml` with `ModernCard` style Border
- [x] T016 [US2] Wrap IIS Management sections in `MainWindow.xaml` with `ModernCard` style Borders
- [x] T017 [US2] Apply `ModernCard` style to log statistics area in `MainWindow.xaml`
- [x] T018 [US2] Adjust TabControl items to feel "elevated" when selected using the new color palette

**Checkpoint**: User Story 2 functional - layout depth improved.

---

## Phase 5: User Story 3 - Reactive Micro-interactions (Priority: P3)

**Goal**: Add animations and hover states to make the UI feel alive.

**Independent Test**: Hover over buttons and verify smooth color transitions. Check progress bars for shimmer effects.

### Implementation for User Story 3

- [x] T019 [US3] Add `VisualStateManager` to `ActionButtonStyle` in `Resources/ModernDarkTheme.xaml` for hover/press animations
- [x] T020 [US3] Implement "Shimmer" animation for `ProgressBar` in `Resources/ModernDarkTheme.xaml`
- [x] T021 [US3] Apply shimmer effect to progress bars in `Views/BackupProgressWindow.xaml` and `MainWindow.xaml`
- [ ] T022 [P] [US3] Add 200ms transitions to TabItem selection changes in `MainWindow.xaml`

**Checkpoint**: All user stories functional - UI is fully modernized.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Consistency check across all sub-windows and final refinements.

- [ ] T023 Apply thematic styles to `Views/SettingsWindow.xaml`
- [ ] T024 Apply thematic styles to `Views/AutoBackupWindow.xaml`
- [ ] T025 Apply thematic styles to `Views/DatabaseCopyWindow.xaml`
- [ ] T026 Final review of RTL alignment and Arabic typography consistency
- [ ] T027 Run visual validation against `Success Criteria` in `spec.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Core dictionaries must exist first.
- **Foundational (Phase 2)**: Depends on Phase 1. Blocks all UI application.
- **User Stories (Phase 3+)**: Can proceed sequentially (US1 -> US2 -> US3). US2/US3 depend on styles created in Foundation.

### Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2.
2. Apply theme to `MainWindow` (US1).
3. **VALIDATE**: Ensure basic dark theme is professional.

---

## Notes

- [P] tasks can be worked on concurrently if multiple UI designers were available.
- Focus strictly on `Resources/ModernDarkTheme.xaml` to avoid cluttering View code-behind.
