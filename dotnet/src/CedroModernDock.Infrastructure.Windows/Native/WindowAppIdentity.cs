using System.Runtime.InteropServices;
using System.Text;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Best-effort resolver for the Windows Application User Model ID (AUMID).
/// Packaged/UWP apps expose it through GetApplicationUserModelId, while many
/// desktop apps that need distinct taskbar identities (including installed
/// Chromium/Edge web apps) set PKEY_AppUserModel_ID on their top-level window.
/// </summary>
internal static class WindowAppIdentity
{
    private const ushort VT_LPWSTR = 31;
    private static readonly Guid IidPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PROPERTYKEY PkeyAppUserModelId = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    public static string? TryGetAppUserModelId(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        // Explicit window property comes first: desktop apps/PWAs can use it to
        // distinguish multiple taskbar identities that share one executable.
        string? explicitId = TryGetExplicitWindowAppUserModelId(hwnd);
        if (!string.IsNullOrWhiteSpace(explicitId))
            return explicitId;

        return TryGetProcessAppUserModelId(hwnd);
    }

    public static string? TryGetProcessAppUserModelId(IntPtr hwnd)
    {
        User32.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return null;

        IntPtr process = Kernel32.OpenProcess(
            Win32Constants.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero)
            return null;

        try
        {
            uint length = 1024;
            var buffer = new StringBuilder((int)length);
            int result = GetApplicationUserModelId(process, ref length, buffer);
            if (result != 0)
                return null;
            string value = buffer.ToString().Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }

    private static string? TryGetExplicitWindowAppUserModelId(IntPtr hwnd)
    {
        IPropertyStore? store = null;
        PROPVARIANT value = default;
        try
        {
            Guid iid = IidPropertyStore;
            int hr = SHGetPropertyStoreForWindow(hwnd, ref iid, out store!);
            if (hr != 0 || store == null)
                return null;

            PROPERTYKEY key = PkeyAppUserModelId;
            hr = store.GetValue(ref key, out value);
            if (hr != 0 || value.vt != VT_LPWSTR || value.pointerValue == IntPtr.Zero)
                return null;

            string? text = Marshal.PtrToStringUni(value.pointerValue)?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
        finally
        {
            try { PropVariantClear(ref value); } catch { }
            if (store != null)
            {
                try { Marshal.FinalReleaseComObject(store); } catch { }
            }
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(
        IntPtr hProcess, ref uint applicationUserModelIdLength,
        [Out] StringBuilder applicationUserModelId);

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }
}
