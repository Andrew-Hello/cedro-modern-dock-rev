# Cedro Modern Dock Rev — Changelog

## Rev v1.3.0 — 2026-08-27

Fourth stable Rev release, focused on a dedicated, conservative Bottom AppBar positioning mode that lets maximized Windows applications avoid the Dock without mixing AppBar reservations into Static or Dynamic positioning.

### Dedicated Bottom AppBar mode

- Added `BOTTOM_APPBAR` as a third positioning mode alongside Static and Dynamic.
- Removed AppBar activation from the General-tab `reserveDesktopSpace` preference path; legacy saved values no longer activate AppBar behavior.
- Static and Dynamic positioning never register a Windows AppBar.
- Bottom AppBar is intentionally limited to the bottom screen edge and always uses a horizontal Dock.
- Added only three valid Bottom AppBar alignments: Left, Center and Right.
- Bottom AppBar sits directly above the Windows taskbar/work-area boundary.
- Free drag, arbitrary screen-spacing controls and vertical Dock layout are not available while Bottom AppBar owns the Shell reservation.
- Entering Bottom AppBar clears legacy dynamic-edge state and disables the four-edge auto-hide state so the two positioning systems cannot compete.

### AppBar lifecycle stability redesign

- Replaced the earlier general-purpose AppBar experiment with a single-reservation lifecycle managed only by the dedicated mode.
- `ABM_NEW` is issued at most once per Bottom AppBar mode activation.
- `ABM_QUERYPOS` is used only during the initial registration, before Cedro changes the Windows work area.
- The negotiated lower boundary is then kept fixed for the lifetime of that AppBar registration.
- Subsequent height changes reuse the same registration with `ABM_SETPOS`; Cedro does not re-query an already-reduced work area.
- Removed the periodic AppBar Shell polling path.
- Removed the `ABN_POSCHANGED` feedback/callback loop from the AppBar implementation.
- Opening or closing Settings no longer registers, removes, or renegotiates AppBar geometry.
- Horizontal Left/Center/Right changes reposition the Dock without changing the reserved work-area height.
- Leaving Bottom AppBar or exiting Cedro sends `ABM_REMOVE` once and restores normal Windows work-area ownership.
- Cedro releases the AppBar before resolving a new Static/Dynamic position, avoiding accidental use of the previous reduced work area as an extra margin.

### Taskbar coexistence

- Initial AppBar negotiation uses the taskbar-aware work area before Cedro registers itself.
- The Windows taskbar remains the fixed lower boundary; Cedro reserves only the strip immediately above it.
- Existing bottom taskbar avoidance remains intact for normal Static/Dynamic edge docking.
- Bottom AppBar never attempts to claim or overlap the taskbar's own screen edge.

### Height-change compatibility

- Bottom AppBar now observes the real native SizeToContent Dock height.
- Added a short debounce so several Avalonia layout changes collapse into one AppBar height update.
- Changes to Dock vertical padding and icon size update the existing AppBar reservation rather than creating a new reservation.
- Running-indicator visibility/state and other actual layout-height changes use the same single-reservation update path.
- Height updates are no-ops when the native Dock height has not changed, so harmless Dock refreshes do not touch Windows Shell geometry.

### Isolation and safety guards

- Core edge-auto-hide getters return disabled while `BOTTOM_APPBAR` is active, protecting imported/legacy configurations that still contain enabled edge-auto-hide fields.
- Only true Static positioning uses the legacy static `SizeChanged → ApplyDockPosition` route; Bottom AppBar geometry is controlled exclusively by its AppBar module.
- Bottom AppBar runtime forces horizontal layout if an imported configuration contains a contradictory vertical-Dock value.
- AppBar cleanup is explicitly performed during the main-window close lifecycle before the HWND is discarded.
- The redesigned architecture was verified by Windows CI and then validated on real Windows hardware without the previous recursive/nested work-area drift.

### Compatibility

