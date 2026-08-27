using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Adds explicit support for pinning Windows script launchers. These are stored
/// as ordinary program items so ordering, custom icon overrides and config
/// export/import continue to work without a separate model type.
/// </summary>
public partial class SettingsViewModel
{
    public string AddScriptText => T("settings.icons.addScript");
    public string ScriptDialogTitle => T("dialog.fileChooser.scriptTitle");
    public string ScriptFileTypeText => T("dialog.fileChooser.scriptFilter");

    public async Task AddScriptAsync(Window window)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = ScriptDialogTitle,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(ScriptFileTypeText)
                {
                    Patterns = new[] { "*.bat", "*.cmd", "*.vbs" }
                }
            }
        });

        if (files.Count == 0)
            return;

        string path = files[0].Path.LocalPath;
        if (!IsSupportedScript(path))
            return;

        var selection = ProgramSelectionResolver.Resolve(path);

        // The Windows icon gateway uses the shell-associated file-type icon for
        // scripts, so .bat/.cmd/.vbs entries are never left visually blank.
        _appServices.IconGateway.CacheProgramIcon(selection.ExecutablePath);
        _appServices.DockService.AddItem(
            new DockProgramItemModel(selection.Label, selection.ExecutablePath));

        RefreshItemLabels();
        _dockRefreshAction();
    }

    private static bool IsSupportedScript(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".vbs", StringComparison.OrdinalIgnoreCase);
    }
}
