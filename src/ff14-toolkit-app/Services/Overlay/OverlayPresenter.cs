namespace FF14Toolkit.App.Services.Overlay;

public static class OverlayPresenter
{
    public static void Present(
        IOverlayFrameWindow overlayWindow,
        OverlayFrame frame)
    {
        overlayWindow.EnsureHandle();
        overlayWindow.ShowFrame(frame);

        if (!overlayWindow.IsVisible)
        {
            overlayWindow.Show();
        }
    }
}
