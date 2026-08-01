# Window Preview Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On hover over a program item with open windows, show a themed popup with live DWM thumbnails of each window; clicking a thumbnail activates the window.

**Architecture:** A transparent, borderless Avalonia `Window` (`WindowPreviewPopup`) hosts per-window rows (rounded dock-colored border, 160x90 transparent thumbnail area, title below). Live previews come from the Windows DWM thumbnail API (`DwmRegisterThumbnail` + friends) rendering into the popup's HWND beneath the window content. `MainWindow` wires `PointerEntered`/`PointerExited` on item buttons: async window query (request-id guard), 80ms hide debounce, popup-hover keeps it open. Pure title formatting lives in `WindowTitleFormatter` (Core, unit-tested). Spec: `docs/superpowers/specs/2026-08-01-window-preview-popup-design.md`.

**Tech Stack:** .NET 9, Avalonia 11.3.12, Windows dwmapi.dll P/Invoke, xunit.

**Working tree note:** dock process locks its exe — kill it before builds: `Get-Process CedroModernDock -ErrorAction SilentlyContinue | Stop-Process -Force`.

**Test command:** `dotnet test dotnet\tests\CedroModernDock.Tests\CedroModernDock.Tests.csproj --nologo` (baseline: 22 passing)

---

## File Structure

| File | Responsibility |
|---|---|
| `dotnet/src/CedroModernDock.Core/Application/WindowTitleFormatter.cs` (new) | Pure title/contrast logic (strip suffix, truncate, text color) |
| `dotnet/src/CedroModernDock.Infrastructure.Windows/Native/DwmThumbnailInterop.cs` (new) | P/Invoke wrapper for the DWM thumbnail API |
| `dotnet/src/CedroModernDock/Views/SpikeThumbnailWindow.axaml` + `.axaml.cs` (temporary) | Spike: prove DWM thumbnails render through a transparent Avalonia window — DELETED after verification |
| `dotnet/src/CedroModernDock/Views/WindowPreviewPopup.axaml` + `.axaml.cs` (new) | The popup: rows, thumbnail registration, positioning, hide |
| `dotnet/src/CedroModernDock/Views/MainWindow.axaml` (modify) | Add `PointerEntered`/`PointerExited` to item `Button` |
| `dotnet/src/CedroModernDock/Views/MainWindow.axaml.cs` (modify) | Hover wiring: debounce, async query, show/hide popup |
| `dotnet/src/CedroModernDock/ViewModels/MainWindowViewModel.cs` (modify) | Add `PreviewDismissAction` (hide popup on dock refresh) |
| `dotnet/tests/CedroModernDock.Tests/WindowTitleFormatterTests.cs` (new) | Unit tests for `WindowTitleFormatter` |

---

## Task 1: WindowTitleFormatter (Core) — TDD

**Files:**
- Create: `dotnet/src/CedroModernDock.Core/Application/WindowTitleFormatter.cs`
- Test: `dotnet/tests/CedroModernDock.Tests/WindowTitleFormatterTests.cs`

- [ ] **Step 1: Write the failing tests**

`dotnet/tests/CedroModernDock.Tests/WindowTitleFormatterTests.cs`:

