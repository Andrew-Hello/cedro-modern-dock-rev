using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using CedroModernDock.Core.Application;
using CedroModernDock.Infrastructure.Windows.Adapters;
using CedroModernDock.Infrastructure.Windows.Persistence;
using CedroModernDock.ViewModels;
using CedroModernDock.Views;

namespace CedroModernDock;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Composition root — wire concrete Windows adapters into the application services.
            // Direct port of App.java's createServices() method.
            var appServices = CreateServices();
            var mainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
            mainWindow.SetAppServices(appServices);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Composes all application services with their Windows-specific adapters.
    /// Direct port of App.java createServices().
    /// </summary>
    private static AppServices CreateServices()
    {
        var repository = new JsonDockRepository();
        var dockService = new DockService(repository);
        var screenBoundsProvider = new WindowsScreenBoundsProvider();

        return new AppServices(
            DockService: dockService,
            AppearanceService: new DockAppearanceService(dockService),
            PositioningService: new DockPositioningService(dockService, screenBoundsProvider),
            ItemActionService: new DockItemActionService(
                new WindowsProgramLauncher(),
                new WindowsFolderLauncher(),
                new WindowsModuleLauncher()
            ),
            WindowPreviewService: new WindowPreviewService(new Win32WindowQueryGateway()),
            IconGateway: new CachedWindowsIconGateway(),
            LocalizationService: new LocalizationService(dockService)
        );
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}