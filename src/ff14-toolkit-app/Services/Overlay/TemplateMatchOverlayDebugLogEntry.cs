using System.Drawing;
using System.Windows;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace FF14Toolkit.App.Services.Overlay;

public sealed record TemplateMatchOverlayDebugLogEntry(
    Rectangle PhysicalScreenBounds,
    WpfPoint DipWindowOrigin,
    WpfSize DipWindowSize,
    double DpiScaleX,
    double DpiScaleY,
    IReadOnlyList<TemplateMatchOverlayDebugRegionLogEntry> Regions);

public sealed record TemplateMatchOverlayDebugRegionLogEntry(
    string Label,
    Rectangle PhysicalRegionBounds,
    WpfPoint DipRegionOrigin,
    WpfSize DipRegionSize,
    WpfPoint DipLabelOrigin);
