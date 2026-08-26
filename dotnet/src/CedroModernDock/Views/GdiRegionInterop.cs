namespace CedroModernDock.Views;

using System;
using System.Runtime.InteropServices;

/// <summary>Local GDI cleanup used only when SetWindowRgn fails to take ownership.</summary>
internal static class Gdi32
{
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteObject(IntPtr hObject);
}