```csharp
using CedroModernDock.Core.Application;

namespace CedroModernDock.Tests;

public class WindowTitleFormatterTests
{
    [Fact]
    public void Format_StripsRedundantAppSuffix()
    {
        Assert.Equal("Untitled", WindowTitleFormatter.Format("Untitled - Chrome", "Chrome"));
    }

    [Fact]
    public void Format_StripsSuffixCaseInsensitively()
    {
        Assert.Equal("Untitled", WindowTitleFormatter.Format("Untitled - CHROME", "chrome"));
    }

    [Fact]
    public void Format_KeepsTitleWhenLabelDoesNotMatch()
    {
        Assert.Equal("Untitled - Chrome", WindowTitleFormatter.Format("Untitled - Chrome", "Vivaldi"));
    }

    [Fact]
    public void Format_TruncatesLongTitles()
    {
        Assert.Equal(new string('a', 40) + "...", WindowTitleFormatter.Format(new string('a', 45), "Chrome"));
    }

    [Fact]
    public void Format_HandlesNullTitle()
    {
        Assert.Equal("", WindowTitleFormatter.Format(null, "Chrome"));
    }

    [Fact]
    public void Format_DoesNotStripWhenResultWouldBeEmpty()
    {
        Assert.Equal("Chrome", WindowTitleFormatter.Format("Chrome", "Chrome"));
    }

    [Fact]
    public void IsDarkBackground_BlackReturnsTrue()
    {
        Assert.True(WindowTitleFormatter.IsDarkBackground("0, 0, 0, "));
    }

    [Fact]
    public void IsDarkBackground_WhiteReturnsFalse()
    {
        Assert.False(WindowTitleFormatter.IsDarkBackground("255, 255, 255, "));
    }

    [Fact]
    public void IsDarkBackground_RedIsDark()
    {
        Assert.True(WindowTitleFormatter.IsDarkBackground("255, 0, 0, "));
    }

    [Fact]
    public void IsDarkBackground_InvalidFallsBackToDark()
    {
        Assert.True(WindowTitleFormatter.IsDarkBackground(""));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet\tests\CedroModernDock.Tests\CedroModernDock.Tests.csproj --nologo`
Expected: FAIL — `WindowTitleFormatter` does not exist.

- [ ] **Step 3: Implement**

`dotnet/src/CedroModernDock.Core/Application/WindowTitleFormatter.cs`:

```csharp
namespace CedroModernDock.Core.Application;

/// <summary>
/// Direct port of WindowPreviewPopup's title/contrast logic (JavaFX).
/// Pure functions — no UI dependency.
/// </summary>
public static class WindowTitleFormatter
{
    public const int MaxTitleLength = 40;

    public static string Format(string? windowTitle, string? appLabel)
        => Truncate(StripRedundantAppSuffix(windowTitle, appLabel), MaxTitleLength);

    public static string StripRedundantAppSuffix(string? windowTitle, string? appLabel)
    {
        if (string.IsNullOrEmpty(windowTitle)) return "";
        if (string.IsNullOrEmpty(appLabel)) return windowTitle;

        string suffix = " - " + appLabel;
        if (windowTitle.Length >= suffix.Length &&
            windowTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            string stripped = windowTitle[..^suffix.Length].Trim();
            if (!string.IsNullOrEmpty(stripped)) return stripped;
        }
        return windowTitle;
    }

    public static string Truncate(string? title, int maxLength)
    {
        if (string.IsNullOrEmpty(title)) return "";
        if (title.Length <= maxLength) return title;
        return title[..maxLength] + "...";
    }

    /// <summary>
    /// True when the dock background RGB is dark enough to need white text
    /// (brightness threshold 128, JavaFX parity).
    /// </summary>
    public static bool IsDarkBackground(string? colorRgb)
    {
        if (string.IsNullOrWhiteSpace(colorRgb)) return true;
        var parts = colorRgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            byte.TryParse(parts[0], out byte r) &&
            byte.TryParse(parts[1], out byte g) &&
            byte.TryParse(parts[2], out byte b))
        {
            double brightness = r * 0.299 + g * 0.587 + b * 0.114;
            return brightness <= 128;
        }
        return true;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet\tests\CedroModernDock.Tests\CedroModernDock.Tests.csproj --nologo`
