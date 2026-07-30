namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

/// <summary>Direct port of DockItemActionService.</summary>
public class DockItemActionService
{
    private readonly IFolderLauncher _folderLauncher;
    private readonly IProgramLauncher _programLauncher;
    private readonly IWindowsModuleLauncher _windowsModuleLauncher;

    public DockItemActionService(
        IProgramLauncher programLauncher,
        IFolderLauncher folderLauncher,
        IWindowsModuleLauncher windowsModuleLauncher)
    {
        _programLauncher = programLauncher;
        _folderLauncher = folderLauncher;
        _windowsModuleLauncher = windowsModuleLauncher;
    }

    public void Execute(DockItem item, Action openSettingsAction)
    {
        if (item is DockProgramItemModel programItem)
        {
            _programLauncher.Launch(programItem.ExecutablePath, programItem.Label);
            return;
        }

        if (item is DockFolderItemModel folderItem)
        {
            _folderLauncher.Launch(folderItem.FolderPath, folderItem.Label);
            return;
        }

        if (item is DockWindowsModuleItemModel windowsModuleItem)
        {
            _windowsModuleLauncher.Launch(windowsModuleItem.Module, windowsModuleItem.Label);
            return;
        }

        if (item is DockSettingsItemModel)
        {
            openSettingsAction();
        }
    }
}
