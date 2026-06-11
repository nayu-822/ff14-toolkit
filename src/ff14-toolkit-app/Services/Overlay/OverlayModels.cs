using System.Drawing;

namespace FF14Toolkit.App.Services.Overlay;

public enum OverlayInputMode
{
    ClickThrough = 0,
    InteractiveElementsOnly = 1,
    FullyInteractive = 2
}

public enum OverlayDashStyle
{
    Solid = 0,
    Dash = 1,
    Dot = 2,
    DashDot = 3
}

public readonly record struct OverlayColor(
    byte A,
    byte R,
    byte G,
    byte B);

public sealed record OverlayStroke(
    OverlayColor Color,
    double Thickness,
    OverlayDashStyle DashStyle);

public sealed record OverlayFill(
    OverlayColor Color);

public sealed record OverlayInteraction(
    bool IsHitTestVisible,
    OverlayClickAction ClickAction)
{
    public static OverlayInteraction None { get; } = new(false, OverlayClickAction.None);
}

public sealed record OverlayFrameOptions(
    bool Topmost,
    bool ShowActivated,
    OverlayInputMode InputMode,
    TimeSpan? AutoHideAfter,
    bool KeepVisible);

public abstract record OverlayElement(
    string FrameId,
    string OwnerId,
    string ElementId,
    int ZIndex,
    bool IsVisible,
    OverlayInteraction Interaction);

public sealed record OverlayRectangleElement(
    string FrameId,
    string OwnerId,
    string ElementId,
    Rectangle Bounds,
    OverlayStroke Stroke,
    OverlayFill? Fill,
    string? Label,
    int ZIndex,
    bool IsVisible = true,
    OverlayInteraction? Interaction = null)
    : OverlayElement(FrameId, OwnerId, ElementId, ZIndex, IsVisible, Interaction ?? OverlayInteraction.None);

public sealed record OverlayFrame(
    string FrameId,
    string OwnerId,
    Rectangle ScreenBounds,
    IReadOnlyList<OverlayElement> Elements,
    OverlayFrameOptions Options);
