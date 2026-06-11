using System.Drawing;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Overlay;
using Microsoft.Extensions.Options;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Services.TemplateMatching.Debug;

public sealed class TemplateMatchDebugVisualizer : ITemplateMatchDebugVisualizer
{
    private readonly bool isEnabled;
    private readonly TemplateMatchOverlayService overlayService;
    private readonly TemplateMatchDebugWindowService normalWindowService;
    private readonly TemplateMatchDebugVisibilityController visibilityController;
    private readonly FF14Toolkit.App.Services.Crafting.CraftSequenceHotkeyLogService logger;

    public TemplateMatchDebugVisualizer(
        TemplateMatchOverlayService overlayService,
        TemplateMatchDebugWindowService normalWindowService,
        TemplateMatchDebugVisibilityController visibilityController,
        IOptions<DevelopmentOptions> developmentOptions,
        FF14Toolkit.App.Services.Crafting.CraftSequenceHotkeyLogService logger)
    {
        isEnabled = developmentOptions.Value.ShowTemplateMatchOverlay;
        this.overlayService = overlayService;
        this.normalWindowService = normalWindowService;
        this.visibilityController = visibilityController;
        this.logger = logger;
    }

    public async Task ShowAsync(TemplateMatchDebugFrame frame, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isEnabled)
        {
            return;
        }

        Rectangle? clickableBounds = frame.Result.MatchedBounds ?? frame.Result.BestCandidateBounds;
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

        bool isSearchingFrame =
            string.Equals(frame.Result.ErrorMessage, "SEARCHING", StringComparison.Ordinal)
            && frame.Result.MatchedBounds is null
            && frame.Result.BestCandidateBounds is null;

        if (frame.Result.Status == TemplateMatchStatus.CaptureFailed
            || frame.Result.Status == TemplateMatchStatus.TemplateLoadFailed
            || frame.Result.Status == TemplateMatchStatus.Error)
        {
            await ShowByModeAsync(
                frame,
                new TemplateMatchOverlayFrame(
                    TemplateMatchOverlayState.Error,
                    frame.TargetName,
                    frame.Result.BestScore,
                    frame.Result.Threshold,
                    frame.Result.Scale,
                    [],
                    frame.Result.ErrorMessage),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        List<TemplateMatchOverlayRegion> regions = [];
        regions.Add(new TemplateMatchOverlayRegion(
            "search-region",
            frame.Result.SearchBounds,
            MediaColor.FromArgb(224, 74, 163, 255),
            MediaColor.FromArgb(40, 74, 163, 255),
            true));

        if (frame.Result.BestCandidateBounds is Rectangle bestCandidateBounds
            && frame.Result.Status != TemplateMatchStatus.Matched)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                $"{frame.Result.TemplateId} CANDIDATE {frame.Result.BestScore:F3} / {frame.Result.Threshold:F3}",
                bestCandidateBounds,
                MediaColor.FromArgb(224, 255, 193, 7),
                MediaColor.FromArgb(32, 255, 193, 7),
                true));
        }

        if (frame.Result.MatchedBounds is Rectangle matchedBounds)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                $"{frame.Result.TemplateId} MATCHED {frame.Result.BestScore:F3} / {frame.Result.Threshold:F3}",
                matchedBounds,
                MediaColor.FromArgb(232, 76, 217, 100),
                MediaColor.FromArgb(72, 76, 217, 100),
                false));
        }

        TemplateMatchOverlayState state = frame.Result.Status switch
        {
            TemplateMatchStatus.Matched => TemplateMatchOverlayState.Matched,
            _ when isSearchingFrame => TemplateMatchOverlayState.Searching,
            TemplateMatchStatus.NotMatched => TemplateMatchOverlayState.NotMatched,
            TemplateMatchStatus.InvalidRequest => TemplateMatchOverlayState.Error,
            TemplateMatchStatus.CaptureFailed => TemplateMatchOverlayState.Error,
            TemplateMatchStatus.TemplateLoadFailed => TemplateMatchOverlayState.Error,
            _ => TemplateMatchOverlayState.Error
        };

        logger.LogDebug(
            $"Template monitor debug frame: MonitorId={frame.MonitorId}, ViewMode={frame.ViewMode}, Status={frame.Result.Status}, State={state}, Clickable={(clickableBounds is null ? "None" : clickableBounds.Value.ToString())}, Regions={regions.Count}");

        await ShowByModeAsync(
            frame,
            new TemplateMatchOverlayFrame(
                state,
                frame.TargetName,
                frame.Result.BestScore > 0d ? frame.Result.BestScore : null,
                frame.Result.Threshold,
                frame.Result.Scale > 0d ? frame.Result.Scale : null,
                regions,
                frame.Result.ErrorMessage),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HideAsync(string monitorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        visibilityController.Reset(monitorId);
        await overlayService.HideAsync().ConfigureAwait(false);
        await normalWindowService.HideAsync(monitorId, cancellationToken).ConfigureAwait(false);
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
}
