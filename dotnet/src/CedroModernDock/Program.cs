using Avalonia;
using System;
using CedroModernDock.Core.Application;
using CedroModernDock.Infrastructure.Windows.Adapters;
using CedroModernDock.Infrastructure.Windows.Persistence;

namespace CedroModernDock;

sealed class Program
{
    private static SingleInstanceGuard? _singleInstanceGuard;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Single-instance guard — prevents multiple dock instances.
        // Port of App.java's SingleInstanceGuard + localized warning dialog.
        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.TryAcquire())
        {
            var language = new JsonDockRepository().Load().Language;
            string message = LocalizationService.BootstrapText(language, "dialog.singleInstance.message");
            System.Windows.Forms.MessageBox.Show(
                message,
                "Cedro Modern Dock",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _singleInstanceGuard?.Dispose();
            _singleInstanceGuard = null;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
