# UI Design System: Modern Dark

**Feature**: [spec.md](file:///c:/Users/moham/source/repos/BackupDbServer/BackupDbServer/specs/001-modernize-ui/spec.md)

## Color Palette

| Name | Hex | Usage |
|------|-----|-------|
| Background | `#1E1E2E` | Global window background |
| Surface | `#2D2D3F` | Cards, elevated containers |
| Primary | `#10B981` | Emerald Green - primary buttons, success icons |
| Secondary | `#3B82F6` | Azure Blue - informative buttons, tooltips |
| Accent | `#F59E0B` | Amber - warnings, active progress |
| Error | `#EF4444` | Red - failure states |
| Text-Main | `#F9FAFB` | High emphasis text |
| Text-Sub | `#9CA3AF` | Low emphasis / disabled text |

## Typography (Arabic/English)

- **Main Typeface**: `Segoe UI Variable` (System) or `Almarai` (Arabic Webfont fallback).
- **Sizes**:
  - `Header`: 20pt, Bold
  - `Body`: 11pt, Regular
  - `Small`: 9pt, Light/Regular
- **Line Height**: 1.5 (Standard for readability).

## Component Models

### 1. Modern Card
- **Base Control**: `Border`
- **Properties**:
  - `Background`: `{DynamicResource SurfaceBrush}`
  - `CornerRadius`: `12`
  - `Padding`: `16`
  - `Effect`: `DropShadowEffect` (ShadowDepth=2, BlurRadius=20, Opacity=0.25)

### 2. Action Button
- **Base Control**: `Button`
- **Visual States**:
  - `Normal`: `Background=Primary`, `Foreground=Background`
  - `Hover`: `Background=PrimaryLight`, `Cursor=Hand`, `Scale=1.02`
  - `Pressed`: `Scale=0.98`
- **Transition**: 200ms `QuinticEaseOut` for all visual state changes.

### 3. DataGrid Modern
- **Properties**:
  - `Background`: `Transparent`
  - `RowBackground`: `#242436`
  - `AlternatingRowBackground`: `#2D2D3F`
  - `BorderThickness`: `0`
  - `HeadersVisibility`: `Column`
  - `ColumnHeaderStyle`: Bold, `#3B82F6` background.
