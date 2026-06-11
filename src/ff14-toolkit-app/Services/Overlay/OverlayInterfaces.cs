namespace FF14Toolkit.App.Services.Overlay;

public enum OverlayCloseReason
{
    ElementClicked = 0,
    AutoHidden = 1,
    Replaced = 2,
    OwnerStopped = 3,
    ExplicitlyClosed = 4,
    Cleared = 5
}

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
