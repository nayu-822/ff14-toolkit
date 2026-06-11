using System.Drawing;

namespace FF14Toolkit.App.Services.Overlay;

public enum OverlayClickAction
{
    None = 0,
    RaiseEvent = 1,
    HideFrame = 2,
    HideOwner = 3
}

public enum OverlayCloseReason
{
    ElementClicked = 0,
    AutoHidden = 1,
    Replaced = 2,
    OwnerStopped = 3,
    ExplicitlyClosed = 4,
    Cleared = 5
}

public interface IOverlayEventSource
{
    event EventHandler<OverlayElementClickedEventArgs>? ElementClicked;

    event EventHandler<OverlayFrameClosedEventArgs>? FrameClosed;
}

public sealed record OverlayElementClickedEventArgs(
    string FrameId,
    string OwnerId,
    string ElementId,
    Point ScreenPosition);

public sealed record OverlayFrameClosedEventArgs(
    string FrameId,
    string OwnerId,
    OverlayCloseReason Reason);

public interface IOverlayService
{
    Task ShowOrUpdateAsync(
        OverlayFrame frame,
        CancellationToken cancellationToken = default);

    Task HideFrameAsync(
        string frameId,
        OverlayCloseReason reason,
        CancellationToken cancellationToken = default);

    Task HideOwnerAsync(
        string ownerId,
        OverlayCloseReason reason,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        OverlayCloseReason reason,
        CancellationToken cancellationToken = default);
}
