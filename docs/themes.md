# LightSQL — Themes

LightSQL has five colour themes, chosen in **Tools → Options → Appearance → Theme**:

| Theme | Look |
|---|---|
| **Auto (follow Windows)** | Light or Dark, following *Settings → Personalization → Colors → Choose your app mode*. Switches live when Windows changes. Default. |
| **Light** | Modern light theme, blue accent and blue status bar. |
| **Dark** | Dark surfaces, light text, VS Code–style syntax colours. |
| **Classic (Windows Forms gray)** | `#F0F0F0` grey surfaces, white inputs, square corners, blue selection with white text, yellow tooltips, SSMS syntax colours. |
| **Gradient** | Dark indigo → violet → teal gradient window background, translucent panels and editor, violet/pink accents and a gradient status bar. |

Selecting a theme in the dialog **previews it immediately** on every open window;
**Save** keeps it, **Cancel** (or closing the dialog) restores the previous theme.
The choice is stored in `options.json` as `"Theme": "Dark"` (by name). Files
written before themes existed have no `Theme` value and get **Auto**.

The window title bars (drawn by Windows) are switched to dark for Dark and
Gradient (`DwmSetWindowAttribute`, Windows 10 1809+ / Windows 11).

**Limitation:** standard Windows message boxes (`MessageBox`) and the Open / Save
As file dialogs are drawn by Windows and are not themed.

## How it works

```
App.xaml  MergedDictionaries
 ├─ Themes/Palettes/Light.xaml   ← the active palette; ThemeService swaps this one
 ├─ Themes/Controls.xaml         ← implicit styles for every control (DynamicResource only)
 └─ Resources/ObjectExplorerIcons.xaml
```

- **Palettes** (`Themes/Palettes/Light|Dark|Classic|Gradient.xaml`) define only
  resources: brushes (`Theme.WindowBackground`, `Theme.Foreground`,
  `Theme.Accent`, `Theme.Editor.*`, `Theme.Syntax.*`, ...), `Theme.CornerRadius`
  and `Theme.Name` (the palette's name; marks the dictionary as the palette). All
  palettes define **exactly the same keys**.
- **Controls.xaml** holds implicit styles (no `x:Key`) and, where the default WPF
  (Aero2) template has hard-coded light colours, replacement templates: Button,
  TextBox, PasswordBox, CheckBox, ComboBox(+Item), ListBox(+Item),
  TreeView(+Item), TabControl/TabItem, Menu/MenuItem/ContextMenu, Separator,
  ToolBar (+ its Button/ComboBox/TextBox/Separator style keys), StatusBar,
  GroupBox, GridSplitter, Slider (horizontal), ToolTip, ScrollBar/ScrollViewer,
  DataGrid (+ column header, cell). It also has `Theme.WindowStyle`.
  Every colour there is a `{DynamicResource Theme.*}`.
- **`ThemeService`** (`IThemeService`, singleton) applies a theme: it resolves
  Auto to Light/Dark (`ResolveEffectiveTheme`), loads the palette
  (`GetPaletteUri`), replaces the palette dictionary in the application resources
  (WPF then re-resolves every DynamicResource, so open windows recolour at once)
  and updates the title bars of open windows. It listens to
  `ISystemThemeProvider.SystemThemeChanged` and re-applies when Auto is selected.
- **`WindowsSystemThemeProvider`** reads `HKCU\Software\Microsoft\Windows\
  CurrentVersion\Themes\Personalize\AppsUseLightTheme` and watches
  `SystemEvents.UserPreferenceChanged`.
- **`WindowTitleBar`** sets the dark/light title bar of one window. `App` registers
  a class handler for `Window.Loaded`, so every window opened later gets the
  right title bar too.
- **SQL editor** (`SqlEditorControl`): background, text and line-number colours
  are DynamicResources; the selection brush is a resource reference on the
  `TextArea`. Syntax colours are not dependency properties, so the control holds a
  private `ThemeName` property bound with `SetResourceReference` to `Theme.Name`;
  its change callback rebuilds the highlighting with
  `Editor/SqlHighlighting.Create(colors)`, which loads `SqlSyntax.xshd` and
  replaces each named colour (`Keyword`, `String`, ...) with the palette brush
  `Theme.Syntax.<name>`.
- Startup: `App.OnStartup` applies the saved theme before the main window is
  created.

## Rules for new UI (so everything stays themed)

1. **Never hard-code a colour** in a view (`Foreground="Gray"`, `#FF...`). Use
   `{DynamicResource Theme.<Key>}` (always *Dynamic*, never *Static*, or the
   colour will not change at runtime). `Transparent` is allowed.
2. **New windows** set `Style="{DynamicResource Theme.WindowStyle}"` on the root
   `<Window>` (implicit styles do not apply to classes derived from `Window`).
3. **Inline styles** for a control type that `Controls.xaml` themes must use
   `BasedOn="{StaticResource {x:Type TheControl}}"`; without it the style replaces
   the themed one and the control falls back to the default light look.
4. **A new control type** that the app starts using gets an implicit style in
   `Controls.xaml` (re-template it if its default template has fixed colours).
5. **A new colour** is a new key in **every** palette (Light, Dark, Classic,
   Gradient).
6. **A new syntax colour** in `SqlSyntax.xshd` needs a `Theme.Syntax.<name>`
   brush in every palette.
7. **A new theme**: add an `AppTheme` value (enums start at 1), a palette file
   `Themes/Palettes/<Name>.xaml` with all keys and `Theme.Name` = `<Name>`, a case
   in `ThemeService.ResolveEffectiveTheme`/`GetPaletteUri` (and `IsDarkTheme` if
   dark), and an entry in `ThemeOption.All`.

Rules 1, 2, 3, 5 and 6 are enforced by unit tests (`ThemeConventionTests`), and
`ThemedWindowsTests` shows the real windows under every theme, so a broken
template or a missing key fails the build's tests.

## Tests

- `ThemeServiceTests` — Auto resolution, palette URIs, palette swap keeps other
  dictionaries, Windows mode change re-applies Auto only, unknown value → Auto.
- `ThemeConventionTests` — identical keys in all palettes, `Theme.Name` matches,
  syntax brushes present, `Controls.xaml` loads, every referenced `Theme.*` key
  exists, no hard-coded colours in views, every window uses
  `Theme.WindowStyle`, inline styles of themed controls use `BasedOn`.
- `ThemedWindowsTests` — the main window and all dialogs shown off-screen under
  all four palettes (switching at runtime, with a menu and dropdown opened);
  checks window, tree, status bar and editor brushes and the editor's keyword
  colour follow the palette.
- `SqlHighlightingTests`, `OptionsViewModelTests` (preview / save / cancel),
  `OptionsServiceTests` (theme persisted by name, default Auto).
