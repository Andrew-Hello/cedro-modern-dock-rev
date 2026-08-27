namespace CedroModernDock.Core.Models;

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
    /// Optional user-imported image/icon override, stored as normalized PNG
    /// bytes in Base64. This remains the portable representation for PNG/ICO/
    /// JPG/etc. files selected by the user.
    /// </summary>
    [JsonPropertyName("customIconPngBase64")]
    string? CustomIconPngBase64 { get; set; }

    /// <summary>
    /// Optional Windows resource-library expression such as
    /// %SystemRoot%\System32\SHELL32.dll. System-library overrides intentionally
    /// store source + ordinal instead of copying Microsoft icon bytes into the
    /// configuration file.
    /// </summary>
    [JsonPropertyName("customSystemIconSource")]
    string? CustomSystemIconSource { get; set; }

    /// <summary>Zero-based icon ordinal within <see cref="CustomSystemIconSource"/>.</summary>
    [JsonPropertyName("customSystemIconIndex")]
    int? CustomSystemIconIndex { get; set; }

    /// <summary>Not serialized — resolved from the concrete type.</summary>
    [JsonIgnore]
    DockItemType Type { get; }
}