Expected: PASS — 32 tests, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add dotnet/tests/CedroModernDock.Tests/WindowTitleFormatterTests.cs dotnet/src/CedroModernDock.Core/Application/WindowTitleFormatter.cs
git commit -m "feat: window title formatter for preview popup"
```

---

## Task 2: DwmThumbnailInterop (native interop)

No unit tests: pure P/Invoke surface, verified by the spike (Task 3) and the manual check (Task 6). Correct struct layout is critical — see the comment.

**Files:**
- Create: `dotnet/src/CedroModernDock.Infrastructure.Windows/Native/DwmThumbnailInterop.cs`

- [ ] **Step 1: Implement**

```csharp
using System;
using System.Runtime.InteropServices;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Minimal DWM thumbnail interop: renders a live preview of a source window
/// (HWND) into a destination window's client area. All calls are best-effort:
/// failures return false and must never crash the caller.
/// </summary>
public static class DwmThumbnailInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DwmSize
    {
        public int Cx;
        public int Cy;
    }

    // Native layout: dwFlags(4) rcDestination(16) rcSource(16) opacity(1)
    // padding(3) fVisible(4) fSourceClientAreaOnly(4) = 48 bytes. Field order
    // and the rcSource placeholder MUST be kept exactly as-is.
    [StructLayout(LayoutKind.Sequential)]
    public struct DwmThumbnailProperties
    {
        public uint Flags;
        public RECT Destination;
        public RECT Source;       // zero = whole source window
        public byte Opacity;
        public int Visible;       // BOOL
        public int SourceClientAreaOnly;
    }

    public const uint DWM_TNP_RECTDESTINATION = 0x00000001;
    public const uint DWM_TNP_OPACITY = 0x00000004;
    public const uint DWM_TNP_VISIBLE = 0x00000008;

    [DllImport("dwmapi.dll")]
    private static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, out IntPtr phThumbnailId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DwmThumbnailProperties ptnProperties);

    [DllImport("dwmapi.dll")]
    private static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnailId, out DwmSize pSize);

    /// <summary>Registers a live thumbnail of <paramref name="sourceHwnd"/> into <paramref name="destHwnd"/>.</summary>
    public static bool Register(IntPtr destHwnd, IntPtr sourceHwnd, out IntPtr thumbId)
    {
        thumbId = IntPtr.Zero;
        if (destHwnd == IntPtr.Zero || sourceHwnd == IntPtr.Zero) return false;
        return DwmRegisterThumbnail(destHwnd, sourceHwnd, out thumbId) == 0;
    }

    public static bool Unregister(IntPtr thumbId)
    {
        if (thumbId == IntPtr.Zero) return false;
        return DwmUnregisterThumbnail(thumbId) == 0;
    }

    /// <summary>
    /// Positions the thumbnail at <paramref name="dest"/> (physical pixels of the
    /// destination window client area), optionally letterboxing via a pre-computed rect.
    /// </summary>
    public static bool Update(IntPtr thumbId, RECT dest, int opacity = 255, bool visible = true)
    {
        if (thumbId == IntPtr.Zero) return false;
        var props = new DwmThumbnailProperties
        {
            Flags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE,
            Destination = dest,
            Source = default,
            Opacity = (byte)opacity,
            Visible = visible ? 1 : 0,
            SourceClientAreaOnly = 0
        };
        return DwmUpdateThumbnailProperties(thumbId, ref props) == 0;
    }

    public static bool QuerySourceSize(IntPtr thumbId, out DwmSize size)
    {
        size = default;
        if (thumbId == IntPtr.Zero) return false;
        return DwmQueryThumbnailSourceSize(thumbId, out size) == 0;
    }
}
```

- [ ] **Step 2: Build**

Run: `Get-Process CedroModernDock -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet build dotnet\src\CedroModernDock\CedroModernDock.csproj --nologo`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add dotnet/src/CedroModernDock.Infrastructure.Windows/Native/DwmThumbnailInterop.cs
git commit -m "feat: DWM thumbnail interop"
```

---

## Task 3: Spike — DWM thumbnails through a transparent Avalonia window

De-risks the only uncertain part (spec Verification section). If thumbnails do NOT render through the transparent window, STOP and switch to the `ChildHwndHost` fallback before continuing.

**Files:**
- Create (temporary): `dotnet/src/CedroModernDock/Views/SpikeThumbnailWindow.axaml`
- Create (temporary): `dotnet/src/CedroModernDock/Views/SpikeThumbnailWindow.axaml.cs`
- Modify (temporary line): `dotnet/src/CedroModernDock/Views/MainWindow.axaml.cs` `OnOpened`

- [ ] **Step 1: Create the spike window XAML**

`dotnet/src/CedroModernDock/Views/SpikeThumbnailWindow.axaml`:

