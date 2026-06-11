using System.Drawing;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Overlay;
using Microsoft.Extensions.Options;

namespace FF14Toolkit.App.Services.TemplateMatching.Debug;

public sealed class TemplateMatchDebugVisualizer : ITemplateMatchDebugVisualizer
{
    private readonly bool isEnabled;
    private readonly TemplateMatchOverlayService overlayService;
    private readonly TemplateMatchOverlayFrameFactory overlayFrameFactory;
    private readonly TemplateMatchDebugWindowService normalWindowService;
    private readonly TemplateMatchDebugVisibilityController visibilityController;
    private readonly FF14Toolkit.App.Services.Crafting.CraftSequenceHotkeyLogService logger;

    public TemplateMatchDebugVisualizer(
        TemplateMatchOverlayService overlayService,
        TemplateMatchOverlayFrameFactory overlayFrameFactory,
        TemplateMatchDebugWindowService normalWindowService,
        TemplateMatchDebugVisibilityController visibilityController,
        IOptions<DevelopmentOptions> developmentOptions,
        FF14Toolkit.App.Services.Crafting.CraftSequenceHotkeyLogService logger)
    {
        isEnabled = developmentOptions.Value.ShowTemplateMatchOverlay;
        this.overlayService = overlayService;
        this.overlayFrameFactory = overlayFrameFactory;
        this.normalWindowService = normalWindowService;
        this.visibilityController = visibilityController;
        this.logger = logger;
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
        TemplateMatchDebugVisibilityController.VisibilityDecision decision = visibilityController.Evaluate(
            frame.MonitorId,
            clickableBounds);

        if (decision.SuppressedNow)
        {
            logger.LogInformation($"Template monitor debug view suppressed: {frame.MonitorId}");
        }

        if (decision.RestoredNow)
        {
            logger.LogInformation($"Template monitor debug view restored: {frame.MonitorId}");
        }

        if (decision.IsSuppressed)
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
        await overlayService.HideAsync().ConfigureAwait(false);
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
                await overlayService.HideAsync().ConfigureAwait(false);
                break;

            case TemplateMatchDebugViewMode.Overlay:
                await normalWindowService.HideAsync(frame.MonitorId, cancellationToken).ConfigureAwait(false);
                await overlayService.ShowFrameAsync(frame.Result.CaptureBounds, overlayFrame, keepVisible: true).ConfigureAwait(false);
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
                await overlayService.HideAsync().ConfigureAwait(false);
                break;

            default:
                await overlayService.HideAsync().ConfigureAwait(false);
                await normalWindowService.HideAsync(monitorId, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private sealed record TemplateDebugVisualizationDecision(
        bool Requested,
        bool GlobalEnabled,
        TemplateMatchDebugViewMode ViewMode,
        bool Effective,
        string? DisabledReason);
}