- Existing Rev v1.2.0, v1.1.0, v1.0.0 and upstream-compatible configurations remain loadable.
- The old `reserveDesktopSpace` field may remain in JSON for backward compatibility but is no longer an AppBar activation switch.
- Existing Static/Dynamic four-edge docking behavior remains available outside Bottom AppBar mode.
- Rev v1.2.0 remains frozen as the previous known-good rollback baseline.

---

## Rev v1.2.0 — 2026-08-27

Third stable Rev release, focused on script launchers and a full Windows system-icon resource browser.

### Windows script launchers

- Added **Add Script (.bat/.cmd/.vbs)** to **Settings → Icons**.
- Added direct support for `.bat`, `.cmd` and `.vbs` launch targets while keeping them as normal program items for ordering, removal, icon customization and config export/import.
- Script launchers use normal Windows Shell association semantics, matching Explorer double-click behavior.
- The script's containing directory is explicitly used as the working directory so BAT/VBS wrappers can reliably call sibling `.ps1` files and other relative resources.
- Added Shell-associated default icon extraction for scripts so newly pinned launchers do not appear blank.
- Added English and Simplified Chinese strings for script selection and settings controls.

### Multi-library Windows system icon center

- Replaced the single-library `SHELL32.dll` browser with a categorized multi-library icon picker.
- Added the following curated Windows resource catalog:
  - Common: `SHELL32.dll`, `imageres.dll`
  - Devices: `DDORes.dll`, `setupapi.dll`, `compstui.dll`
  - Network: `netshell.dll`, `netcenter.dll`, `networkexplorer.dll`
  - Classic: `moricons.dll`, `pifmgr.dll`
  - Other: `explorer.exe`, `mmres.dll`, `wmploc.dll`
- Resource files missing from the current Windows edition/build are skipped automatically.
- Added a top-level library selector with category labels and resolved-path display.
- Each selected DLL/EXE is scanned asynchronously and shown as a clickable icon thumbnail grid with resource indexes.
- Switching libraries cancels the old scan before starting the new one.

### Lightweight system-icon persistence

- System-library icons are no longer copied into `config.json` as Base64 PNG data.
- Added per-item `customSystemIconSource` and `customSystemIconIndex` properties.
- System icon selections are persisted as `%SystemRoot%`-based source expressions plus a zero-based resource index, for example `%SystemRoot%\System32\imageres.dll` + `15`.
- `%SystemRoot%` is expanded only at runtime, avoiding hard-coded `C:\Windows` paths.
- System icons are dynamically extracted when the Dock/settings list resolves the item's icon.
- Imported PNG/ICO/JPG/BMP/GIF/TIFF overrides continue to use the portable Base64 PNG representation introduced in Rev v1.1.0.
- Selecting a system icon clears an imported-image override and vice versa, preventing conflicting override sources.
- **Restore default icon** clears both override modes.
- Existing Rev v1.1.0 Base64 icon configurations remain compatible.
- Documented the Windows-version caveat: resource indexes can theoretically change across Windows releases, so imported PNG/ICO overrides remain preferable when exact cross-PC visual identity is required.

### UI and refresh behavior

- Settings-list icon previews now resolve both imported image overrides and dynamic system-library overrides.
- Custom icon state is refreshed immediately after applying, resetting, moving or reordering Dock items.
- Added complete English and Simplified Chinese localization for system-icon categories, library selection, loading/error states and helper text.

### Build and compatibility

- Verified the complete Rev v1.2.0 development baseline with the Windows GitHub Actions Release build.
- Existing Rev v1.0.0 and v1.1.0 configurations remain loadable because all new system-icon fields are optional.
- Rev v1.1.0 and Rev v1.0.0 remain frozen as previous known-good rollback points.

---

## Rev v1.1.0 — 2026-08-27

Second stable Rev release, focused on portable per-item icon customization and a cleaner development/release workflow.

### Custom icon overrides