```xml
<Window x:Class="CedroModernDock.Views.SpikeThumbnailWindow"
        xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Spike"
        Width="320" Height="200"
        CanResize="False" SystemDecorations="None" ShowInTaskbar="False"
        Topmost="True" Background="Transparent" TransparencyLevelHint="Transparent">
    <Border x:Name="ThumbArea" Background="Transparent"
            BorderBrush="Red" BorderThickness="2"
            Margin="20" Width="280" Height="160"
            HorizontalAlignment="Center" VerticalAlignment="Center"/>
</Window>
```

(Red border so the area is locatable in captures even if the thumbnail fails.)

- [ ] **Step 2: Create the spike code-behind**

`dotnet/src/CedroModernDock/Views/SpikeThumbnailWindow.axaml.cs`:

```csharp
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

public partial class SpikeThumbnailWindow : Window
{
    private readonly IntPtr _sourceHwnd;
    private IntPtr _thumb;

    public SpikeThumbnailWindow(IntPtr sourceHwnd)
    {
        InitializeComponent();
        _sourceHwnd = sourceHwnd;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        IPlatformHandle? handle = TryGetPlatformHandle();
        if (handle == null) { Close(); return; }

        Dispatcher.UIThread.Post(() =>
        {
            if (!DwmThumbnailInterop.Register(handle.Handle, _sourceHwnd, out _thumb)) { Close(); return; }

            var area = ThumbArea.TransformToVisual(this)?.TransformBounds(ThumbArea.Bounds);
            if (area == null) { Close(); return; }

            double scale = RenderScaling;
            var rect = new DwmThumbnailInterop.RECT
            {
                Left = (int)(area.Value.X * scale),
                Top = (int)(area.Value.Y * scale),
                Right = (int)((area.Value.X + area.Value.Width) * scale),
                Bottom = (int)((area.Value.Y + area.Value.Height) * scale)
            };
            DwmThumbnailInterop.Update(_thumb, rect);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            timer.Tick += (_, _) => { timer.Stop(); Close(); };
            timer.Start();
        }, DispatcherPriority.Loaded);
    }

    protected override void OnClosed(EventArgs e)
    {
        DwmThumbnailInterop.Unregister(_thumb);
        _thumb = IntPtr.Zero;
        base.OnClosed(e);
    }
}
```

- [ ] **Step 3: Show the spike from MainWindow (temporary line)**

In `dotnet/src/CedroModernDock/Views/MainWindow.axaml.cs` `OnOpened`, after `_dockBehavior.Apply();` add:

```csharp
        // TEMP SPIKE — remove after verification.
        new SpikeThumbnailWindow(handle.Handle).Show();
```

- [ ] **Step 4: Build, run, verify**

Run: `Get-Process CedroModernDock -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet build dotnet\src\CedroModernDock\CedroModernDock.csproj --nologo` then start the exe, wait 6s.

