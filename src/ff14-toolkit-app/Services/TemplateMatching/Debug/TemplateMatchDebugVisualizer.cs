using System.Drawing;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Overlay;
using Microsoft.Extensions.Options;

namespace FF14Toolkit.App.Services.TemplateMatching.Debug;

public sealed class TemplateMatchDebugVisualizer : ITemplateMatchDebugVisualizer
{
    private readonly bool isEnabled;
    private readonly IOverlayService overlayService;
    private readonly IOverlayEventSource overlayEventSource;
    private readonly TemplateMatchOverlayFrameFactory overlayFrameFactory;
    private readonly TemplateMatchDebugWindowService normalWindowService;
    private readonly TemplateMatchDebugVisibilityController visibilityController;
    private readonly FF14Toolkit.App.Services.Crafting.CraftSequenceHotkeyLogService logger;

    public TemplateMatchDebugVisualizer(
        IOverlayService overlayService,
        IOverlayEventSource overlayEventSource,
        TemplateMatchOverlayFrameFactory overlayFrameFactory,
        TemplateMatchDebugWindowService normalWindowService,
        TemplateMatchDebugVisibilityController visibilityController,
        IOptions<DevelopmentOptions> developmentOptions,
        FF14Toolkit.App.Services.Crafting.CraftSequenceHotkeyLogService logger)
    {
        isEnabled = developmentOptions.Value.ShowTemplateMatchOverlay;
        this.overlayService = overlayService;
        this.overlayEventSource = overlayEventSource;
        this.overlayFrameFactory = overlayFrameFactory;
        this.normalWindowService = normalWindowService;
        this.visibilityController = visibilityController;
        this.logger = logger;
        this.overlayEventSource.ElementClicked += OnOverlayElementClicked;
    }

    public async Task ShowAsync(TemplateMatchDebugFrame frame, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TemplateDebugVisualizationDecision visualizationDecision = CreateDecision(frame);
        logger.LogInformation(
            $"Template debug visualization: MonitorId={frame.MonitorId} Requested=true GlobalEnabled={visualizationDecision.GlobalEnabled} ViewMode={visualizationDecision.ViewMode} Effective={visualizationDecision.Effective} DisabledReason={visualizationDecision.DisabledReason ?? "<none>"}");

        if (!visualizationDecision.Effective)
        {
            return;
        }

        Rectangle? clickableBounds = overlayFrameFactory.GetClickableBounds(frame.Result);
        if (visibilityController.IsSuppressed(frame.MonitorId))
        {
            logger.LogDebug($"Template monitor debug frame skipped: MonitorId={frame.MonitorId}, ViewMode={frame.ViewMode}, Status={frame.Result.Status}, Reason=Suppressed");
            await HideByModeAsync(frame.MonitorId, frame.ViewMode, cancellationToken).ConfigureAwait(false);
            return;
        }

        TemplateMatchOverlayFrame overlayFrame = overlayFrameFactory.Create(frame);

        logger.LogDebug(
            $"Template monitor debug frame: MonitorId={frame.MonitorId}, ViewMode={frame.ViewMode}, Status={frame.Result.Status}, State={overlayFrame.State}, Clickable={(clickableBounds is null ? "None" : clickableBounds.Value.ToString())}, Regions={overlayFrame.Regions.Count}");

        await ShowByModeAsync(frame, overlayFrame, cancellationToken).ConfigureAwait(false);
    }

    public async Task HideAsync(string monitorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        visibilityController.Remove(monitorId);
        await overlayService.HideFrameAsync(CreateFrameId(monitorId), OverlayCloseReason.ExplicitlyClosed, cancellationToken).ConfigureAwait(false);
        await normalWindowService.HideAsync(monitorId, cancellationToken).ConfigureAwait(false);
    }

    private TemplateDebugVisualizationDecision CreateDecision(TemplateMatchDebugFrame frame)
    {
        if (!isEnabled)
        {
            return new TemplateDebugVisualizationDecision(true, false, frame.ViewMode, false, "Global development flag is OFF");
        }

        if (frame.ViewMode == TemplateMatchDebugViewMode.None)
        {
            return new TemplateDebugVisualizationDecision(true, true, frame.ViewMode, false, "Debug view mode is None");
        }

        return new TemplateDebugVisualizationDecision(true, true, frame.ViewMode, true, null);
    }

    private async Task ShowByModeAsync(
        TemplateMatchDebugFrame frame,
        TemplateMatchOverlayFrame overlayFrame,
        CancellationToken cancellationToken)
    {
        switch (frame.ViewMode)
        {
            case TemplateMatchDebugViewMode.NormalWindow:
                await normalWindowService.ShowAsync(frame, cancellationToken).ConfigureAwait(false);
                await overlayService.HideFrameAsync(CreateFrameId(frame.MonitorId), OverlayCloseReason.ExplicitlyClosed, cancellationToken).ConfigureAwait(false);
                break;

            case TemplateMatchDebugViewMode.Overlay:
                await normalWindowService.HideAsync(frame.MonitorId, cancellationToken).ConfigureAwait(false);
                OverlayFrame genericOverlayFrame = overlayFrameFactory.CreateOverlayFrame(
                    CreateFrameId(frame.MonitorId),
                    frame,
                    keepVisible: true,
                    autoHideAfter: null);
                await overlayService.ShowOrUpdateAsync(genericOverlayFrame, cancellationToken).ConfigureAwait(false);
                break;

            default:
                await HideByModeAsync(frame.MonitorId, frame.ViewMode, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task HideByModeAsync(
        string monitorId,
        TemplateMatchDebugViewMode viewMode,
        CancellationToken cancellationToken)
    {
        switch (viewMode)
        {
            case TemplateMatchDebugViewMode.NormalWindow:
                await normalWindowService.HideAsync(monitorId, cancellationToken).ConfigureAwait(false);
                break;

            case TemplateMatchDebugViewMode.Overlay:
                await overlayService.HideFrameAsync(CreateFrameId(monitorId), OverlayCloseReason.ExplicitlyClosed, cancellationToken).ConfigureAwait(false);
                break;

            default:
                await overlayService.HideFrameAsync(CreateFrameId(monitorId), OverlayCloseReason.ExplicitlyClosed, cancellationToken).ConfigureAwait(false);
                await normalWindowService.HideAsync(monitorId, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static string CreateFrameId(string monitorId)
    {
        return $"template-match:{monitorId}";
    }

    private void OnOverlayElementClicked(object? sender, OverlayElementClickedEventArgs eventArgs)
    {
        if (!string.Equals(eventArgs.OwnerId, TemplateMatchOverlayFrameFactory.OwnerId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? monitorId = TryGetMonitorId(eventArgs.FrameId);
        if (string.IsNullOrWhiteSpace(monitorId))
        {
            return;
        }

        if (visibilityController.Suppress(monitorId))
        {
            logger.LogInformation($"Template monitor debug view suppressed: {monitorId}");
        }
    }

    private static string? TryGetMonitorId(string frameId)
    {
        const string prefix = "template-match:";
        return frameId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? frameId[prefix.Length..]
            : null;
    }

    private sealed record TemplateDebugVisualizationDecision(
        bool Requested,
        bool GlobalEnabled,
        TemplateMatchDebugViewMode ViewMode,
        bool Effective,
        string? DisabledReason);
}
