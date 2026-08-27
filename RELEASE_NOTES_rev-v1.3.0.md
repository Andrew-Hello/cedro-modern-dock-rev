# Cedro Modern Dock Rev v1.3.0

Rev v1.3.0 introduces a dedicated **Bottom AppBar** positioning mode designed to let normal maximized Windows applications avoid the Dock while keeping Windows Shell work-area handling conservative and predictable.

## New in v1.3.0

### Bottom AppBar as a dedicated positioning mode

**Settings → Dock Positioning** now provides three mutually exclusive modes:

- Static positioning
- Dynamic positioning
- Bottom AppBar

Bottom AppBar is deliberately constrained:

- Bottom edge only.
- Horizontal Dock only.
- Left / Center / Right alignment only.
- Always positioned directly above the Windows taskbar/work-area boundary.
- No free dragging.
- No arbitrary screen-spacing controls.
- Four-edge auto-hide does not run concurrently with Bottom AppBar.

Static and Dynamic positioning never register a Windows AppBar.

### Maximized-window avoidance

When Bottom AppBar is active, Cedro registers one bottom AppBar reservation with Windows Shell. Normal maximized applications then use the Dock's upper edge as their bottom work-area boundary instead of extending underneath an Always-on-top Dock.

The Windows taskbar remains the fixed lower boundary. Cedro negotiates only the strip immediately above the existing taskbar/work area and does not claim or cover the taskbar edge.

### Stability-focused AppBar lifecycle

This release uses a deliberately conservative Shell integration model:

- `ABM_NEW` once per Bottom AppBar activation.
- `ABM_QUERYPOS` only during initial registration.
- One fixed negotiated bottom boundary for the lifetime of that reservation.
- Height changes update the same reservation through `ABM_SETPOS`.
- Cedro never feeds its own already-reduced Windows work area back into AppBar placement calculations.
- No periodic AppBar polling.
- No `ABN_POSCHANGED` feedback loop.
- Opening/closing Settings does not register, remove or renegotiate AppBar geometry.
- Leaving Bottom AppBar or exiting Cedro removes the reservation once.

This architecture replaces an earlier experimental general-purpose AppBar approach that could recursively change work-area geometry when mixed with free/static edge positioning.

### Dock-height compatibility

Bottom AppBar tracks Cedro's actual SizeToContent height. A short debounce collapses multiple layout changes into one update of the existing reservation.

This keeps the reserved area synchronized when changing:

- Dock vertical padding
- Icon size
- Running-indicator visibility/state when it changes real Dock height
- Other real layout changes that alter the native Dock height

If the final height is unchanged, Cedro does not touch Shell geometry.

Left / Center / Right alignment changes move the Dock horizontally without changing work-area height.

### Isolation and legacy-config safety

- Legacy `reserveDesktopSpace` JSON values no longer activate AppBar behavior.
- Only `positioningMode = BOTTOM_APPBAR` can register the AppBar.
- Existing enabled edge-auto-hide fields are ignored while Bottom AppBar is active.
- Entering Bottom AppBar clears the dynamic edge-docking state and disables the four-edge auto-hide settings so two edge-management systems cannot compete.
- Imported configurations that request vertical Dock layout are forced horizontal while Bottom AppBar is active.
- Switching back to Static/Dynamic releases the AppBar before Cedro calculates the normal Dock position.

## Existing Rev features retained

- BAT/CMD/VBS script launchers with script-directory working directory.
- Multi-library Windows system-icon browser (`SHELL32.dll`, `imageres.dll`, device/network/classic resources and more).
- Lightweight `%SystemRoot%` resource path + icon-index persistence for Windows system icons.
- PNG/ICO/JPG/BMP/GIF/TIFF per-item custom icon overrides.
- True transparent Dock composition.
- Optional Always-on-top with fullscreen/game suppression.
- Static/Dynamic four-edge magnetic docking and auto-hide outside Bottom AppBar mode.
- Adjustable Dock vertical padding.
- Independent running indicators, hover magnification, hover labels and live window previews.
- Unpinned running-app section with right-click and drag-to-pin.
- Packaged Windows App/UWP support through AppUserModelID and AppsFolder launch targets.
- Best-effort Edge/Chromium PWA identity support.
- Configuration export/import, automatic backup and restart after import.

## Portable package

Download `CedroModernDock-Rev-v1.3.0-win-x64.zip`, extract it, and run `CedroModernDock.exe`.

The live configuration is stored in:

```text
%APPDATA%\CedroModernDock\config.json
```

## Stable baseline

This release is built from the frozen `stable/rev-v1.3.0` branch. Rev v1.2.0, Rev v1.1.0 and Rev v1.0.0 remain available as previous known-good rollback points.

## Credits

Cedro Modern Dock Rev is a GPL-3.0 community enhancement fork of the upstream Cedro Modern Dock project:

https://github.com/Cedro-Software/cedro-modern-dock
