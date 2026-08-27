# Cedro Modern Dock Rev v1.2.0

Rev v1.2.0 expands Cedro Modern Dock Rev with direct Windows script launchers and a categorized multi-library Windows system-icon browser.

## New in v1.2.0

### Script launchers

- Pin `.bat`, `.cmd` and `.vbs` launchers directly from **Settings → Icons → Add Script**.
- Script entries behave like normal Dock program items for ordering, removal, icon customization and configuration export/import.
- Launching follows normal Windows Shell association behavior.
- Cedro sets the script's containing folder as its working directory, helping wrappers that call sibling `.ps1` files or other relative resources work reliably.
- BAT/CMD/VBS entries use their Windows-associated file-type icon by default.

### Windows system icon center

The system-icon picker now scans multiple Windows icon-bearing DLL/EXE resources instead of only `SHELL32.dll`.

Built-in catalog:

- Common — `SHELL32.dll`, `imageres.dll`
- Devices — `DDORes.dll`, `setupapi.dll`, `compstui.dll`
- Network — `netshell.dll`, `netcenter.dll`, `networkexplorer.dll`
- Classic — `moricons.dll`, `pifmgr.dll`
- Other — `explorer.exe`, `mmres.dll`, `wmploc.dll`

Cedro automatically hides resource libraries that are not present on the current Windows build. Switch libraries at the top of the picker and choose directly from the asynchronously loaded thumbnail grid.

### Compact system-icon persistence

Windows system-library selections are now stored as:

```json
{
  "customSystemIconSource": "%SystemRoot%\\System32\\imageres.dll",
  "customSystemIconIndex": 15
}
```

Cedro resolves `%SystemRoot%` and extracts the icon dynamically at runtime. System icon bytes are not copied into `config.json` or the persistent custom-icon cache.

Imported PNG/ICO/JPG/BMP/GIF/TIFF overrides continue to use the portable Base64 PNG representation from Rev v1.1.0. The two override modes are mutually exclusive, and **Restore default icon** clears either one.

Existing Rev v1.1.0 Base64 custom-icon configurations remain compatible.

> Note: Windows DLL icon indexes are implementation details and can theoretically differ across Windows releases. If exact visual identity across different PCs/Windows builds is more important than compact configuration, use an imported PNG/ICO override.

## Existing Rev features retained

- True transparent Dock composition with desktop icons visible behind it.
- Optional Always-on-top with automatic fullscreen/game suppression.
- Four-edge magnetic docking and auto-hide with a visible reveal tail.
- Bottom docking that respects the Windows taskbar work area.
- Adjustable Dock vertical padding.
- Independent running indicators, hover magnification, hover labels and live window previews.
- Unpinned running-app section with right-click and drag-to-pin.
- Packaged Windows App/UWP support through AppUserModelID and AppsFolder launch targets.
- Best-effort Edge/Chromium PWA identity support.
- Imported per-item image/icon overrides.
- Configuration export/import, automatic safety backup and automatic restart after import.

## Portable package

Download `CedroModernDock-Rev-v1.2.0-win-x64.zip`, extract it, and run `CedroModernDock.exe`.

The live configuration is stored in:

```text
%APPDATA%\CedroModernDock\config.json
```

Imported custom-image cache files may be materialized under:

```text
%APPDATA%\CedroModernDock\customIcons
```

Windows system-library icon overrides do not require a persistent copied icon file.

## Stable baseline

This release is built from the frozen `stable/rev-v1.2.0` branch. Rev v1.1.0 and Rev v1.0.0 remain available as previous known-good rollback points.

## Credits

Cedro Modern Dock Rev is a GPL-3.0 community enhancement fork of the upstream Cedro Modern Dock project:

https://github.com/Cedro-Software/cedro-modern-dock
