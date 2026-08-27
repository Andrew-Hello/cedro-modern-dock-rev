using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Per-item custom icon override management. Imported image/ICO files continue
/// to be normalized into portable PNG/Base64 data, while Windows system-library
/// selections are persisted compactly as resource Source + Icon Index and are
/// dynamically extracted at runtime.
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
            return CustomDockIconResolver.HasOverride(item);
        }
    }

    public string CustomIconTitle => T("settings.icons.customIcon.title");
    public string CustomIconHelper => T("settings.icons.customIcon.helper");
    public string ChooseCustomIconText => T("settings.icons.customIcon.choose");
    public string ChooseSystemIconText => T("settings.icons.customIcon.chooseSystem");
    public string ResetCustomIconText => T("settings.icons.customIcon.reset");
    public string CustomIconDialogTitle => T("settings.icons.customIcon.dialogTitle");
    public string CustomIconFileTypeText => T("settings.icons.customIcon.fileType");
    public string CustomIconFailedText => T("settings.icons.customIcon.failed");
    public string SystemIconPickerTitle => T("settings.icons.customIcon.systemPicker.title");
    public string SystemIconPickerSubtitle => T("settings.icons.customIcon.systemPicker.subtitle");
    public string SystemIconPickerLoading => T("settings.icons.customIcon.systemPicker.loading");
    public string SystemIconPickerLoaded => T("settings.icons.customIcon.systemPicker.loaded");
    public string SystemIconPickerFailed => T("settings.icons.customIcon.systemPicker.failed");
    public string SystemIconPickerCancel => T("settings.icons.customIcon.systemPicker.cancel");
    public string SystemIconPickerLibraryLabel => T("settings.icons.customIcon.systemPicker.libraryLabel");
    public string SystemIconPickerNoLibraries => T("settings.icons.customIcon.systemPicker.noLibraries");

    public string SystemIconCategoryName(string category) => category switch
    {
        "common" => T("settings.icons.customIcon.systemPicker.category.common"),
        "devices" => T("settings.icons.customIcon.systemPicker.category.devices"),
        "network" => T("settings.icons.customIcon.systemPicker.category.network"),
        "classic" => T("settings.icons.customIcon.systemPicker.category.classic"),
        _ => T("settings.icons.customIcon.systemPicker.category.other")
    };

    /// <summary>Called when the dock item selection changes.</summary>
    public void NotifyCustomIconSelectionChanged()
    {
        OnPropertyChanged(nameof(CanCustomizeIcon));
        OnPropertyChanged(nameof(CanResetCustomIcon));
    }

    /// <summary>Applies either type of custom icon to rows in the settings list.</summary>
    public void ApplyCustomIconPreviews()
    {
        var items = _appServices.DockService.GetItems();
        int count = Math.Min(items.Count, ItemEntries.Count);
        for (int i = 0; i < count; i++)
        {
            if (!CustomDockIconResolver.HasOverride(items[i]))
                continue;

            Bitmap? icon = CustomDockIconResolver.Resolve(items[i], 128);
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
            ApplyCustomImageOverride(newData);
        }
        catch (Exception ex)
        {
            ShowCustomIconError(ex.Message);
        }
    }

    /// <summary>Applies a user-imported image/ICO override and clears any system reference.</summary>
    public void ApplyCustomImageOverride(string newData)
    {
        if (!CanCustomizeIcon || string.IsNullOrWhiteSpace(newData))
            return;

        try
        {
            DockItem item = _appServices.DockService.GetItems()[SelectedItemIndex];
            string? oldData = item.CustomIconPngBase64;

            item.CustomIconPngBase64 = newData;
            item.CustomSystemIconSource = null;
            item.CustomSystemIconIndex = null;
            _appServices.DockService.SaveChanges();

            if (!string.IsNullOrWhiteSpace(oldData) && oldData != newData)
                CustomIconStore.DeleteCached(oldData);

            UpdateSelectedCustomIconPreview(item);
            NotifyCustomIconSelectionChanged();
            _dockRefreshAction();
        }
        catch (Exception ex)
        {
            ShowCustomIconError(ex.Message);
        }
    }

    /// <summary>
    /// Applies a Windows library reference directly. No Microsoft icon bytes are
    /// stored in config.json or copied into Cedro's persistent custom-icon cache.
    /// </summary>
    public void ApplySystemIconOverride(SystemIconSelection selection)
    {
        if (!CanCustomizeIcon || selection.IconIndex < 0 ||
            string.IsNullOrWhiteSpace(selection.SourceExpression))
            return;

        try
        {
            DockItem item = _appServices.DockService.GetItems()[SelectedItemIndex];
            string? oldData = item.CustomIconPngBase64;

            item.CustomIconPngBase64 = null;
            item.CustomSystemIconSource = selection.SourceExpression;
            item.CustomSystemIconIndex = selection.IconIndex;
            _appServices.DockService.SaveChanges();

            if (!string.IsNullOrWhiteSpace(oldData))
                CustomIconStore.DeleteCached(oldData);

            UpdateSelectedCustomIconPreview(item);
            NotifyCustomIconSelectionChanged();
            _dockRefreshAction();
        }
        catch (Exception ex)
        {
            ShowCustomIconError(ex.Message);
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
        item.CustomSystemIconSource = null;
        item.CustomSystemIconIndex = null;
        _appServices.DockService.SaveChanges();
        CustomIconStore.DeleteCached(oldData);

        RefreshItemLabels();
        if (selectedIndex >= 0 && selectedIndex < ItemEntries.Count)
            SelectedItemIndex = selectedIndex;
        ApplyCustomIconPreviews();
        _dockRefreshAction();
    }

    private void UpdateSelectedCustomIconPreview(DockItem item)
    {
        Bitmap? icon = CustomDockIconResolver.Resolve(item, 128);
        if (icon != null && SelectedItemIndex >= 0 && SelectedItemIndex < ItemEntries.Count)
            ItemEntries[SelectedItemIndex] = new DockItemListEntry(
                ItemEntries[SelectedItemIndex].Label, icon);
    }

    private void ShowCustomIconError(string details)
    {
        System.Windows.Forms.MessageBox.Show(
            string.Format(CustomIconFailedText, details),
            "Cedro Modern Dock",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);
    }
}
