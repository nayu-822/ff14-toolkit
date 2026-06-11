using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Views;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class TemplateMatchOverlayService
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(1.6);

    private readonly Dispatcher dispatcher;
    private readonly bool isEnabled;
    private TemplateMatchOverlayWindow? overlayWindow;
    private int overlayVersion;

    public TemplateMatchOverlayService(IOptions<DevelopmentOptions> developmentOptions)
    {
        dispatcher = Application.Current.Dispatcher;
        isEnabled = developmentOptions.Value.ShowTemplateMatchOverlay;
    }

    public void Show(Rectangle screenBounds, IReadOnlyList<TemplateMatchOverlayRegion> regions)
    {
        if (!isEnabled || regions.Count == 0)
        {
            return;
        }

        int currentVersion = Interlocked.Increment(ref overlayVersion);
        _ = dispatcher.InvokeAsync(() =>
        {
            overlayWindow ??= new TemplateMatchOverlayWindow();
            _ = new WindowInteropHelper(overlayWindow).EnsureHandle();

            try
            {
                overlayWindow.ShowRegions(screenBounds, regions);
            }
            catch (InvalidOperationException)
            {
                if (!overlayWindow.IsVisible)
                {
                    overlayWindow.Show();
                }

                overlayWindow.ShowRegions(screenBounds, regions);
            }

            if (!overlayWindow.IsVisible)
            {
                overlayWindow.Show();
            }
        });

        _ = HideLaterAsync(currentVersion);
    }

    public void Shutdown()
    {
        Interlocked.Increment(ref overlayVersion);
        _ = dispatcher.InvokeAsync(() =>
        {
            if (overlayWindow is null)
            {
                return;
            }

            overlayWindow.Close();
            overlayWindow = null;
        });
    }

    private async Task HideLaterAsync(int version)
    {
        await Task.Delay(DisplayDuration).ConfigureAwait(false);
        await dispatcher.InvokeAsync(() =>
        {
            if (version != overlayVersion)
            {
                return;
            }

            overlayWindow?.Hide();
        });
    }
}
