# Research: UI Modernization Aesthetics & Tech

**Feature**: [spec.md](file:///c:/Users/moham/source/repos/BackupDbServer/BackupDbServer/specs/001-modernize-ui/spec.md)
**Date**: 2026-02-26

## Research Tasks

### 1. Modern WPF UI Framework Evaluation
- **Decision**: Stay with **Vanilla XAML + Custom Styles** (supplemented by `FluentWPF` or similar lightweight helper if needed).
- **Rationale**: Minimalist, luxury aesthetics often require high precision that heavy UI frameworks (MahApps, MaterialDesignInXaml) might make difficult to customize without significant "fighting" against their defaults. Custom styles allow for better RTL (Arabic) control and adherence to the `dotnet-wpf-modern` skill guidelines.
- **Alternatives considered**: 
  - *MahApps.Metro*: Very robust but often results in a "generic" look and can be heavy.
  - *MaterialDesignInXamlToolkit*: Excellent but follows Material Design strictly, which might conflict with a "Luxury/Refined" custom brand direction.

### 2. Card Layout Implementation
- **Decision**: Implementation of a custom `Card` style for `Border` controls.
- **Rationale**: A `Border` with `CornerRadius="12"`, a subtle `DropShadowEffect` (BlurRadius=15, Opacity=0.2), and a slightly lightened background (e.g., #2D2D3F on a #1E1E2E background) provides the best depth.
- **WPF Implementation**:
  ```xml
  <Style x:Key="ModernCard" TargetType="Border">
      <Setter Property="Background" Value="#2D2D3F"/>
      <Setter Property="CornerRadius" Value="12"/>
      <Setter Property="Margin" Value="10"/>
      <Setter Property="Effect">
          <Setter.Value>
              <DropShadowEffect BlurRadius="15" ShadowDepth="2" Opacity="0.3"/>
          </Setter.Value>
      </Setter>
  </Style>
  ```

### 3. Vector Icon Library
- **Decision**: **Segoe MDL2 Assets** and **Lucide/FontAwesome Vector Data**.
- **Rationale**: Using `PathGeometry` (Vector Data) directly is the most performant and "premium" way to handle icons in WPF. Segoe MDL2 is built-in to Windows 10/11, ensuring zero dependency overhead for many system-like icons.
- **Reference**: Lucide icons can be converted to XAML Path data easily.

### 4. Micro-interactions
- **Decision**: Use `VisualStateManager` and `Storyboards` in ControlTemplates.
- **Rationale**: WPF handles animations natively very well. Hover states will use `DoubleAnimation` for `Opacity` or `ColorAnimation` for backgrounds with a 250ms duration ("EaseInOut").

## Resolved Clarifications
- **Clarification**: Framework choice?
- **Decision**: Vanilla XAML + Custom Styles for maximum control over RTL and premium aesthetic.
