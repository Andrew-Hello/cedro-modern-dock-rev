# Cedro Modern Dock Rev v1.0.0

This is the first frozen stable release of the enhanced `cedro-modern-dock-rev` fork.

## What makes Rev different

- Transparent top-level Dock composition that correctly shows desktop icons behind the Dock.
- Optional Always-on-top with automatic fullscreen/game suppression.
- Four-edge magnetic docking and auto-hide.
- Bottom docking respects the Windows taskbar work area.
- Visible hoverable/clickable edge tail while hidden.
- Adjustable vertical Dock padding.
- Independent toggles for running indicator dots, hover magnification, hover labels and live window previews.
- Unpinned running-app section with right-click pinning and drag-to-pin.
- Packaged Windows App/UWP support through AppUserModelID and AppsFolder launch targets.
- Best-effort Edge/Chromium PWA identity support.
- Configuration export/import, automatic safety backup and automatic restart after import.

## Portable package

Download `CedroModernDock-Rev-v1.0.0-win-x64.zip`, extract it, and run `CedroModernDock.exe`.

The live configuration is stored in:

```text
%APPDATA%\CedroModernDock\config.json
```

You can also export/import configuration from the Settings window.

## Stable baseline

This release is created from the frozen `stable/rev-v1.0.0` branch. Future experimental changes continue on `cedro-enhanced-window`, so this release remains a known-good rollback point.

## Credits

Cedro Modern Dock Rev is a GPL-3.0 community enhancement fork of the upstream Cedro Modern Dock project:

https://github.com/Cedro-Software/cedro-modern-dock
