namespace CedroModernDock.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Direct port of DockFolderItemModel. The serialized "path" property
/// holds the folder path; FolderPath is a non-serialized convenience alias.
/// </summary>
public class DockFolderItemModel : DockItem
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";

    [JsonPropertyName("customIconPngBase64")]
    public string? CustomIconPngBase64 { get; set; }

    [JsonPropertyName("customSystemIconSource")]
    public string? CustomSystemIconSource { get; set; }

    [JsonPropertyName("customSystemIconIndex")]
    public int? CustomSystemIconIndex { get; set; }

    [JsonIgnore]
    public string FolderPath => Path;

    [JsonIgnore]
    public DockItemType Type => DockItemType.FOLDER;

    public DockFolderItemModel() { }

    public DockFolderItemModel(string label, string folderPath)
    {
        Label = label;
        Path = folderPath;
    }
}
