using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Views;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Diagnostics;
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
            TemplateMatchOverlayDebugLogEntry logEntry = overlayWindow.ShowRegions(screenBounds, regions);
            Debug.WriteLine(
                $"TemplateMatchOverlay screenBounds={logEntry.PhysicalScreenBounds} dipWindowOrigin=({logEntry.DipWindowOrigin.X:F2},{logEntry.DipWindowOrigin.Y:F2}) dipWindowSize=({logEntry.DipWindowSize.Width:F2},{logEntry.DipWindowSize.Height:F2}) dpiScale=({logEntry.DpiScaleX:F3},{logEntry.DpiScaleY:F3})");
            foreach (TemplateMatchOverlayDebugRegionLogEntry regionLog in logEntry.Regions)
            {
                Debug.WriteLine(
                    $"TemplateMatchOverlay region={regionLog.Label} physicalBounds={regionLog.PhysicalRegionBounds} dipOrigin=({regionLog.DipRegionOrigin.X:F2},{regionLog.DipRegionOrigin.Y:F2}) dipSize=({regionLog.DipRegionSize.Width:F2},{regionLog.DipRegionSize.Height:F2}) dipLabelOrigin=({regionLog.DipLabelOrigin.X:F2},{regionLog.DipLabelOrigin.Y:F2})");
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
