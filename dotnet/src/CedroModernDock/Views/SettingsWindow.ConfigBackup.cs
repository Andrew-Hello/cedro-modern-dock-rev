using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CedroModernDock.Infrastructure.Windows.Persistence;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow
{
    private void OnOpenBackupFolder(object? sender, RoutedEventArgs e)
        => OpenBackupFolder(Vm);

    private async void OnExportConfig(object? sender, RoutedEventArgs e)
        => await ExportConfigAsync(Vm);

    private async void OnImportConfig(object? sender, RoutedEventArgs e)
        => await ImportConfigAsync(Vm);

    private void OpenBackupFolder(SettingsViewModel vm)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = JsonDockRepository.BackupDirectoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowConfigMessage(string.Format(vm.ExportFailedText, ex.Message), true);
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
            ShowConfigMessage(string.Format(vm.ExportSuccessText, destination), false);
        }
        catch (Exception ex)
        {
            ShowConfigMessage(string.Format(vm.ExportFailedText, ex.Message), true);
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
                ShowConfigMessage(string.Format(vm.ImportFailedText, error ?? "Unknown error"), true);
                return;
            }

            ShowConfigMessage(vm.ImportSuccessRestartText, false);
            RestartAfterConfigImport(vm);
        }
        catch (Exception ex)
        {
            ShowConfigMessage(string.Format(vm.ImportFailedText, ex.Message), true);
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
            ShowConfigMessage(string.Format(vm.ImportFailedText, ex.Message), true);
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
