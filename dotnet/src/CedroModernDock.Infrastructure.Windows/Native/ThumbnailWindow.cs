using System;
using System.Runtime.InteropServices;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// A small borderless, click-through top-level window that hosts a single
/// DWM thumbnail. DWM thumbnails composite above the host window's own content
/// and ignore window regions, so rounded corners are achieved with
/// DWMWA_WINDOW_CORNER_PREFERENCE (Win11 DWM corner rounding), which clips the
/// thumbnail at the composition level.
/// </summary>
public sealed class ThumbnailWindow : IDisposable
{
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;
    private const string ClassName = "CedroDockThumbnailWindow";

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;

    private static readonly IntPtr _hInstance = Kernel32.GetModuleHandle(null);

    private static readonly WndProc WndProcImpl = (hwnd, msg, wParam, lParam) =>
    {
        if (msg == WM_NCHITTEST)
            return new IntPtr(HTTRANSPARENT);
        return User32.DefWindowProc(hwnd, msg, wParam, lParam);
    };

    private static readonly nint WndProcPtr = Marshal.GetFunctionPointerForDelegate(WndProcImpl);

    private readonly IntPtr _hwnd;
    private IntPtr _thumb;
    private int _x, _y, _width, _height;

    public IntPtr Handle => _hwnd;

    /// <summary>Current screen rect in physical pixels (updated by ctor and Move).</summary>
    public (int X, int Y, int Width, int Height) ScreenRect => (_x, _y, _width, _height);

    /// <summary>True when the physical screen point is inside this window's rect.</summary>
    public bool ContainsPoint(int x, int y) =>
        x >= _x && x < _x + _width && y >= _y && y < _y + _height;

    static ThumbnailWindow()
    {
        var wc = new WNDCLASS
        {
            lpfnWndProc = WndProcPtr,
            hInstance = _hInstance,
            lpszClassName = ClassName,
            hbrBackground = IntPtr.Zero
        };
        User32.RegisterClass(wc);
    }

    /// <summary>
    /// Creates a rounded, click-through thumbnail window for <paramref name="sourceHwnd"/>.
    /// <paramref name="borderColor"/> is a COLORREF (0x00BBGGRR) used for the 1px DWM
    /// border drawn around rounded windows; pass the popup row background color so the
    /// ring blends into it.
    /// </summary>
    public ThumbnailWindow(IntPtr sourceHwnd, int x, int y, int width, int height, uint borderColor)
    {
        _x = x; _y = y; _width = width; _height = height;
        _hwnd = User32.CreateWindowEx(
            (uint)(Win32Constants.WS_EX_TOOLWINDOW | Win32Constants.WS_EX_NOACTIVATE),
            ClassName, "",
            (uint)(Win32Constants.WS_POPUP | Win32Constants.WS_VISIBLE),
            x, y, width, height, IntPtr.Zero, IntPtr.Zero, _hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create thumbnail window");

        int corner = DWMWCP_ROUND;
        DwmThumbnailInterop.SetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, corner);
        DwmThumbnailInterop.SetWindowAttribute(_hwnd, DWMWA_BORDER_COLOR, (int)borderColor);

        if (!DwmThumbnailInterop.Register(_hwnd, sourceHwnd, out _thumb))
            throw new InvalidOperationException("Failed to register DWM thumbnail");

        var rect = new DwmThumbnailInterop.RECT { Left = 0, Top = 0, Right = width, Bottom = height };
        DwmThumbnailInterop.Update(_thumb, rect);

        User32.SetWindowPos(_hwnd, Win32Constants.HWND_TOPMOST, x, y, width, height, 0);
    }

    public void Move(int x, int y)
    {
        _x = x; _y = y;
        User32.SetWindowPos(_hwnd, Win32Constants.HWND_TOPMOST, x, y, 0, 0,
            Win32Constants.SWP_NOSIZE | Win32Constants.SWP_NOACTIVATE);
    }

    public void Dispose()
    {
        if (_thumb != IntPtr.Zero)
        {
            DwmThumbnailInterop.Unregister(_thumb);
            _thumb = IntPtr.Zero;
        }
        if (_hwnd != IntPtr.Zero)
            User32.DestroyWindow(_hwnd);
    }
}
