# Cedro Modern Dock Rev — Changelog

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
