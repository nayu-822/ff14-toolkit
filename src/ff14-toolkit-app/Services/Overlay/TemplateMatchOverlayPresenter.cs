using System.Drawing;

namespace FF14Toolkit.App.Services.Overlay;

public static class TemplateMatchOverlayPresenter
{
    public static void Present(
        ITemplateMatchOverlayWindow overlayWindow,
        Rectangle screenBounds,
        TemplateMatchOverlayFrame frame)
    {
        overlayWindow.EnsureHandle();
        overlayWindow.ShowFrame(screenBounds, frame);

        if (!overlayWindow.IsVisible)
        {
            overlayWindow.Show();
        }
    }
}
