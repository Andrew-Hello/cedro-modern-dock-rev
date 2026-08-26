namespace CedroModernDock.Core.Models;

using System.Globalization;
using System.Text.Json.Serialization;

/// <summary>
/// Polymorphic dock-item interface. The JSON attributes replicate the Jackson
/// <c>@JsonTypeInfo</c>/<c>@JsonSubTypes</c> configuration so that config.json
/// stays <b>format-compatible</b> with the original Java application.
///
/// The <c>@type</c> discriminator property maps to the same subtype names:
/// programItem, folderItem, windowsModuleItem, settingsItem.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "@type")]
[JsonDerivedType(typeof(DockProgramItemModel), "programItem")]
[JsonDerivedType(typeof(DockFolderItemModel), "folderItem")]
[JsonDerivedType(typeof(DockWindowsModuleItemModel), "windowsModuleItem")]
[JsonDerivedType(typeof(DockSettingsItemModel), "settingsItem")]
public interface DockItem
{
    string Label { get; set; }
    string Path { get; set; }

    /// <summary>
    /// Optional user-selected icon override, stored as normalized PNG bytes in
    /// Base64. Keeping the bytes in config.json makes exported configurations
    /// self-contained and portable to another PC; the Windows UI materializes a
    /// cached copy under %APPDATA%\CedroModernDock\customIcons at runtime.
    /// </summary>
    [JsonPropertyName("customIconPngBase64")]
    string? CustomIconPngBase64 { get; set; }

    /// <summary>Not serialized — resolved from the concrete type.</summary>
    [JsonIgnore]
    DockItemType Type { get; }
}
