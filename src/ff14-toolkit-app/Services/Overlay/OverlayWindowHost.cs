using FF14Toolkit.App.Views;
using System.Windows.Interop;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class OverlayFrameWindowHost : IOverlayFrameWindow
{
    private readonly OverlayFrameWindow window = new();

    public bool IsVisible => window.IsVisible;

    public event EventHandler<OverlayElementClickedEventArgs>? ElementClicked
    {
        add => window.ElementClicked += value;
        remove => window.ElementClicked -= value;
    }

    public void EnsureHandle()
    {
        _ = new WindowInteropHelper(window).EnsureHandle();
    }

    public void ShowFrame(OverlayFrame frame)
    {
        window.ShowFrame(frame);
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
