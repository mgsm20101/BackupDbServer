# User Guide: Modern UI Implementation

This guide explains how to apply the "Modern Dark" theme consistently across the `BackupDbServer` codebase.

## Applying the Theme

1. **Resource Inclusion**:
   Ensure `ModernDarkTheme.xaml` is merged in `App.xaml`:
   ```xml
   <Application.Resources>
       <ResourceDictionary>
           <ResourceDictionary.MergedDictionaries>
               <ResourceDictionary Source="/Resources/ModernDarkTheme.xaml"/>
           </ResourceDictionary.MergedDictionaries>
       </ResourceDictionary>
   </Application.Resources>
   ```

2. **Using Cards**:
   Instead of flat Borders or GroupBoxes, use the `ModernCardStyle`:
   ```xml
   <Border Style="{StaticResource ModernCardStyle}">
       <!-- Content here -->
   </Border>
   ```

3. **Dynamic Brushes**:
   Always use `DynamicResource` for colors to allow for future theme switching:
   ```xml
   <Button Background="{DynamicResource PrimaryAccentBrush}" ... />
   ```

4. **Vector Icons**:
   Use the `PathIcon` component (if implemented) or raw `Path` data relative to the icon dictionary.