- Added a **Custom icon override** section to **Settings → Icons**.
- Added **Choose custom icon...** and **Restore default icon** actions for the selected pinned item.
- Supports PNG, ICO, JPG/JPEG, BMP, GIF and TIFF source images.
- Custom icons take priority over all automatic icon sources, including EXE extraction, packaged Windows App/UWP identity icons, Edge/Chromium PWA icons, folder icons and built-in Windows module icons.
- Custom overrides also work for the Cedro Settings item.
- Selected source images are normalized to PNG before persistence.
- Added `%APPDATA%\CedroModernDock\customIcons` as the managed runtime cache.
- Custom PNG data is embedded in the item's JSON configuration as Base64, making configuration exports self-contained.
- Export/import therefore migrates custom icon overrides to another PC without requiring a separate icon-folder copy.
- Added immediate Dock refresh and Settings-list preview refresh after applying or resetting an override.
- Added English and Simplified Chinese UI strings for the new controls.

### Build and release workflow

- Updated the enhanced-branch CI to use concurrency cancellation so stale builds are automatically replaced by the newest commit during rapid development.
- Added a dedicated Rev v1.1.0 stable-release workflow and portable Windows x64 package naming.
- Updated the main README to make Rev v1.1.0 the current documented release while retaining Rev v1.0.0 as a rollback baseline.

### Compatibility

- Existing Rev v1.0.0 and upstream configurations remain loadable because the custom-icon field is optional.
- Items without a custom override continue to use the existing automatic icon-resolution pipeline unchanged.
- Rev v1.0.0 remains frozen and available as a previous known-good release.

---

## Rev v1.0.0 — 2026-08-26

First stable enhanced baseline of `cedro-modern-dock-rev`.

### Desktop/window integration

- Reworked the Dock as a transparent top-level window instead of attaching it to the desktop WorkerW host.
- Fixed transparency so desktop icons and labels remain visible through the Dock background.
- Added optional Always-on-top behavior.
- Added true fullscreen detection so games and fullscreen applications temporarily suppress topmost automatically.

### Edge docking and auto-hide

- Added static and dynamic edge docking.
- Added magnetic drag-to-edge docking in Dynamic mode.
- Added all four edges (Top, Bottom, Left, Right).
- Added independent switches for horizontal edges (Top/Bottom) and vertical edges (Left/Right).
- Bottom docking uses the monitor work-area boundary so the Dock stays above the Windows taskbar.
- Added animated hide/reveal behavior.
- Added a persistent visible edge tail that can be hovered or clicked to restore the Dock.
- Tail appears only after docking/hide animation completes.
- Dynamic edge/offset state is persisted across restarts.

### Appearance and interaction settings

- Added configurable Dock vertical padding.
- Added optional running indicator dots.
- Added optional hover magnification.
- Added optional hover labels.
- Added optional live window previews.
- Moved tooltip placement above icons where enabled.

### Running applications

- Added unpinned running-app section.
- Added taskbar-style click behavior for single-window running apps.
- Added live DWM window previews and window activation/close controls.
- Added right-click **Pin to Dock** for running apps.
- Added drag from running-app section into pinned section to pin.

### Windows App / packaged app / PWA support

- Improved UWP/AppX running-window resolution through ApplicationFrameHost/CoreWindow handling.
- Added AppUserModelID (AUMID) detection.
- Added packaged app relaunch via `shell:AppsFolder\<AUMID>`.
- Added packaged-app manifest icon fallback.
- Added best-effort per-window AppUserModelID detection for Edge/Chromium installed web apps so PWAs can be pinned separately from the browser executable.

### Configuration and migration

- Configuration remains stored at `%APPDATA%\CedroModernDock\config.json`.
- Added Settings shortcuts for opening the backup folder, exporting configuration and importing configuration.
- Added validation before replacing a configuration.
- Added automatic pre-import timestamped backups.
- Added automatic application restart after a successful import with single-instance retry handling.

### Build/release

- Added GitHub Actions Windows build pipeline for the enhanced branch.
- Added portable x64 build artifacts.
- Introduced a frozen stable baseline and dedicated Rev release tag naming scheme.

### Compatibility

The Rev configuration remains based on the upstream Cedro JSON model. New properties are optional/defaulted so existing configurations can continue loading.
