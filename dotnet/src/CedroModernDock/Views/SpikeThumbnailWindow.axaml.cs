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

            var matrix = ThumbArea.TransformToVisual(this);
            if (matrix == null) { Close(); return; }
            var area = ThumbArea.Bounds.TransformToAABB(matrix.Value);

            double scale = RenderScaling;
            var rect = new DwmThumbnailInterop.RECT
            {
                Left = (int)(area.X * scale),
                Top = (int)(area.Y * scale),
                Right = (int)((area.X + area.Width) * scale),
                Bottom = (int)((area.Y + area.Height) * scale)
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
