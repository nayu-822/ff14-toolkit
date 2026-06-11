namespace FF14Toolkit.App.Services.Overlay;

public interface IOverlayFrameWindow
{
    bool IsVisible { get; }

    event EventHandler<OverlayElementClickedEventArgs>? ElementClicked;

    void EnsureHandle();

    void ShowFrame(OverlayFrame frame);

    void Show();

    void Hide();

    void Close();
}
