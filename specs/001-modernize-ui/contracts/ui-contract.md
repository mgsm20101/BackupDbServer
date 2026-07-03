# UI Contract: Modernize UI/UX

**Feature**: [spec.md](file:///c:/Users/moham/source/repos/BackupDbServer/BackupDbServer/specs/001-modernize-ui/spec.md)

## Interface Commitment

The UI modernization guarantees a consistent look and feel across the entire application by binding all views to a centralized set of `DynamicResource` keys.

### Global Resource Keys

Developers and UI designers MUST use these keys for the "Modern Dark" theme:

| Resource Key | Type | Description |
|--------------|------|-------------|
| `MainBackgroundBrush` | SolidColorBrush | `#1E1E2E` - Root grid/window background. |
| `CardBackgroundBrush` | SolidColorBrush | `#2D2D3F` - Container for grouped items. |
| `PrimaryAccentBrush` | SolidColorBrush | `#10B981` - Actionable emerald highlights. |
| `ModernHeaderStyle` | Style (TextBlock) | Bold, 20pt text with emerald underline. |
| `ModernCardStyle` | Style (Border) | The standard container with shadow and corners. |

## Visual Hierarchy

1. **Top Level**: Darkest layer (#1E1E2E).
2. **Component Level**: Elevated cards (#2D2D3F) with soft shadows.
3. **Interactive Level**: Emerald (#10B981) or Azure (#3B82F6) highlights for buttons.
4. **Content Level**: High contrast white text on dark backgrounds.

## Vector Icon Map

| Emoji/Legacy | Vector ID | Context |
|--------------|-----------|---------|
| 🔄 | `Symbol.Refresh` / `Lucide.RefreshCw` | Updates, Refreshes |
| 💾 | `Symbol.Save` / `Lucide.Save` | Saving settings, selections |
| 💽 | `Symbol.HardDrive` / `Lucide.Database` | Backup execution |
| 📋 | `Symbol.Copy` / `Lucide.Clipboard` | Logs, Database copies |
| ⚙ | `Symbol.Setting` / `Lucide.Settings` | Settings menu |
