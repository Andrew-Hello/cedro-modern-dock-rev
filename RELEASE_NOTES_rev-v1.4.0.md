# Cedro Modern Dock Rev v1.4.0

Rev v1.4.0 focuses on display-environment resilience and a complete Settings experience redesign while preserving the conservative Bottom AppBar architecture introduced in Rev v1.3.0.

## Highlights

### Bottom AppBar display/DPI rebinding

- Cedro now listens for native `WM_DISPLAYCHANGE` and `WM_DPICHANGED` notifications while Bottom AppBar mode is active.
- Moving between displays with different resolutions or DPI scaling no longer leaves the Dock using stale taskbar/work-area geometry.
- Display changes are debounced before Shell work-area operations so Windows, Explorer and Avalonia can finish their own monitor/DPI relayout first.
- Cedro safely removes the old Bottom AppBar reservation, re-detects the active monitor and taskbar boundary, then creates one fresh reservation for the new display environment.
- `WM_SETTINGCHANGE` / `SPI_SETWORKAREA` are deliberately not used as rebinding triggers, avoiding a feedback path where Cedro could respond to its own AppBar work-area updates.

### Stable Dock height with running indicators

- Running-indicator dots no longer participate in Dock height measurement.
- Each icon reserves a fixed indicator slot whether an application is running or not.
- When a dot appears, the icon shifts slightly upward inside the already-reserved area instead of increasing the window height.
- Starting or closing applications therefore no longer changes the Dock/AppBar reservation height.
- The global running-indicator visibility switch likewise no longer causes the Bottom AppBar or maximized-window work area to jump.

### Settings window Z-order fix

- Settings is no longer opened as an owned child of the Always-on-top Dock window.
- This prevents Windows owned-window Z-order rules from promoting Settings into the Dock's topmost band.
- Dialogs opened from Settings, including the Windows system-icon library, color pickers and module windows, can now appear naturally above Settings instead of being covered by it.
- Settings remains single-instance and activates the existing window when requested again.

### Complete Settings UI redesign

- Replaced the increasingly stretched top-tab layout with a desktop-style left navigation rail and bounded right-side content surface.
- Default Settings size is now 1000×700, resizable within a practical minimum/maximum range instead of expanding into an oversized 4K canvas.
- Each settings page owns its own scroll region; the page content no longer shares one giant outer ScrollViewer.
- Content surfaces have a sensible maximum width so extra monitor width becomes breathing room rather than forcing controls farther apart.
- Rebuilt the Dock Items page around a focused item list plus compact action/custom-icon cards.
- Reorganized Dock appearance, icon appearance and General settings into compact card/grid layouts.
- Removed the previous runtime "load old tabs, inject controls, then reflow everything" approach; the XAML now defines the final information architecture directly.
- Existing script launcher, Windows system-icon library, configuration backup/import/export and Bottom AppBar controls remain available within the redesigned Settings surface.

## Compatibility

- Existing Rev v1.3.0, v1.2.0, v1.1.0, v1.0.0 and compatible upstream configurations remain loadable.
- Bottom AppBar remains a dedicated third positioning mode; Static and Dynamic positioning still never register an AppBar.
- Existing Windows system-icon `Source + Index` references and imported Base64 image overrides remain unchanged.
- Rev v1.3.0 remains frozen as the previous known-good rollback baseline.

## Build

This release is built on Windows with .NET 9 and Avalonia 11.3. The attached package is the portable Windows x64 build:

`CedroModernDock-Rev-v1.4.0-win-x64.zip`
