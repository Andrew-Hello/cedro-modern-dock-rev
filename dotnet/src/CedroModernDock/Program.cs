using Avalonia;
using System;
using System.IO;
using CedroModernDock.Core.Application;
using CedroModernDock.Infrastructure.Windows.Adapters;
using CedroModernDock.Infrastructure.Windows.Persistence;

namespace CedroModernDock;

sealed class Program
{
    private const string RestartAfterImportArgument = "--restart-after-import";
    private static SingleInstanceGuard? _singleInstanceGuard;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // The app is a WinExe (no console), so runtime failures are otherwise
        // silent. Log unhandled exceptions for diagnostics.
        CrashLogger.Hook();

        bool restartingAfterImport = args.Any(a =>
            string.Equals(a, RestartAfterImportArgument, StringComparison.OrdinalIgnoreCase));

        // During an import-triggered restart the replacement process is launched
        // just before the old process exits. Wait/retry the named mutex rather
        // than showing a false "already running" warning and abandoning restart.
        bool acquired = restartingAfterImport
            ? TryAcquireSingleInstanceWithRetry(TimeSpan.FromSeconds(6))
            : TryAcquireSingleInstanceOnce();

        if (!acquired)
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
            // Internal restart coordination arguments are not relevant to
            // Avalonia's desktop lifetime.
            string[] appArgs = args
                .Where(a => !string.Equals(a, RestartAfterImportArgument, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(appArgs);
        }
        finally
        {
            _singleInstanceGuard?.Dispose();
            _singleInstanceGuard = null;
        }
    }

    private static bool TryAcquireSingleInstanceOnce()
    {
        _singleInstanceGuard = new SingleInstanceGuard();
        if (_singleInstanceGuard.TryAcquire())
            return true;

        _singleInstanceGuard.Dispose();
        _singleInstanceGuard = null;
        return false;
    }

    private static bool TryAcquireSingleInstanceWithRetry(TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        do
        {
            if (TryAcquireSingleInstanceOnce())
                return true;
            System.Threading.Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

internal static class CrashLogger
{
    private static readonly object Sync = new();
    private static readonly string Path =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CedroModernDock", "crash-log.txt");

    public static void Hook()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Log($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
    }

    public static void Log(string message)
    {
        try
        {
            lock (Sync)
                File.AppendAllText(Path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