Then capture the spike window region (it shows at the default window position — capture the primary screen's center area and locate the red border via scan):

> **Note:** `regioncap` / `pixelprobe` are helper exes in `%TEMP%\opencode` (`C:\Users\ARTHUR~1\AppData\Local\Temp\opencode`) — NOT on PATH. Invoke with full path, e.g. `& "$env:TEMP\opencode\regioncap.exe" 500 300 500 400 spike.png` (or run from that directory).

```
regioncap 500 300 500 400 spike.png
```

Expected: a red-framed rect containing a scaled-down live copy of the dock (black background + icon colors). Verify with pixelprobe on `spike.png`:
- Red border pixels present (R>150, G<80).
- Inside the frame, at least one pixel matching the dock's black (R≈G≈B≈0).
- At least one saturated icon-colored pixel inside the frame (icons are vivid on black).

If the interior is the wallpaper (no black, no icon colors) → thumbnails did NOT render → STOP, report, switch to `ChildHwndHost` plan revision.

- [ ] **Step 5: Remove the spike**

Delete `SpikeThumbnailWindow.axaml` and `SpikeThumbnailWindow.axaml.cs`, remove the temporary `OnOpened` line. Rebuild (0 errors).

- [ ] **Step 6: Commit**

```bash
git add -A dotnet/src/CedroModernDock
git commit -m "feat: verify DWM thumbnails render through transparent Avalonia window (spike)"
```

---

## Task 4: WindowPreviewPopup window

**Files:**
- Create: `dotnet/src/CedroModernDock/Views/WindowPreviewPopup.axaml`
- Create: `dotnet/src/CedroModernDock/Views/WindowPreviewPopup.axaml.cs`

- [ ] **Step 1: Create the popup XAML**

```xml
<Window x:Class="CedroModernDock.Views.WindowPreviewPopup"
        xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Cedro Modern Dock Preview"
        CanResize="False" SystemDecorations="None" ShowInTaskbar="False"
        Topmost="True" Background="Transparent" TransparencyLevelHint="Transparent"
        SizeToContent="WidthAndHeight">
    <StackPanel x:Name="RowsPanel" Orientation="Vertical" Spacing="6" Margin="4"/>
</Window>
```

- [ ] **Step 2: Implement the code-behind**

`dotnet/src/CedroModernDock/Views/WindowPreviewPopup.axaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

/// <summary>
/// Popup listing a program item's open windows as live DWM thumbnails.
/// The window is fully transparent; thumbnails render beneath the content,
/// so only the row borders/titles are drawn by Avalonia.
/// </summary>
public partial class WindowPreviewPopup : Window
{
    private const int ThumbWidth = 160;
    private const int ThumbHeight = 90;

    private readonly List<(IntPtr Thumb, Border Area)> _rows = new();
    private readonly List<IntPtr> _sources = new();
    private IReadOnlyList<IntPtr> _sourceHandles = Array.Empty<IntPtr>();
    private string _appLabel = "";
    private string _colorRgb = "0, 0, 0, ";
    private int _rounding = 12;
    private IntPtr _hwnd;

    public WindowPreviewPopup()
    {
        InitializeComponent();
    }

    /// <summary>Populates rows and positions the popup over <paramref name="anchor"/>.</summary>
    public void ShowFor(IReadOnlyList<WindowInfo> windows, string appLabel,
        string colorRgb, int rounding, Button anchor)
    {
        _appLabel = appLabel;
        _colorRgb = colorRgb;
        _rounding = rounding;
        _hwnd = IntPtr.Zero;
        _sourceHandles = new List<IntPtr>(windows.Select(w => w.Handle));

        BuildRows(windows);
        PositionNear(anchor);
        Show();
    }

    private void BuildRows(IReadOnlyList<WindowInfo> windows)
    {
        ClearRows();
        _rows.Clear();

        var parts = _colorRgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        var borderBrush = new SolidColorBrush(Color.FromArgb(160, r, g, b));
        var textBrush = new SolidColorBrush(
            WindowTitleFormatter.IsDarkBackground(_colorRgb) ? Colors.White : Colors.Black);

        foreach (var window in windows)
        {
            var thumbArea = new Border { Width = ThumbWidth, Height = ThumbHeight, Background = Brushes.Transparent };
            var title = new TextBlock
            {
                Text = WindowTitleFormatter.Format(window.Title, _appLabel),
                Foreground = textBrush,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };
            var content = new StackPanel { Orientation = Orientation.Vertical };
            content.Children.Add(thumbArea);
            content.Children.Add(title);

            var row = new Border
            {
                CornerRadius = new CornerRadius(_rounding),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                Padding = new Thickness(6, 6, 6, 0),
                Child = content
            };
            RowsPanel.Children.Add(row);
            _rows.Add((IntPtr.Zero, thumbArea));
            _sources.Add(IntPtr.Zero);
        }
    }

    private void ClearRows()
    {
        foreach (var (thumb, _) in _rows)
            DwmThumbnailInterop.Unregister(thumb);
        RowsPanel.Children.Clear();
    }

    private void PositionNear(Button anchor)
    {
        var anchorCenter = anchor.PointToScreen(new Point(anchor.Bounds.Width / 2, anchor.Bounds.Height / 2));
        Measure(Size.Infinity);
        double scale = RenderScaling;
        int w = (int)(DesiredSize.Width * scale);
        int h = (int)(DesiredSize.Height * scale);

        var screen = Screen.FromPoint(anchorCenter) ?? Screen.AllScreens[0];
        var work = screen.WorkingArea;

        // Place below the dock by default; above when the dock is in the lower half.
        int popupX = anchorCenter.X - w / 2;
        int popupY = anchorCenter.Y + (int)(anchor.Bounds.Height * scale / 2) + 5;
        if (popupY + h > work.Bottom)
            popupY = anchorCenter.Y - (int)(anchor.Bounds.Height * scale / 2) - h - 5;

        popupX = Math.Max(work.Left + 4, Math.Min(popupX, work.Right - w - 4));
        popupY = Math.Max(work.Top + 4, popupY);
        Position = new PixelPoint(popupX, popupY);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        // Never steal focus: same extended styles as DockWindowBehavior.ApplyExtendedStyles.
        if (_hwnd != IntPtr.Zero)
        {
            IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
            int exStyle = exStylePtr.ToInt32();
            exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;
            exStyle &= ~Win32Constants.WS_EX_APPWINDOW;
            User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        // Single registration path: layout must be final before registering
        // thumbnails, so defer to Loaded priority. Source HWNDs were stored by
        // ShowFor (_sourceHandles).
        Dispatcher.UIThread.Post(RegisterThumbnails, DispatcherPriority.Loaded);
    }

    private void RegisterThumbnails()
    {
        if (_hwnd == IntPtr.Zero) return;
        double scale = RenderScaling;

        for (int i = 0; i < _rows.Count && i < _sourceHandles.Count; i++)
        {
            var area = _rows[i].Area;
            var bounds = area.TransformToVisual(this)?.TransformBounds(area.Bounds);
            if (bounds == null) continue;

            IntPtr sourceHwnd = _sourceHandles[i];
            if (sourceHwnd == IntPtr.Zero) continue;
            if (!DwmThumbnailInterop.Register(_hwnd, sourceHwnd, out IntPtr thumb)) continue;
            _rows[i] = (thumb, area);
            _sources[i] = sourceHwnd;

            if (!DwmThumbnailInterop.QuerySourceSize(thumb, out var srcSize) || srcSize.Cx <= 0 || srcSize.Cy <= 0)
                continue;

            // Fit preserving aspect inside the area (letterbox), physical pixels.
            double areaW = bounds.Value.Width * scale;
            double areaH = bounds.Value.Height * scale;
            double ar = (double)srcSize.Cx / srcSize.Cy;
            double fitW = areaW, fitH = areaW / ar;
            if (fitH > areaH) { fitH = areaH; fitW = areaH * ar; }
            double ox = bounds.Value.X * scale + (areaW - fitW) / 2;
            double oy = bounds.Value.Y * scale + (areaH - fitH) / 2;

            var rect = new DwmThumbnailInterop.RECT
            {
                Left = (int)ox,
                Top = (int)oy,
                Right = (int)(ox + fitW),
                Bottom = (int)(oy + fitH)
            };
            DwmThumbnailInterop.Update(thumb, rect);
        }
    }

    public void HidePopup()
    {
        ClearRows();
        Hide();
    }
}
```

**NOTE — apply all three corrections while implementing:**
1. Registration is fully internal to the popup: `ShowFor` stores the source HWNDs (`_sourceHandles`), `OnOpened` posts `RegisterThumbnails()` (private, parameterless) at Loaded priority — the SINGLE registration path. MainWindow must NOT call any registration method after `ShowFor` (double registration would leak thumb IDs and overwrite `_rows[i]`).
2. `OnOpened` applies `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (and clears `WS_EX_APPWINDOW`) via `User32.GetWindowLongPtr`/`User32.SetWindowLongPtr` — same pattern as `DockWindowBehavior.ApplyExtendedStyles` — so the popup never steals focus. `User32`/`Win32Constants` live in `CedroModernDock.Infrastructure.Windows.Native`.
3. `DwmThumbnailInterop` is in namespace `CedroModernDock.Infrastructure.Windows.Native` — verify the `using` matches the file's actual namespace.

- [ ] **Step 3: Build**

Run: `Get-Process CedroModernDock -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet build dotnet\src\CedroModernDock\CedroModernDock.csproj --nologo`
Expected: 0 errors. (Popup isn't reachable from the UI yet — no behavior change.)

- [ ] **Step 4: Commit**

```bash
git add dotnet/src/CedroModernDock/Views/WindowPreviewPopup.axaml dotnet/src/CedroModernDock/Views/WindowPreviewPopup.axaml.cs
git commit -m "feat: window preview popup with live DWM thumbnails"
```

---

## Task 5: MainWindow hover wiring

**Files:**
- Modify: `dotnet/src/CedroModernDock/Views/MainWindow.axaml` (item Button — add 2 attributes)
- Modify: `dotnet/src/CedroModernDock/Views/MainWindow.axaml.cs`
- Modify: `dotnet/src/CedroModernDock/ViewModels/MainWindowViewModel.cs` (PreviewDismissAction)

- [ ] **Step 1: Add pointer handlers to the item Button**

In `MainWindow.axaml`, on the item `Button` (inside the DataTemplate), add:

```xml
                            PointerEntered="OnItemPointerEntered"
                            PointerExited="OnItemPointerExited"
```

- [ ] **Step 2: Add PreviewDismissAction to the ViewModel**

In `MainWindowViewModel.cs`, next to `RepositionAction`:

```csharp
    /// <summary>Set by MainWindow — dismisses the window-preview popup (dock refresh).</summary>
    public Action? PreviewDismissAction { get; set; }
```

And at the end of `UpdateDockUI()` (after `RepositionAction?.Invoke();`):

```csharp
        PreviewDismissAction?.Invoke();
```

- [ ] **Step 3: Implement hover wiring in MainWindow code-behind**

Add to `MainWindow.axaml.cs` fields:

```csharp
    private WindowPreviewPopup? _previewPopup;
    private Button? _hoveredButton;
    private int _previewRequestId;
    private bool _isOverPreview;
    private readonly DispatcherTimer _previewHideDebounce = new() { Interval = TimeSpan.FromMilliseconds(80) };
```

In the constructor (after `InitializeComponent()`):

```csharp
        _previewHideDebounce.Tick += (_, _) =>
        {
            _previewHideDebounce.Stop();
            if (!_isOverPreview)
                HidePreview();
        };
```

In `OnOpened`, after `vm.RepositionAction = () => ApplyDockPosition();`:

```csharp
            vm.PreviewDismissAction = HidePreview;
```

Add the handlers:

```csharp
    private void OnItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || _appServices == null) return;
        if (button.DataContext is not DockItemViewModel vm || vm.Item is not DockProgramItemModel item)
            return;

        _hoveredButton = button;
        _previewHideDebounce.Stop();
        int requestId = ++_previewRequestId;
        var programItem = item;

        Task.Run(() =>
        {
            var windows = _appServices.WindowPreviewService.LoadPreview(programItem);
            Dispatcher.UIThread.Post(() => OnPreviewLoaded(requestId, button, vm, windows));
        });
    }

    private void OnPreviewLoaded(int requestId, Button button, DockItemViewModel vm,
        List<WindowInfo> windows)
    {
        if (requestId != _previewRequestId || _hoveredButton != button) return;
        if (windows.Count == 0) return;
        if (_appServices == null) return;

        var appearance = _appServices.AppearanceService;
        _previewPopup ??= new WindowPreviewPopup();
        _previewPopup.ShowFor(windows, vm.Label,
            appearance.GetDockColorRGB(), appearance.GetDockBorderRounding(), button);
        // Registration happens inside the popup (OnOpened -> Loaded priority). No call here.
    }

    private void OnItemPointerExited(object? sender, PointerEventArgs e)
    {
        if (_hoveredButton == sender) _hoveredButton = null;
        _previewHideDebounce.Stop();
        _previewHideDebounce.Start();
    }

    private void HidePreview()
    {
        ++_previewRequestId;
        _previewPopup?.HidePopup();
        _hoveredButton = null;
    }

    private void OnPopupPointerEntered(object? sender, PointerEventArgs e)
    {
        _isOverPreview = true;
        _previewHideDebounce.Stop();
    }

    private void OnPopupPointerExited(object? sender, PointerEventArgs e)
    {
        _isOverPreview = false;
        _previewHideDebounce.Stop();
        _previewHideDebounce.Start();
    }

    private void OnPopupThumbnailClicked(IntPtr sourceHwnd)
    {
        HidePreview();
        if (sourceHwnd != IntPtr.Zero)
            _appServices?.WindowPreviewService.Activate(
                new WindowInfo(sourceHwnd, ""));
    }
