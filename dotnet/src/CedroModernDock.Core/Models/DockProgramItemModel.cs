namespace CedroModernDock.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Direct port of DockProgramItemModel. The serialized "path" property
/// holds the executable path; the ExecutablePath accessor is a non-serialized
/// convenience alias (matching the Java @JsonIgnore getExecutablePath).
///
/// Enhanced builds may also persist a Shell launch target/AppUserModelID. This
/// lets packaged Windows apps and installed web apps keep a stable launch
/// identity even when their backing executable is inaccessible, shared, or
/// changes location after an app update.
/// </summary>
public class DockProgramItemModel : DockItem
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";

    [JsonPropertyName("customIconPngBase64")]
    public string? CustomIconPngBase64 { get; set; }

    /// <summary>
    /// Optional shell namespace target, typically
    /// shell:AppsFolder\\&lt;AppUserModelID&gt;. When present it is preferred for
    /// launching while Path remains available for legacy executable matching.
    /// </summary>
    [JsonPropertyName("launchTarget")]
    public string? LaunchTarget { get; set; }

    /// <summary>Stable Windows application identity when one is available.</summary>
    [JsonPropertyName("appUserModelId")]
    public string? AppUserModelId { get; set; }

    /// <summary>
    /// Optional identity key for a window-specific cached icon. This is useful
    /// for apps such as Edge PWAs that share one executable but have distinct
    /// taskbar identities/icons.
    /// </summary>
    [JsonPropertyName("iconCacheKey")]
    public string? IconCacheKey { get; set; }

    [JsonIgnore]
    public string ExecutablePath => Path;

    [JsonIgnore]
    public DockItemType Type => DockItemType.PROGRAM;

    public DockProgramItemModel() { }

    public DockProgramItemModel(string label, string exePath)
    {
        Label = label;
        Path = exePath;
    }
}
