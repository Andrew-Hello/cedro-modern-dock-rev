# Cedro Modern Dock Rev

> A Windows Dock / Quick Launch enhancement fork based on [Cedro Modern Dock](https://github.com/Cedro-Software/cedro-modern-dock).
>
> **Rev v1.1.0** extends the first stable enhanced baseline with portable per-item custom icon overrides while keeping the original GPL-3.0 license and upstream attribution.

![.NET](https://img.shields.io/badge/.NET-9-5122d3?style=for-the-badge&logo=dotnet&logoColor=white)
![Avalonia](https://img.shields.io/badge/Avalonia-11.3-0080ff?style=for-the-badge&logo=avalonia&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![License](https://img.shields.io/github/license/Andrew-Hello/cedro-modern-dock-rev?style=for-the-badge)

Cedro Modern Dock Rev keeps the lightweight launcher workflow of the upstream project, while adding stronger Windows desktop integration, taskbar-like running-app behavior, edge docking, application identity support, portable configuration backup/restore, and user-controlled icon overrides.

## Rev v1.1.0 highlights

### Custom icon overrides

- Assign a custom icon to any pinned Dock item from **Settings → Icons**.
- Supports PNG, ICO, JPG/JPEG, BMP, GIF and TIFF source files.
- Custom icons take priority over automatically extracted EXE, Windows App/UWP, PWA, folder and built-in module icons.
- Use **Restore default icon** at any time to return to Cedro's automatic icon resolution.
- Custom images are normalized to PNG and cached under `%APPDATA%\CedroModernDock\customIcons` for fast loading.
- The normalized icon data is also embedded in `config.json`, so configuration export/import carries custom icons to another PC without separately copying image files.
- Works with normal programs, packaged Windows apps, Edge/Chromium PWAs, folders, Windows modules and the Cedro Settings item.

### Window and desktop behavior

- True transparent top-level Dock composition: desktop icons and labels remain visible through the Dock background.
- Optional **Always on top** behavior.
- Automatically suspends topmost while another application or game is truly fullscreen, then restores it afterwards.
- Dynamic or static Dock positioning.
- Four-edge docking and auto-hide:
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
- Existing transparency, color, rounding, spacing, icon size, tint, horizontal/vertical layout and other upstream customization remain available.

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
2. Download `CedroModernDock-Rev-v1.1.0-win-x64.zip` (or a newer Rev release).
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

For migration to another PC, exporting/importing the JSON is recommended. Custom icon overrides are embedded in the exported configuration and therefore migrate with it. Program shortcuts still depend on their target application being installed at a compatible path. Packaged Windows apps and supported PWA entries use their Windows application identity when available.

## Pinning Windows Apps and PWAs

For apps that do not have a normal `.exe` path (for example many packaged Windows apps):

1. Launch the app normally from Windows.
2. Wait for it to appear in the Dock's unpinned running-app section.
3. Right-click it and choose **Pin to Dock**, or drag it into the pinned section.
4. Close the app and test the newly pinned entry.

Cedro stores the Windows application identity and launches packaged apps through the Windows AppsFolder shell target where applicable.

If the automatically resolved icon is not ideal — a common case for browser-installed PWAs — select the pinned item in **Settings → Icons** and apply a custom icon override.

## Supported languages

The upstream localization set is retained, including English, Portuguese (Brazil), Spanish, French, German, Japanese, Simplified Chinese, Traditional Chinese, Hindi, Arabic, Bengali, Russian, Urdu, Indonesian, Nigerian Pidgin, Marathi, Telugu, Turkish, Tamil, Cantonese and Vietnamese.

New Rev-specific strings currently have complete English and Simplified Chinese translations; other locales can fall back to English until translated.

## Project architecture

The current project is **C# / .NET 9 / Avalonia 11.3** and follows a layered architecture:

- `CedroModernDock.Core` — domain models, application services, configuration contracts and i18n.
- `CedroModernDock.Infrastructure.Windows` — Win32/DWM/Shell integration, icon extraction, custom-icon caching, application identity, JSON persistence, registry auto-start and tray behavior.
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
- `stable/rev-v1.1.0` — frozen Rev v1.1.0 baseline.
- `stable/rev-v1.0.0` — frozen first stable Rev baseline.
- Latest release tag: `rev-v1.1.0`.

Frozen baselines remain separate so future experimental changes can continue without losing known-good rollback points.

## Upstream and license

This repository is a community enhancement fork of **Cedro Modern Dock**. The original project and its authors remain the source of the core application and architecture.

Upstream project:

- https://github.com/Cedro-Software/cedro-modern-dock

This fork remains licensed under **GPL-3.0**, consistent with the upstream repository. See `LICENSE` for details.

## Rev changelog

See [CHANGELOG-REV.md](CHANGELOG-REV.md) for the enhanced fork's release history.
