# Cedro Modern Dock Rev v1.1.0

Rev v1.1.0 adds portable per-item custom icon overrides on top of the Rev v1.0.0 stable baseline.

Release channel: **stable**.

## New in v1.1.0

- Add your own icon to any pinned Dock item from **Settings → Icons**.
- Supported source formats: PNG, ICO, JPG/JPEG, BMP, GIF and TIFF.
- Custom icons override automatic EXE, Windows App/UWP, PWA, folder and Windows-module icon resolution.
- **Restore default icon** returns an item to Cedro's automatic icon pipeline at any time.
- Custom icons work with normal programs, packaged Windows apps, Edge/Chromium PWAs, folders, Windows modules and the Cedro Settings item.
- Selected images are normalized to PNG and cached under `%APPDATA%\CedroModernDock\customIcons`.
- The normalized PNG is also embedded in `config.json`, so **Export config / Import config carries custom icons to another PC automatically**.
- Settings-list previews and the live Dock refresh immediately after changing an icon.
- English and Simplified Chinese UI strings are included.

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
- Configuration export/import, automatic safety backup and automatic restart after import.

## Portable package

Download `CedroModernDock-Rev-v1.1.0-win-x64.zip`, extract it, and run `CedroModernDock.exe`.

The live configuration is stored in:

```text
%APPDATA%\CedroModernDock\config.json
```

Custom icon cache files are materialized under:

```text
%APPDATA%\CedroModernDock\customIcons
```

You normally do not need to copy that folder manually because custom icon data travels inside exported configuration.

## Stable baseline

This release is built from the frozen `stable/rev-v1.1.0` branch. Rev v1.0.0 remains available as the previous known-good rollback point.

## Credits

Cedro Modern Dock Rev is a GPL-3.0 community enhancement fork of the upstream Cedro Modern Dock project:

https://github.com/Cedro-Software/cedro-modern-dock
