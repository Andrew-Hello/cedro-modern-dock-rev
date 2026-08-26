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

    /// <summary>
    /// Executes the dock item action. Packaged apps/PWAs prefer their stable
    /// Shell launch target and fall back to the executable path if necessary.
    /// </summary>
    public bool Execute(DockItem item, Action openSettingsAction)
    {
        if (item is DockProgramItemModel programItem)
        {
            if (!string.IsNullOrWhiteSpace(programItem.LaunchTarget) &&
                _programLauncher.Launch(programItem.LaunchTarget, programItem.Label))
                return true;

            return _programLauncher.Launch(programItem.ExecutablePath, programItem.Label);
        }

        if (item is DockFolderItemModel folderItem)
        {
            _folderLauncher.Launch(folderItem.FolderPath, folderItem.Label);
            return true;
        }

        if (item is DockWindowsModuleItemModel windowsModuleItem)
        {
            _windowsModuleLauncher.Launch(windowsModuleItem.Module, windowsModuleItem.Label);
            return true;
        }

        if (item is DockSettingsItemModel)
        {
            openSettingsAction();
        }

        return true;
    }
}
