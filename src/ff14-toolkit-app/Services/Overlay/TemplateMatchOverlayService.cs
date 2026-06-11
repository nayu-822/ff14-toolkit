using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Crafting;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class TemplateMatchOverlayService
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(1.6);

    private readonly Dispatcher dispatcher;
    private readonly bool isEnabled;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly Func<ITemplateMatchOverlayWindow> windowFactory;
    private ITemplateMatchOverlayWindow? overlayWindow;
    private int overlayVersion;
    private int handleCreatedLogged;

    public TemplateMatchOverlayService(
        IOptions<DevelopmentOptions> developmentOptions,
        CraftSequenceHotkeyLogService logger)
        : this(developmentOptions, logger, static () => new TemplateMatchOverlayWindowHost())
    {
    }

    internal TemplateMatchOverlayService(
        IOptions<DevelopmentOptions> developmentOptions,
        CraftSequenceHotkeyLogService logger,
        Func<ITemplateMatchOverlayWindow> windowFactory)
    {
        dispatcher = Application.Current.Dispatcher;
        isEnabled = developmentOptions.Value.ShowTemplateMatchOverlay;
        this.logger = logger;
        this.windowFactory = windowFactory;
    }

    public void Show(Rectangle screenBounds, IReadOnlyList<TemplateMatchOverlayRegion> regions)
    {
        TemplateMatchOverlayFrame frame = new(
            TemplateMatchOverlayState.Matched,
            "TEMPLATE",
            null,
            null,
            null,
            regions,
            null);
        ShowFrame(screenBounds, frame, keepVisible: false);
    }

    public void ShowFrame(Rectangle screenBounds, TemplateMatchOverlayFrame frame, bool keepVisible)
    {
        if (!isEnabled)
        {
            return;
        }

        int currentVersion = Interlocked.Increment(ref overlayVersion);
        DispatcherOperation operation = dispatcher.InvokeAsync(() =>
        {
            try
            {
                overlayWindow ??= windowFactory();
                TemplateMatchOverlayPresenter.Present(overlayWindow, screenBounds, frame);
                if (Interlocked.Exchange(ref handleCreatedLogged, 1) == 0)
                {
                    logger.LogInformation("Overlay window handle created.");
                }

                logger.LogDebug("Template-match overlay frame updated.");
            }
            catch (Exception exception)
            {
                logger.LogError("Template-match overlay update failed.", exception);
                throw;
            }
        });

        _ = ObserveDispatcherOperationAsync(operation);

        if (!keepVisible)
        {
            _ = HideLaterAsync(currentVersion);
        }
    }

    public void Hide()
    {
        Interlocked.Increment(ref overlayVersion);
        DispatcherOperation operation = dispatcher.InvokeAsync(() =>
        {
            try
            {
                overlayWindow?.Hide();
            }
            catch (Exception exception)
            {
                logger.LogError("Template-match overlay hide/stop failed.", exception);
                throw;
            }
        });

        _ = ObserveDispatcherOperationAsync(operation);
    }

    public void Shutdown()
    {
        Interlocked.Increment(ref overlayVersion);
        DispatcherOperation operation = dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (overlayWindow is null)
                {
                    return;
                }

                overlayWindow.Close();
                overlayWindow = null;
            }
            catch (Exception exception)
            {
                logger.LogError("Template-match overlay shutdown failed.", exception);
                throw;
            }
        });

        _ = ObserveDispatcherOperationAsync(operation);
    }

    private async Task HideLaterAsync(int version)
    {
        await Task.Delay(DisplayDuration).ConfigureAwait(false);
        DispatcherOperation operation = dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (version != overlayVersion)
                {
                    return;
                }

                overlayWindow?.Hide();
            }
            catch (Exception exception)
            {
                logger.LogError("Template-match overlay hide/stop failed.", exception);
                throw;
            }
        });

        await ObserveDispatcherOperationAsync(operation).ConfigureAwait(false);
    }

    private async Task ObserveDispatcherOperationAsync(DispatcherOperation operation)
    {
        try
        {
            await operation.Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError("Dispatcher exception logging: template-match overlay update failed.", exception);
        }
    }
}
