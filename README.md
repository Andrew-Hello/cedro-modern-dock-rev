# Cedro Modern Dock Rev

> A Windows Dock / Quick Launch enhancement fork based on [Cedro Modern Dock](https://github.com/Cedro-Software/cedro-modern-dock).
>
> **Rev v1.4.0** improves Bottom AppBar behavior across display/DPI changes, stabilizes Dock height around running indicators, fixes Settings-window Z-order, and introduces a fully redesigned Settings experience, while retaining the original GPL-3.0 license and upstream attribution.

![.NET](https://img.shields.io/badge/.NET-9-5122d3?style=for-the-badge&logo=dotnet&logoColor=white)
![Avalonia](https://img.shields.io/badge/Avalonia-11.3-0080ff?style=for-the-badge&logo=avalonia&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![License](https://img.shields.io/github/license/Andrew-Hello/cedro-modern-dock-rev?style=for-the-badge)

Cedro Modern Dock Rev keeps the lightweight launcher workflow of the upstream project, while adding stronger Windows desktop integration, taskbar-like running-app behavior, edge docking, application identity support, script launchers, portable configuration backup/restore and flexible per-item icon overrides.

## Rev v1.4.0 highlights

### Display/DPI-aware Bottom AppBar rebinding

Rev v1.4.0 makes the dedicated Bottom AppBar mode more resilient when moving between displays with different resolutions or DPI scaling.

- Cedro listens for native `WM_DISPLAYCHANGE` and `WM_DPICHANGED` events while Bottom AppBar is active.
- Resolution, monitor-topology and DPI changes are debounced before any Shell work-area operation.
- The old reservation is removed before Cedro re-detects the active monitor and taskbar boundary.
- Cedro then establishes one fresh Bottom AppBar reservation for the new display environment.
- Height updates are suspended during rebinding so Avalonia layout events from the new DPI cannot accidentally update an old-monitor reservation.
- `WM_SETTINGCHANGE` / `SPI_SETWORKAREA` are intentionally not used as triggers, avoiding a feedback path where Cedro could respond to its own work-area changes.

This is especially useful when switching between a high-DPI laptop panel and a larger external monitor.

### Stable running-indicator geometry

Running indicator dots no longer change the Dock window height.

- Every icon reserves a fixed indicator slot whether a program is running or not.
- When a dot appears, the icon shifts slightly upward inside the already-reserved space.
- Starting or closing applications therefore no longer changes the Dock/AppBar height.
- Toggling the global running-indicator preference also leaves the Dock height unchanged.

This prevents maximized-window geometry and Bottom AppBar positioning from moving simply because running-state dots appear or disappear.

### Redesigned Settings experience

The Settings window has been rebuilt around a desktop-style information architecture instead of the increasingly stretched top-tab layout.

- Left-side navigation replaces the old horizontal tab strip.
- The right-side content surface uses bounded widths instead of expanding across the full width of a 2K/4K display.
- Default window size is 1000×700 with practical resize limits.
- Every settings page owns its own scroll region.
- Dock Items now uses a dedicated item list plus compact action and custom-icon cards.
- Dock appearance, icon appearance and General settings use compact card/grid layouts.
- The XAML now defines the final page structure directly instead of creating legacy tabs first and rearranging them after `Opened`.

### Settings Z-order correction

Settings is no longer an owned child of the Always-on-top Dock window.

This fixes a long-standing Windows owned-window Z-order issue where Settings could remain above dialogs it opened. The Windows system-icon picker, color pickers, module window and other Settings-owned dialogs can now appear naturally above the Settings window.

## Rev v1.3.0 highlights

### Dedicated Bottom AppBar positioning mode

Rev v1.3.0 introduces **Bottom AppBar** as a third positioning mode alongside Static and Dynamic positioning.

- **Static positioning** and **Dynamic positioning** never register a Windows AppBar.
- **Bottom AppBar** is an intentionally constrained Shell-integration mode designed only for the bottom of the screen.
- The Dock is always horizontal and sits directly above the Windows taskbar/work-area boundary.
- Only three horizontal alignments are available: **Left**, **Center** and **Right**.
- Free dragging and screen-spacing controls are disabled in this mode.
- Four-edge auto-hide is isolated from Bottom AppBar and does not run concurrently with it.
- Switching away from Bottom AppBar or exiting Cedro immediately releases the Windows work-area reservation.

This lets normal maximized applications stop at the Dock's upper edge instead of extending underneath an Always-on-top Dock.

### Conservative AppBar lifecycle

The Bottom AppBar implementation deliberately avoids continuously renegotiating Windows work-area geometry.

- `ABM_NEW` is sent once when the dedicated mode becomes active.
- The legal bottom boundary is negotiated once against the existing taskbar/other AppBars.
- Subsequent Dock-height changes update the same reservation instead of registering another AppBar.
- The already-reduced Windows work area is never fed back into AppBar placement calculations, preventing recursive inward drift or stacked/nested reservations.
- There is no periodic AppBar polling and no `ABN_POSCHANGED` feedback loop.
- Opening or closing the Settings window does not register, remove or re-query the AppBar.
- Legacy `reserveDesktopSpace` configuration data is ignored as an activation mechanism; only the explicit `BOTTOM_APPBAR` positioning mode can own an AppBar reservation.

### Taskbar-aware placement and dynamic height compatibility

- Bottom AppBar negotiates its initial boundary before Cedro modifies the Windows work area, preserving the taskbar as the hard lower boundary.
- The Dock remains directly above the taskbar rather than competing for or overlapping the taskbar edge.
- Cedro watches the actual SizeToContent Dock height and applies a short debounce before updating an existing AppBar reservation.
- Changes to icon size, Dock vertical padding and other real layout-height changes therefore remain compatible with the reserved desktop area.
- Running-indicator visibility/state changes are handled through the same single-reservation height-update path.
- Left / Center / Right alignment changes move only the Dock horizontally and do not change the reserved work-area height.

The Bottom AppBar architecture in Rev v1.3.0 was redesigned after testing of a more general AppBar experiment showed that mixing Shell reservations with free edge positioning could create recursive work-area changes. Rev v1.3.0 intentionally narrows the feature to a single, predictable bottom-edge contract.

## Rev v1.2.0 highlights

### BAT / CMD / VBS launchers

- **Settings → Icons → Add Script (.bat/.cmd/.vbs)** can pin Windows script launchers directly.
- `.bat`, `.cmd` and `.vbs` entries behave like ordinary pinned program items: they can be reordered, removed, exported/imported and assigned custom icons.
- Scripts launch through the normal Windows Shell association, matching Explorer double-click behavior.
- Cedro explicitly uses the script's own directory as its working directory, helping existing BAT/VBS wrappers that call sibling `.ps1` files or other relative resources continue to work.
- Script entries receive the Windows-associated file-type icon by default instead of appearing blank.

### Windows system icon center

The custom-icon picker can browse a curated set of icon-bearing Windows DLL/EXE resources instead of being limited to `SHELL32.dll`.

Built-in catalog:

- **Common** — `SHELL32.dll`, `imageres.dll`
- **Devices** — `DDORes.dll`, `setupapi.dll`, `compstui.dll`
- **Network** — `netshell.dll`, `netcenter.dll`, `networkexplorer.dll`
- **Classic** — `moricons.dll`, `pifmgr.dll`
- **Other** — `explorer.exe`, `mmres.dll`, `wmploc.dll`

The picker automatically skips resource files that are not present on the current Windows edition/build. Choose a library from the selector, browse its icons as a thumbnail grid, then click an icon to apply it to the selected Dock item.

System-icon overrides are stored compactly as a resource expression plus icon index, for example:

```json
{
  "customSystemIconSource": "%SystemRoot%\\System32\\imageres.dll",
  "customSystemIconIndex": 15
}
```

Cedro expands `%SystemRoot%` at runtime and extracts the icon dynamically. Microsoft system icon bytes are not copied into Cedro's configuration or persistent custom-icon cache.

Because icon indexes are a Windows resource implementation detail, the exact icon at a given index can theoretically differ between Windows releases. For cross-PC migration where exact visual identity matters more than configuration size, use a PNG/ICO custom override instead.

### Custom image/icon overrides

- Assign a custom icon to any pinned Dock item from **Settings → Icons**.
- Supports PNG, ICO, JPG/JPEG, BMP, GIF and TIFF source files.
- Imported image overrides are normalized to PNG and embedded in `config.json` as Base64, making them self-contained across configuration export/import.
- System-library icons and imported image icons are mutually exclusive override modes; selecting one automatically clears the other.
- **Restore default icon** clears either override mode and returns the item to Cedro's automatic icon-resolution pipeline.
- Overrides work with normal programs, scripts, packaged Windows apps, Edge/Chromium PWAs, folders, Windows modules and the Cedro Settings item.
- Existing Base64 custom-icon configurations from Rev v1.1.0 remain compatible.

### Window and desktop behavior

- True transparent top-level Dock composition: desktop icons and labels remain visible through the Dock background.
- Optional **Always on top** behavior.
- Automatically suspends topmost while another application or game is truly fullscreen, then restores it afterwards.
- Three positioning modes in Rev v1.3.0 and later: Static, Dynamic and Bottom AppBar.
- Four-edge docking and auto-hide remain available to the normal Static/Dynamic workflow:
  - Top / Bottom can be enabled independently from Left / Right.
  - Dynamic mode supports drag-to-edge magnetic docking.
  - Bottom docking respects the Windows taskbar work area instead of overlapping it.
  - A visible clickable/hoverable tail remains after the Dock hides.
  - The tail appears only after the hide animation finishes, avoiding a distracting moving trail.

### Appearance and interaction

- Adjustable Dock vertical padding for a thin-strip layout.
- Optional running indicator dots.
- Optional hover magnification.
- Optional hover application-name labels.
- Optional live window previews.
- Existing transparency, color, rounding, spacing, icon size, tint, horizontal/vertical layout and other upstream customization remain available where compatible with the selected positioning mode.

### Running applications and pinning

- Shows running applications that are not yet pinned.
- Right-click an unpinned running application and choose **Pin to Dock**.
- Drag an unpinned running application directly into the pinned area to pin it.
- Supports normal Win32 desktop executables.
- Supports packaged Windows applications through Windows Application User Model IDs (AUMID), including apps that do not expose a normal launchable `.exe` entry.
- Best-effort support for Edge/Chromium installed web applications (PWA) by preserving their Windows application identity instead of treating every window as the browser executable.
- Window previews, activation and running-state tracking also understand packaged-app window hosting.

## Installation

The recommended build is the portable Windows x64 package attached to the latest **Cedro Modern Dock Rev** GitHub Release:

1. Open this repository's **Releases** page.
2. Download `CedroModernDock-Rev-v1.4.0-win-x64.zip` (or a newer Rev release).
3. Extract it to a normal writable folder.
4. Run `CedroModernDock.exe`.

When testing a new build, exit any existing Cedro instance first because the application intentionally uses a single-instance guard.

## Configuration, backup and migration

The live configuration is stored at:

```text
%APPDATA%\CedroModernDock\config.json
```

The Settings window also provides **Configuration & backup** controls:

- **Open backup folder**
- **Export config...**
- **Import config...**

Before an import, Cedro automatically saves the current configuration under:

```text
%APPDATA%\CedroModernDock\Backups\
```

After a successful import, Cedro automatically restarts so the imported settings are applied immediately.

For migration to another PC, exporting/importing the JSON is recommended. Imported PNG/ICO custom overrides travel inside the configuration. Windows system-icon overrides travel as `%SystemRoot%`-based resource references plus icon indexes. Program/script shortcuts still depend on their target file being present at a compatible path. Packaged Windows apps and supported PWA entries use their Windows application identity when available.

## Pinning Windows Apps and PWAs

For apps that do not have a normal `.exe` path (for example many packaged Windows apps):

1. Launch the app normally from Windows.
2. Wait for it to appear in the Dock's unpinned running-app section.
3. Right-click it and choose **Pin to Dock**, or drag it into the pinned section.
4. Close the app and test the newly pinned entry.

Cedro stores the Windows application identity and launches packaged apps through the Windows AppsFolder shell target where applicable.

If the automatically resolved icon is not ideal — a common case for browser-installed PWAs — select the pinned item in **Settings → Icons** and choose either an imported custom image/icon or a Windows system-library icon.

## Supported languages

The upstream localization set is retained, including English, Portuguese (Brazil), Spanish, French, German, Japanese, Simplified Chinese, Traditional Chinese, Hindi, Arabic, Bengali, Russian, Urdu, Indonesian, Nigerian Pidgin, Marathi, Telugu, Turkish, Tamil, Cantonese and Vietnamese.

New Rev-specific strings currently have complete English and Simplified Chinese translations; other locales can fall back to English until translated.

## Project architecture

The current project is **C# / .NET 9 / Avalonia 11.3** and follows a layered architecture:

- `CedroModernDock.Core` — domain models, application services, configuration contracts and i18n.
- `CedroModernDock.Infrastructure.Windows` — Win32/DWM/Shell integration, system/custom icon extraction, application identity, JSON persistence, registry auto-start and tray behavior.
- `CedroModernDock` — Avalonia UI, Dock window, settings window, view models and visual behavior.
- `CedroModernDock.Tests` — xUnit tests.

`App.axaml.cs` composes the dependencies and injects them into the UI/application layer.

## Build locally

Prerequisites:

- Windows development environment
- .NET 9 SDK (or compatible .NET 10 SDK)
- Git
- Visual Studio 2022 / Rider / VS Code are optional

From the `dotnet` directory:

```powershell
dotnet restore
dotnet build src/CedroModernDock -c Release
```

The build output is under:

```text
dotnet/src/CedroModernDock/bin/Release/net9.0-windows/
```

You can also run the application directly during development:

```powershell
dotnet run --project src/CedroModernDock
```

## Stable baselines and development branch

- `main` — current stable Rev source shown on the repository homepage.
- `cedro-enhanced-window` — active Rev development branch.
- `stable/rev-v1.4.0` — frozen Rev v1.4.0 baseline.
- `stable/rev-v1.3.0` — frozen Rev v1.3.0 baseline.
- `stable/rev-v1.2.0` — frozen Rev v1.2.0 baseline.
- `stable/rev-v1.1.0` — frozen Rev v1.1.0 baseline.
- `stable/rev-v1.0.0` — frozen first stable Rev baseline.
- Latest release tag: `rev-v1.4.0`.

Frozen baselines remain separate so future experimental changes can continue without losing known-good rollback points.

## Upstream and license

This repository is a community enhancement fork of **Cedro Modern Dock**. The original project and its authors remain the source of the core application and architecture.

Upstream project:

- https://github.com/Cedro-Software/cedro-modern-dock

This fork remains licensed under **GPL-3.0**, consistent with the upstream repository. See `LICENSE` for details.

## Rev changelog

See [CHANGELOG-REV.md](CHANGELOG-REV.md) for the enhanced fork's release history.