```

**Adjustments while implementing (apply and keep the code consistent):**
1. `WindowInfo` comes from `CedroModernDock.Core.Domain` — add the `using`.
2. The popup must call back for click-to-activate and pointer enter/exit. Give `WindowPreviewPopup` three hooks:
   - `public Action<IntPtr>? ThumbnailClicked { get; set; }` — invoked from each row's `PointerPressed` (row area) with the row's source HWND.
   - `public Action? PointerEnteredCallback { get; set; }` / `public Action? PointerExitedCallback { get; set; }` — invoked from the popup root's `PointerEntered`/`PointerExited`.
   Wire them in `MainWindow.OnPreviewLoaded` (`_previewPopup.ThumbnailClicked = hwnd => OnPopupThumbnailClicked(hwnd);` once at creation). Do NOT call any registration method from MainWindow — `ShowFor` + the internal Loaded-priority post is the single path.
3. `DockProgramItemModel` is in `CedroModernDock.Core.Models` (already imported).

- [ ] **Step 4: Wire popup callbacks and row clicks**

In `WindowPreviewPopup`:
- Add the three callback properties.
- In `BuildRows`, attach `row.PointerPressed += (_, _) => ThumbnailClicked?.Invoke(_sources[i]);` — source handles are kept in `_sources` (parallel to `_rows`), filled by `RegisterThumbnails()`.
- Add `RootHost` `PointerEntered`/`PointerExited` events (attach to `this` window via `PointerEntered="..."` in XAML or code) invoking the callbacks.

- [ ] **Step 5: Build and test**

Run: `Get-Process CedroModernDock -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet build dotnet\src\CedroModernDock\CedroModernDock.csproj --nologo`
Expected: 0 errors.
Run: `dotnet test dotnet\tests\CedroModernDock.Tests\CedroModernDock.Tests.csproj --nologo`
Expected: 32 passing.

- [ ] **Step 6: Commit**

```bash
git add dotnet/src/CedroModernDock/Views/MainWindow.axaml dotnet/src/CedroModernDock/Views/MainWindow.axaml.cs dotnet/src/CedroModernDock/ViewModels/MainWindowViewModel.cs dotnet/src/CedroModernDock/Views/WindowPreviewPopup.axaml.cs
git commit -m "feat: hover window preview popup with live thumbnails"
```

---

## Task 6: End-to-end verification

- [ ] **Step 1: Start the dock and open some windows**

Launch the app: `dotnet\src\CedroModernDock\bin\Debug\net9.0-windows\CedroModernDock.exe`. Open 2+ windows of the same app (e.g., two Chrome windows or two VS Code windows).

- [ ] **Step 2: Hover a running item**

Hover the item's icon for ~500ms. Expected: popup appears below the dock (or above if docked at the bottom), centered on the icon, showing one row per window: live thumbnail + title. Moving the mouse into the popup keeps it open; moving away hides it after ~80ms.

- [ ] **Step 3: Click a thumbnail**

Expected: the corresponding window comes to the foreground (restored if minimized) and the popup closes.

- [ ] **Step 4: Verify live-ness**

With the popup open, change the source window (type in it / move it). Expected: the thumbnail updates live.

- [ ] **Step 5: Verify edge cases**

- Item with no open windows: no popup on hover (dot only).
- Settings / My Computer / Recycle Bin items: no popup.
- Dock near screen edge: popup stays within the working area.
- Open settings and change dock color/rounding → hover again: popup uses the new theme.

- [ ] **Step 6: Commit any leftover fixes**

```bash
git status --short
git add -A
git commit -m "fix: preview popup polish after manual verification"
```

(Only if there were fixes; otherwise no-op.)
