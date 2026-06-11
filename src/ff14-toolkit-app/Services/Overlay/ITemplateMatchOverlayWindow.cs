using System.Drawing;

namespace FF14Toolkit.App.Services.Overlay;

public interface ITemplateMatchOverlayWindow
{
    bool IsVisible { get; }

    void EnsureHandle();

    void ShowFrame(Rectangle screenBounds, TemplateMatchOverlayFrame frame);

    void Show();

    void Hide();

    void Close();
}
