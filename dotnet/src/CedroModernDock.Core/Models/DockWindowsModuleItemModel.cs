namespace CedroModernDock.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Direct port of DockWindowsModuleItemModel. The "module" field identifies
/// which built-in Windows surface this item opens (mypc, trash, ctrlpnl, pconfig).
/// The icon path is derived from the module in the constructor.
/// </summary>
public class DockWindowsModuleItemModel : DockItem
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";

    [JsonPropertyName("customIconPngBase64")]
    public string? CustomIconPngBase64 { get; set; }

    [JsonPropertyName("customSystemIconSource")]
    public string? CustomSystemIconSource { get; set; }

    [JsonPropertyName("customSystemIconIndex")]
    public int? CustomSystemIconIndex { get; set; }

    public string Module { get; set; } = "";

    [JsonIgnore]
    public DockItemType Type => DockItemType.WINDOWS_MODULE;

    public DockWindowsModuleItemModel() { }

    public DockWindowsModuleItemModel(string label, string module)
    {
        Path = module switch
        {
            "mypc" => "/com/github/arthurdeka/cedromoderndock/icons/my_computer.png",
            "trash" => "/com/github/arthurdeka/cedromoderndock/icons/trash.png",
            "ctrlpnl" => "/com/github/arthurdeka/cedromoderndock/icons/control.png",
            "pconfig" => "/com/github/arthurdeka/cedromoderndock/icons/windows_settings.png",
            _ => ""
        };

        Label = label;
        Module = module;
    }
}
