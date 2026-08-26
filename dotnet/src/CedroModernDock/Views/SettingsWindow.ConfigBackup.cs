using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CedroModernDock.Infrastructure.Windows.Persistence;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow
{
    private bool _configBackupPanelInstalled;

    private void InstallConfigBackupSettingsPanel()
    {
        if (_configBackupPanelInstalled || DataContext is not SettingsViewModel vm)
            return;

        TabControl? tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (tabs == null || tabs.Items.Count < 5 || tabs.Items[4] is not TabItem generalTab ||
            generalTab.Content is not StackPanel generalPanel)
            return;

        _configBackupPanelInstalled = true;

        var heading = new TextBlock
        {
            Text = vm.ConfigBackupTitle,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 14, 0, 2)
        };

        var helper = new TextBlock
        {
            Text = vm.ConfigBackupHelper,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 2, 0, 8)
        };

        var openFolder = new Button { Content = vm.OpenBackupFolderText };
        openFolder.Click += (_, _) => OpenBackupFolder(vm);

        var exportConfig = new Button { Content = vm.ExportConfigText };
        exportConfig.Click += async (_, _) => await ExportConfigAsync(vm);

        var importConfig = new Button { Content = vm.ImportConfigText };
        importConfig.Click += async (_, _) => await ImportConfigAsync(vm);

        buttons.Children.Add(openFolder);
        buttons.Children.Add(exportConfig);
        buttons.Children.Add(importConfig);

        // The original General page always ends with four static metadata rows:
        // Acknowledgements, version, repository and contact. Insert the backup
        // section immediately before those rows regardless of how many enhanced
        // behavior/interaction controls were added above it.
        int insertAt = Math.Max(0, generalPanel.Children.Count - 4);
        generalPanel.Children.Insert(insertAt++, heading);
        generalPanel.Children.Insert(insertAt++, helper);
        generalPanel.Children.Insert(insertAt, buttons);
    }

    private void OpenBackupFolder(SettingsViewModel vm)
    {
        try
        {
            string folder = JsonDockRepository.BackupDirectoryPath;
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowConfigMessage(string.Format(vm.ExportFailedText, ex.Message), isError: true);
        }
    }

    private async System.Threading.Tasks.Task ExportConfigAsync(SettingsViewModel vm)
    {
        try
        {
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = vm.ExportDialogTitle,
                SuggestedFileName = $"CedroModernDock-Backup-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(vm.ConfigFileTypeText)
                    {
                        Patterns = new[] { "*.json" }
                    }
                }
            });

            if (file == null)
                return;

            string destination = file.Path.LocalPath;
            JsonDockRepository.ExportCurrentConfig(destination);
            ShowConfigMessage(string.Format(vm.ExportSuccessText, destination), isError: false);
        }
        catch (Exception ex)
        {
            ShowConfigMessage(string.Format(vm.ExportFailedText, ex.Message), isError: true);
        }
    }

    private async System.Threading.Tasks.Task ImportConfigAsync(SettingsViewModel vm)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = vm.ImportDialogTitle,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(vm.ConfigFileTypeText)
                    {
                        Patterns = new[] { "*.json" }
                    }
                }
            });

            if (files.Count == 0)
                return;

            string source = files[0].Path.LocalPath;
            if (!JsonDockRepository.TryImportConfig(source, out string? error))
            {
                ShowConfigMessage(string.Format(vm.ImportFailedText, error ?? "Unknown error"), isError: true);
                return;
            }

            ShowConfigMessage(vm.ImportSuccessRestartText, isError: false);
            RestartAfterConfigImport(vm);
        }
        catch (Exception ex)
        {
            ShowConfigMessage(string.Format(vm.ImportFailedText, ex.Message), isError: true);
        }
    }

    private void RestartAfterConfigImport(SettingsViewModel vm)
    {
        try
        {
            string? executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
                throw new InvalidOperationException("Unable to determine the current executable path.");

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true
            };

            if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                string? entryAssembly = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrWhiteSpace(entryAssembly))
                    throw new InvalidOperationException("Unable to determine the Cedro assembly path.");
                startInfo.ArgumentList.Add(entryAssembly);
            }

            startInfo.ArgumentList.Add("--restart-after-import");
            Process.Start(startInfo);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
            else
                Close();
        }
        catch (Exception ex)
        {
            ShowConfigMessage(string.Format(vm.ImportFailedText, ex.Message), isError: true);
        }
    }

    private static void ShowConfigMessage(string message, bool isError)
    {
        System.Windows.Forms.MessageBox.Show(
            message,
            "Cedro Modern Dock",
            System.Windows.Forms.MessageBoxButtons.OK,
            isError
                ? System.Windows.Forms.MessageBoxIcon.Error
                : System.Windows.Forms.MessageBoxIcon.Information);
    }
}
