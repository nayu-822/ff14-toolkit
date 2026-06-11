using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Crafting;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class TemplateMatchOverlayService : IOverlayService
{
    private const string ActiveFrameId = "template-match:active";
    private const string AggregateFrameId = "overlay:aggregate";
    private const string AggregateOwnerId = "overlay-service";

    private readonly Dispatcher dispatcher;
    private readonly bool isEnabled;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly Func<IOverlayFrameWindow> windowFactory;
    private readonly IOverlayFrameStore frameStore;
    private readonly TemplateMatchOverlayFrameAdapter frameAdapter;
    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, CancellationTokenSource> autoHideSources = new(StringComparer.OrdinalIgnoreCase);
    private IOverlayFrameWindow? overlayWindow;
    private int overlayVersion;
    private int handleCreatedLogged;

    public TemplateMatchOverlayService(
        IOptions<DevelopmentOptions> developmentOptions,
        CraftSequenceHotkeyLogService logger,
        IOverlayFrameStore frameStore,
        TemplateMatchOverlayFrameAdapter frameAdapter)
        : this(developmentOptions, logger, frameStore, frameAdapter, static () => new OverlayFrameWindowHost())
    {
    }

    internal TemplateMatchOverlayService(
        IOptions<DevelopmentOptions> developmentOptions,
        CraftSequenceHotkeyLogService logger,
        IOverlayFrameStore frameStore,
        TemplateMatchOverlayFrameAdapter frameAdapter,
        Func<IOverlayFrameWindow> windowFactory)
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
        TimeSpan? autoHideAfter = keepVisible ? null : TimeSpan.FromSeconds(1.6);
        OverlayFrame overlayFrame = frameAdapter.CreateOverlayFrame(ActiveFrameId, screenBounds, frame, keepVisible, autoHideAfter);
        await ShowOrUpdateAsync(overlayFrame).ConfigureAwait(false);
    }

    public void Hide()
    {
        ObserveFireAndForget(HideAsync(), "Failed to hide template-match overlay.");
    }

    public async Task HideAsync()
    {
        await HideFrameAsync(ActiveFrameId, OverlayCloseReason.ExplicitlyClosed).ConfigureAwait(false);
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

                ClearAutoHideSources();
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

    public async Task ShowOrUpdateAsync(
        OverlayFrame frame,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isEnabled)
        {
            return;
        }

        try
        {
            frameStore.AddOrUpdate(frame);
            ResetAutoHide(frame);
            int currentVersion = Interlocked.Increment(ref overlayVersion);
            await dispatcher.InvokeAsync(() =>
            {
                RenderCurrentFrames(currentVersion);
            }).Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError("Template-match overlay update failed.", exception);
            throw;
        }
    }

    public async Task HideFrameAsync(
        string frameId,
        OverlayCloseReason reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            CancelAutoHide(frameId);
            frameStore.Remove(frameId);

            int currentVersion = Interlocked.Increment(ref overlayVersion);
            await dispatcher.InvokeAsync(() =>
            {
                RenderCurrentFrames(currentVersion);
            }).Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError($"Template-match overlay hide failed. Reason={reason}", exception);
            throw;
        }
    }

    public async Task HideOwnerAsync(
        string ownerId,
        OverlayCloseReason reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            IReadOnlyList<OverlayFrame> removedFrames = frameStore.RemoveByOwner(ownerId);
            foreach (OverlayFrame frame in removedFrames)
            {
                CancelAutoHide(frame.FrameId);
            }

            int currentVersion = Interlocked.Increment(ref overlayVersion);
            await dispatcher.InvokeAsync(() =>
            {
                RenderCurrentFrames(currentVersion);
            }).Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError($"Template-match overlay owner hide failed. Reason={reason}", exception);
            throw;
        }
    }

    public async Task ClearAsync(
        OverlayCloseReason reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            ClearAutoHideSources();
            frameStore.Clear();

            int currentVersion = Interlocked.Increment(ref overlayVersion);
            await dispatcher.InvokeAsync(() =>
            {
                RenderCurrentFrames(currentVersion);
            }).Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError($"Template-match overlay clear failed. Reason={reason}", exception);
            throw;
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

    private void RenderCurrentFrames(int version)
    {
        if (version != overlayVersion)
        {
            return;
        }

        IReadOnlyList<OverlayFrame> frames = frameStore.GetAll()
            .OrderBy(frame => frame.OwnerId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(frame => frame.FrameId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frames.Count == 0)
        {
            overlayWindow?.Hide();
            return;
        }

        Rectangle screenBounds = frames
            .Select(frame => frame.ScreenBounds)
            .Aggregate(Rectangle.Union);

        OverlayFrame displayFrame = CreateDisplayFrame(screenBounds, frames);

        overlayWindow ??= windowFactory();
        OverlayPresenter.Present(overlayWindow, displayFrame);
        if (Interlocked.Exchange(ref handleCreatedLogged, 1) == 0)
        {
            logger.LogInformation("Overlay window handle created.");
        }
    }

    private static OverlayFrame CreateDisplayFrame(Rectangle screenBounds, IReadOnlyList<OverlayFrame> frames)
    {
        if (frames.Count == 1)
        {
            return frames[0];
        }

        OverlayFrame latestFrame = frames[^1];
        List<OverlayElement> elements = frames
            .SelectMany(frame => frame.Elements)
            .OrderBy(element => element.ZIndex)
            .ThenBy(element => element.ElementId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new OverlayFrame(
            AggregateFrameId,
            AggregateOwnerId,
            screenBounds,
            elements,
            latestFrame.Options with { AutoHideAfter = null, KeepVisible = true });
    }

    private void ResetAutoHide(OverlayFrame frame)
    {
        CancelAutoHide(frame.FrameId);
        if (frame.Options.AutoHideAfter is not TimeSpan autoHideAfter)
        {
            return;
        }

        CancellationTokenSource cancellationTokenSource = new();
        lock (syncRoot)
        {
            autoHideSources[frame.FrameId] = cancellationTokenSource;
        }

        ObserveFireAndForget(
            AutoHideFrameAsync(frame.FrameId, autoHideAfter, cancellationTokenSource.Token),
            $"Failed to auto-hide overlay frame: {frame.FrameId}");
    }

    private async Task AutoHideFrameAsync(string frameId, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await HideFrameAsync(frameId, OverlayCloseReason.AutoHidden).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelAutoHide(string frameId)
    {
        CancellationTokenSource? cancellationTokenSource = null;
        lock (syncRoot)
        {
            if (autoHideSources.Remove(frameId, out CancellationTokenSource? existingSource))
            {
                cancellationTokenSource = existingSource;
            }
        }

        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }

    private void ClearAutoHideSources()
    {
        List<CancellationTokenSource> sources;
        lock (syncRoot)
        {
            sources = autoHideSources.Values.ToList();
            autoHideSources.Clear();
        }

        foreach (CancellationTokenSource source in sources)
        {
            source.Cancel();
            source.Dispose();
        }
    }
}
