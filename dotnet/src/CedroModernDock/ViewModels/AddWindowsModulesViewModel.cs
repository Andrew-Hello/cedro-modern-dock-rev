using System.Collections.ObjectModel;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;

namespace CedroModernDock.ViewModels;

/// <summary>ViewModel for the Add Windows Modules modal. Port of AddWindowsModulesModalController.</summary>
public class AddWindowsModulesViewModel : ViewModelBase
{
    private static readonly string[] ModuleIds = { "mypc", "trash", "ctrlpnl", "pconfig" };

    private readonly AppServices _appServices;
    private readonly Action _dockRefreshAction;
    private int _selectedIndex = -1;

    public ObservableCollection<string> ModuleNames { get; } = new();

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public string Title => _appServices.LocalizationService.Text("windowsModule.modal.title");
    public string Subtitle => _appServices.LocalizationService.Text("windowsModule.modal.subtitle");
    public string AvailableTitle => _appServices.LocalizationService.Text("windowsModule.modal.available.title");
    public string AvailableHelper => _appServices.LocalizationService.Text("windowsModule.modal.available.helper");
    public string AddButtonText => _appServices.LocalizationService.Text("windowsModule.modal.addSelected");

    public AddWindowsModulesViewModel(AppServices appServices, Action dockRefreshAction)
    {
        _appServices = appServices;
        _dockRefreshAction = dockRefreshAction;
        RefreshModuleNames();
    }

    public void AddSelectedModule()
    {
        if (SelectedIndex < 0 || SelectedIndex >= ModuleIds.Length)
            return;

        string moduleId = ModuleIds[SelectedIndex];
        string defaultLabel = moduleId switch
        {
            "mypc" => "My Computer",
            "trash" => "Recycle Bin",
            "ctrlpnl" => "Control Panel",
            "pconfig" => "Settings",
            _ => moduleId
        };

        _appServices.DockService.AddItem(new DockWindowsModuleItemModel(defaultLabel, moduleId));
        _dockRefreshAction();
    }

    public void RefreshModuleNames()
    {
        var loc = _appServices.LocalizationService;
        ModuleNames.Clear();
        foreach (var id in ModuleIds)
        {
            ModuleNames.Add(id switch
            {
                "mypc" => loc.Text("windowsModule.myComputer"),
                "trash" => loc.Text("windowsModule.recycleBin"),
                "ctrlpnl" => loc.Text("windowsModule.controlPanel"),
                "pconfig" => loc.Text("windowsModule.settings"),
                _ => id
            });
        }
    }
}
