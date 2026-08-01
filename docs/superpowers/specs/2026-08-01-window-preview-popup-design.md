# Window Preview Popup — Design

**Date:** 2026-08-01
**Status:** Approved (design); pending implementation plan

## Problem

Hovering a dock item with open windows shows only the white running dot. The
JavaFX version of the dock showed a popup listing the item's open windows on
hover (like the Windows taskbar). This behavior is missing in the Avalonia
port.

## Current State

The service layer was already ported and is unused by any UI:

- `WindowPreviewService` (`CedroModernDock.Core.Application`) — `LoadPreview(item)`
  returns `List<WindowInfo>`, `HasOpenWindows(exe)`, `Activate(windowInfo)`.
- `Win32WindowQuery.GetOpenWindows(exe)` — enumerates top-level visible windows
  for an executable via `EnumWindows`.
- `DockItemViewModel.IsRunning` — the white dot (watcher polls
  `HasOpenWindows`).

What is missing: the popup UI, the DWM live-thumbnail interop, and the hover
wiring.

## Goal

On hover over a program item that has open windows, show a themed popup below
(above if the dock is in the lower half of the screen) the dock, centered on
the icon, listing each open window as a **live DWM thumbnail** with its window
title below. Clicking a thumbnail activates that window and closes the popup.

## Components

### 1. `DwmThumbnailInterop` (new — `CedroModernDock.Infrastructure.Windows/Native`)

P/Invoke wrapper:

- `DwmRegisterThumbnail(IntPtr hwndDest, IntPtr hwndSource, out IntPtr thumb)`
- `DwmUnregisterThumbnail(IntPtr thumb)`
- `DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES props)`
- `DwmQueryThumbnailSourceSize(IntPtr thumb, out DWM_SIZE size)`

Structures: `DWM_THUMBNAIL_PROPERTIES` (flags `DWM_TNP_RECTDESTINATION |
DWM_TNP_OPACITY | DWM_TNP_VISIBLE`, destination `RECT`, opacity, visible),
`DWM_SIZE` (cx, cy).

**Best-effort contract:** every call tolerates failures silently (returns
false/ignores HRESULT). The UI never depends on a thumbnail actually
rendering.

### 2. `WindowPreviewPopup` (new — `CedroModernDock/Views/WindowPreviewPopup.axaml` + `.cs`)

An Avalonia `Window`:

- `TransparencyLevelHint="Transparent"`, `Background="Transparent"`,
  `SystemDecorations="None"`, `CanResize="False"`, `ShowInTaskbar="False"`,
  `Topmost="True"` while visible.
- `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` applied after show (same native
  pattern as `DockWindowBehavior`) so it never steals focus and stays out of
  Alt-Tab.
- Content: vertical `StackPanel` of rows. Each row:
  - `Border`: `CornerRadius` = dock rounding, `BorderBrush` = dock color
    (with alpha), `BorderThickness="1"`, **transparent interior** so the DWM
    thumbnail shows through the window content.
  - Inside: a transparent thumbnail area (fixed ~160x90) + a title
    `TextBlock` below it.
  - Title text color: black/white chosen by dock background brightness
    (ported from JavaFX `getTextColorForBackground`).
- API: `ShowFor(item, button, dockWindow, dockThemeValues)` —
  builds rows, registers thumbnails, positions, shows. `HidePopup()` —
  unregisters all thumbnails and hides.

### 3. `WindowTitleFormatter` (new — `CedroModernDock.Core`)

Pure static logic, direct port of JavaFX `formatWindowTitle`:

- `StripRedundantAppSuffix(title, appLabel)`: removes a case-insensitive
  trailing `" - <appLabel>"`; empty result falls back to the original.
- `Truncate(title, 40)`: appends `"..."` past 40 chars.
- `Format(title, appLabel)`: strip then truncate.

Unit-testable; used by the popup to display titles.

### 4. MainWindow hover wiring (modified — `Views/MainWindow.axaml.cs` + `Views/MainWindow.axaml`)

- Add `PointerEntered` / `PointerExited` handlers to the item `Button` in the
  DataTemplate (only meaningful for program items — handler checks the VM type
  / executable path).
- `PointerEntered` → async query:
  - Request-id counter; each hover increments; stale results ignored.
  - Run `WindowPreviewService.LoadPreview(item)` on a background thread.
  - On completion (UI thread): if the request is current, the pointer is still
    over the button, and the list is non-empty → `popup.ShowFor(...)`.
- `PointerExited` → 80ms debounce → hide unless the pointer is over the popup.
- Popup `PointerEntered` cancels the pending hide; popup `PointerExited`
  schedules it.
- Thumbnail click → `WindowPreviewService.Activate(windowInfo)` + hide popup.
- The popup instance is owned by `MainWindow` (single instance, like the
  settings singleton pattern).

## Behavior Details

- **Scope:** program items only (`DockProgramItemModel`), matching JavaFX.
  Settings / module / folder items show no popup.
- **Positioning:** centered horizontally over the hovered icon, 5px gap.
  Below the dock bar if the dock is in the upper half of the screen, above if
  in the lower half (ported from JavaFX `reposition`).
- **Reposition on size change:** when popup content/layout changes, recompute
  the position and the thumbnail destination rects.
- **Thumbnails:**
  - Registered after the popup is shown and laid out, one per window row,
    destination = thumb-area client rect in the popup HWND.
  - Aspect preserved: query source size, fit into 160x90, centered.
  - Live by nature of DWM; no polling.
  - If a source window closes while open, the failed update is ignored; the
    next hover re-queries.
- **Hide:** unregister all thumbnails on hide so no stale handles remain.

## Error Handling

- All native calls are best-effort; never crash on failure.
- `DwmUnregisterThumbnail` on an invalid source returns `E_INVALIDARG` —
  ignored.
- Popup show/hide must never throw; wrap native segments in try/catch.

## Verification

- **Spike (first implementation step):** confirm DWM thumbnails render
  through an Avalonia `TransparencyLevelHint="Transparent"` window. Fallback
  if they do not: host each thumbnail in a native child window
  (`ChildHwndHost`). Do not build the full UI until the spike passes.
- Unit tests: `WindowTitleFormatter` (suffix strip, truncate, empty/edge
  cases). Existing 22 tests stay green.
- Manual on-screen check: hover Chrome/VS Code with open windows → popup with
  live thumbnails; thumbnail updates while the window changes; click activates
  the window; popup hides on leave without flicker; no focus steal.
