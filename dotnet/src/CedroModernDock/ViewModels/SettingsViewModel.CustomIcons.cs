using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Per-item custom icon override management. The selected image is normalized
/// to PNG, persisted inside config.json as Base64, and cached under AppData for
/// fast loading. This keeps the existing JSON export/import workflow portable.
/// </summary>
public partial class SettingsViewModel
{
    public bool CanCustomizeIcon
        => SelectedItemIndex >= 0 && SelectedItemIndex < _appServices.DockService.GetItems().Count;

    public bool CanResetCustomIcon
    {
        get
        {
            if (!CanCustomizeIcon) return false;
            DockItem item = _appServices.DockService.GetItems()[SelectedItemIndex];
            return !string.IsNullOrWhiteSpace(item.CustomIconPngBase64);
        }
    }

    public string CustomIconTitle => T("settings.icons.customIcon.title");
    public string CustomIconHelper => T("settings.icons.customIcon.helper");
    public string ChooseCustomIconText => T("settings.icons.customIcon.choose");
    public string ResetCustomIconText => T("settings.icons.customIcon.reset");
    public string CustomIconDialogTitle => T("settings.icons.customIcon.dialogTitle");
    public string CustomIconFileTypeText => T("settings.icons.customIcon.fileType");
    public string CustomIconFailedText => T("settings.icons.customIcon.failed");

    /// <summary>Called when the dock item selection changes.</summary>
    public void NotifyCustomIconSelectionChanged()
    {
        OnPropertyChanged(nameof(CanCustomizeIcon));
        OnPropertyChanged(nameof(CanResetCustomIcon));
    }

    /// <summary>
    /// Replaces the settings-list preview icon for every item that has an
    /// override. The main RefreshItemLabels method can stay focused on legacy
    /// automatic icons while this partial layer applies enhanced overrides.
    /// </summary>
    public void ApplyCustomIconPreviews()
    {
        var items = _appServices.DockService.GetItems();
        int count = Math.Min(items.Count, ItemEntries.Count);
        for (int i = 0; i < count; i++)
        {
            string? data = items[i].CustomIconPngBase64;
            if (string.IsNullOrWhiteSpace(data))
                continue;

            string? path = CustomIconStore.EnsureCached(data);
            Bitmap? icon = IconLoader.LoadFromFile(path);
            if (icon != null)
                ItemEntries[i] = new DockItemListEntry(ItemEntries[i].Label, icon);
        }
        NotifyCustomIconSelectionChanged();
    }

    public async Task ChooseCustomIconAsync(Window window)
    {
        if (!CanCustomizeIcon)
            return;

        try
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = CustomIconDialogTitle,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(CustomIconFileTypeText)
                    {
                        Patterns = CustomIconStore.SupportedPatterns
                    }
                }
            });

            if (files.Count == 0)
                return;

            string source = files[0].Path.LocalPath;
            string newData = CustomIconStore.ImportAsPngBase64(source);

            DockItem item = _appServices.DockService.GetItems()[SelectedItemIndex];
            string? oldData = item.CustomIconPngBase64;
            item.CustomIconPngBase64 = newData;
            _appServices.DockService.SaveChanges();

            if (!string.IsNullOrWhiteSpace(oldData) && oldData != newData)
                CustomIconStore.DeleteCached(oldData);

            string? cachedPath = CustomIconStore.EnsureCached(newData);
            Bitmap? icon = IconLoader.LoadFromFile(cachedPath);
            if (icon != null && SelectedItemIndex < ItemEntries.Count)
                ItemEntries[SelectedItemIndex] = new DockItemListEntry(
                    ItemEntries[SelectedItemIndex].Label, icon);

            NotifyCustomIconSelectionChanged();
            _dockRefreshAction();
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                string.Format(CustomIconFailedText, ex.Message),
                "Cedro Modern Dock",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    public void ResetCustomIcon()
    {
        if (!CanResetCustomIcon)
            return;

        int selectedIndex = SelectedItemIndex;
        DockItem item = _appServices.DockService.GetItems()[selectedIndex];
        string? oldData = item.CustomIconPngBase64;
        item.CustomIconPngBase64 = null;
        _appServices.DockService.SaveChanges();
        CustomIconStore.DeleteCached(oldData);

        RefreshItemLabels();
        if (selectedIndex >= 0 && selectedIndex < ItemEntries.Count)
            SelectedItemIndex = selectedIndex;
        ApplyCustomIconPreviews();
        _dockRefreshAction();
    }
}
