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
    private const string ActiveFrameId = "template-match:active";

    private readonly Dispatcher dispatcher;
    private readonly bool isEnabled;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly Func<ITemplateMatchOverlayWindow> windowFactory;
    private readonly IOverlayFrameStore frameStore;
    private readonly TemplateMatchOverlayFrameAdapter frameAdapter;
    private ITemplateMatchOverlayWindow? overlayWindow;
    private int overlayVersion;
    private int handleCreatedLogged;

    public TemplateMatchOverlayService(
        IOptions<DevelopmentOptions> developmentOptions,
        CraftSequenceHotkeyLogService logger,
        IOverlayFrameStore frameStore,
        TemplateMatchOverlayFrameAdapter frameAdapter)
        : this(developmentOptions, logger, frameStore, frameAdapter, static () => new TemplateMatchOverlayWindowHost())
    {
    }

    internal TemplateMatchOverlayService(
        IOptions<DevelopmentOptions> developmentOptions,
        CraftSequenceHotkeyLogService logger,
        IOverlayFrameStore frameStore,
        TemplateMatchOverlayFrameAdapter frameAdapter,
        Func<ITemplateMatchOverlayWindow> windowFactory)
    {
        dispatcher = Application.Current.Dispatcher;
        isEnabled = developmentOptions.Value.ShowTemplateMatchOverlay;
        this.logger = logger;
        this.frameStore = frameStore;
        this.frameAdapter = frameAdapter;
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
        ObserveFireAndForget(ShowFrameAsync(screenBounds, frame, keepVisible: false), "Failed to show template-match overlay.");
    }

    public void ShowFrame(Rectangle screenBounds, TemplateMatchOverlayFrame frame, bool keepVisible)
    {
        ObserveFireAndForget(ShowFrameAsync(screenBounds, frame, keepVisible), "Failed to show template-match overlay.");
    }

    public async Task ShowFrameAsync(Rectangle screenBounds, TemplateMatchOverlayFrame frame, bool keepVisible)
    {
        if (!isEnabled)
        {
            return;
        }

        try
        {
            int currentVersion = Interlocked.Increment(ref overlayVersion);
            OverlayFrame overlayFrame = frameAdapter.CreateOverlayFrame(ActiveFrameId, screenBounds, frame);
            frameStore.AddOrUpdate(overlayFrame);
            await dispatcher.InvokeAsync(() =>
            {
                overlayWindow ??= windowFactory();
                TemplateMatchOverlayFrame presentedFrame = frameAdapter.ToTemplateMatchOverlayFrame(overlayFrame, frame);
                TemplateMatchOverlayPresenter.Present(overlayWindow, screenBounds, presentedFrame);
                if (Interlocked.Exchange(ref handleCreatedLogged, 1) == 0)
                {
                    logger.LogInformation("Overlay window handle created.");
                }

                logger.LogDebug($"Template-match overlay frame updated. State={frame.State}, Regions={frame.Regions.Count}, KeepVisible={keepVisible}");
            }).Task.ConfigureAwait(false);

            if (!keepVisible)
            {
                _ = HideLaterAsync(currentVersion);
            }
        }
        catch (Exception exception)
        {
            logger.LogError("Template-match overlay update failed.", exception);
            throw;
        }
    }

    public void Hide()
    {
        ObserveFireAndForget(HideAsync(), "Failed to hide template-match overlay.");
    }

    public async Task HideAsync()
    {
        try
        {
            Interlocked.Increment(ref overlayVersion);
            frameStore.Remove(ActiveFrameId);
            await dispatcher.InvokeAsync(() =>
            {
                overlayWindow?.Hide();
            }).Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError("Template-match overlay hide/stop failed.", exception);
            throw;
        }
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

                frameStore.Clear();
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
        try
        {
            await Task.Delay(DisplayDuration).ConfigureAwait(false);
            await dispatcher.InvokeAsync(() =>
            {
                if (version != overlayVersion)
                {
                    return;
                }

                frameStore.Remove(ActiveFrameId);
                overlayWindow?.Hide();
            }).Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError("Template-match overlay hide/stop failed.", exception);
        }
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

    private void ObserveFireAndForget(Task task, string message)
    {
        _ = ObserveFireAndForgetCoreAsync(task, message);
    }

    private async Task ObserveFireAndForgetCoreAsync(Task task, string message)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(message, exception);
        }
    }
}
