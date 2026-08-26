namespace CedroModernDock.Core.Models;

using System.Text.Json.Serialization;
using CedroModernDock.Core.Application;

/// <summary>
/// Direct port of DockModel. Holds the full dock configuration: items,
/// appearance settings, positioning, and language.
///
/// JSON property names use camelCase to match the original Java/Jackson
/// config.json format exactly, ensuring existing user configs load as-is.
/// </summary>
public class DockModel
{
    [JsonPropertyName("items")]
    public List<DockItem> Items { get; set; } = new();

    private int _iconsSize = 24;
    [JsonPropertyName("iconsSize")]
    public int IconsSize
    {
        get => _iconsSize;
        set => _iconsSize = value;
    }

    private int _spacingBetweenIcons = 0;
    [JsonPropertyName("spacingBetweenIcons")]
    public int SpacingBetweenIcons
    {
        get => _spacingBetweenIcons;
        set => _spacingBetweenIcons = value;
    }

    [JsonPropertyName("dockVerticalPadding")]
    public int DockVerticalPadding { get; set; } = 4;

    /// <summary>
    /// User preference for keeping the dock in the topmost window band.
    /// Defaults to true to preserve the enhanced dock behavior for existing users.
    /// Runtime fullscreen detection may temporarily suppress topmost without
    /// changing this persisted preference.
    /// </summary>
    [JsonPropertyName("alwaysOnTop")]
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>
    /// Legacy single edge-auto-hide switch. Kept for backward-compatible JSON
    /// loading; new builds expose independent horizontal (top/bottom) and
    /// vertical (left/right) switches below.
    /// </summary>
    [JsonPropertyName("autoHideAtScreenEdge")]
    public bool AutoHideAtScreenEdge { get; set; }

    /// <summary>
    /// Enables top/bottom edge docking. Nullable so an older config that has
    /// only autoHideAtScreenEdge can transparently migrate at runtime.
    /// </summary>
    [JsonPropertyName("autoHideAtHorizontalEdges")]
    public bool? AutoHideAtHorizontalEdges { get; set; }

    /// <summary>
    /// Enables left/right edge docking. Nullable so an older config that has
    /// only autoHideAtScreenEdge can transparently migrate at runtime.
    /// </summary>
    [JsonPropertyName("autoHideAtVerticalEdges")]
    public bool? AutoHideAtVerticalEdges { get; set; }

    /// <summary>
    /// Dynamic-mode edge docking state. Side encoding: 0=None, 1=Top,
    /// 2=Bottom, 3=Left, 4=Right. The offset is the visible dock's along-edge
    /// distance in pixels from the monitor's top/left origin.
    /// </summary>
    [JsonPropertyName("dynamicEdgeDocked")]
    public bool DynamicEdgeDocked { get; set; }

    [JsonPropertyName("dynamicEdgeSide")]
    public int DynamicEdgeSide { get; set; }

    [JsonPropertyName("dynamicEdgeOffset")]
    public int DynamicEdgeOffset { get; set; }

    private double _dockTransparency = 0.3;
    [JsonPropertyName("dockTransparency")]
    public double DockTransparency
    {
        get => _dockTransparency;
        set => _dockTransparency = value;
    }

    [JsonPropertyName("dockBorderRounding")]
    public int DockBorderRounding { get; set; } = 10;

    [JsonPropertyName("dockColorRGB")]
    public string DockColorRGB { get; set; } = "0, 0, 0, ";

    [JsonPropertyName("dockPositionX")]
    [JsonInclude]
    public double DockPositionX { get; private set; }

    [JsonPropertyName("dockPositionY")]
    [JsonInclude]
    public double DockPositionY { get; private set; }

    [JsonPropertyName("positioningMode")]
    public DockPositioningMode PositioningMode { get; set; } = DockPositioningMode.STATIC;

    [JsonPropertyName("verticalAnchor")]
    public DockVerticalAnchor VerticalAnchor { get; set; } = DockVerticalAnchor.TOP;

    [JsonPropertyName("horizontalAnchor")]
    public DockHorizontalAnchor HorizontalAnchor { get; set; } = DockHorizontalAnchor.MIDDLE;

    [JsonPropertyName("topSpacing")]
    public int TopSpacing { get; set; } = 20;

    [JsonPropertyName("leftSpacing")]
    public int LeftSpacing { get; set; } = 20;

    [JsonPropertyName("rightSpacing")]
    public int RightSpacing { get; set; } = 20;

    [JsonPropertyName("bottomSpacing")]
    public int BottomSpacing { get; set; } = 20;

    [JsonPropertyName("language")]
    public SupportedLanguage Language { get; set; } = SupportedLanguage.EN_US;

    [JsonPropertyName("showUnpinnedRunningApps")]
    public bool ShowUnpinnedRunningApps { get; set; } = true;

    [JsonPropertyName("verticalDock")]
    public bool VerticalDock { get; set; }

    [JsonPropertyName("tintIcons")]
    public bool TintIcons { get; set; }

    [JsonPropertyName("tintColorRGB")]
    public string TintColorRGB { get; set; } = "0, 80, 140";

    public void AddItem(DockItem item) => Items.Add(item);

    public void RemoveItem(int index) => Items.RemoveAt(index);

    public void LoadDefaultItems() => Items.Add(new DockSettingsItemModel());

    public void SwapItems(int firstItemIdx, int secondItemIdx)
    {
        (Items[firstItemIdx], Items[secondItemIdx]) =
            (Items[secondItemIdx], Items[firstItemIdx]);
    }

    /// <summary>
    /// Moves the item at <paramref name="fromIndex"/> into the gap at
    /// <paramref name="toIndex"/> (0..Count): the item ends up at that final
    /// position, with the others shifting accordingly. Unlike SwapItems this
    /// supports arbitrary jumps, which drag-reorder needs.
    /// </summary>
    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Items.Count) return;
        toIndex = Math.Clamp(toIndex, 0, Items.Count);
        if (fromIndex == toIndex) return;

        var item = Items[fromIndex];
        Items.RemoveAt(fromIndex);
        // After removal the gap index shifts left when moving downwards.
        if (toIndex > fromIndex) toIndex--;
        Items.Insert(toIndex, item);
    }

    public void SetDockPosition(double positionX, double positionY)
    {
        DockPositionX = positionX;
        DockPositionY = positionY;
    }
}
