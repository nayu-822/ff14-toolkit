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

    private readonly Dispatcher dispatcher;
    private readonly bool isEnabled;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly Func<ITemplateMatchOverlayWindow> windowFactory;
    private readonly IOverlayFrameStore frameStore;
    private readonly TemplateMatchOverlayFrameAdapter frameAdapter;
    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, CancellationTokenSource> autoHideSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TemplateMatchOverlayFrame> templateFrames = new(StringComparer.OrdinalIgnoreCase);
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
        TimeSpan? autoHideAfter = keepVisible ? null : TimeSpan.FromSeconds(1.6);
        OverlayFrame overlayFrame = frameAdapter.CreateOverlayFrame(ActiveFrameId, screenBounds, frame, keepVisible, autoHideAfter);
        lock (syncRoot)
        {
            templateFrames[overlayFrame.FrameId] = frame;
        }

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
                lock (syncRoot)
                {
                    templateFrames.Clear();
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
            lock (syncRoot)
            {
                templateFrames.Remove(frameId);
            }

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

            lock (syncRoot)
            {
                foreach (OverlayFrame frame in removedFrames)
                {
                    templateFrames.Remove(frame.FrameId);
                }
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
            lock (syncRoot)
            {
                templateFrames.Clear();
            }

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

        OverlayFrame frameToPresent = frames[^1];
        TemplateMatchOverlayFrame displayFrame = CreateDisplayFrame(frameToPresent, frames);

        overlayWindow ??= windowFactory();
        TemplateMatchOverlayPresenter.Present(overlayWindow, screenBounds, displayFrame);
        if (Interlocked.Exchange(ref handleCreatedLogged, 1) == 0)
        {
            logger.LogInformation("Overlay window handle created.");
        }
    }

    private TemplateMatchOverlayFrame CreateDisplayFrame(OverlayFrame latestFrame, IReadOnlyList<OverlayFrame> frames)
    {
        lock (syncRoot)
        {
            if (frames.Count == 1
                && templateFrames.TryGetValue(latestFrame.FrameId, out TemplateMatchOverlayFrame? sourceFrame))
            {
                return frameAdapter.ToTemplateMatchOverlayFrame(latestFrame, sourceFrame);
            }
        }

        return frameAdapter.CreateDisplayFrame(frames);
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
