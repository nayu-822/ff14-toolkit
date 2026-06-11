using FF14Toolkit.App.Views;
using System.Drawing;
using System.Windows.Interop;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class TemplateMatchOverlayWindowHost : ITemplateMatchOverlayWindow
{
    private readonly TemplateMatchOverlayWindow window = new();

    public bool IsVisible => window.IsVisible;

    public void EnsureHandle()
    {
        _ = new WindowInteropHelper(window).EnsureHandle();
    }

    public void ShowFrame(Rectangle screenBounds, TemplateMatchOverlayFrame frame)
    {
        window.ShowFrame(screenBounds, frame);
    }

    public void Show()
    {
        window.Show();
    }

    public void Hide()
    {
        window.Hide();
    }

    public void Close()
    {
        window.Close();
    }
}
