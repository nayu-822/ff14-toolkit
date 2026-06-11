using System.Diagnostics;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.TemplateMatching.Debug;

namespace FF14Toolkit.App.Services.TemplateMatching.Monitoring;

public sealed class TemplateMatchMonitor : ITemplateMatchMonitor, ITemplateMonitorStatusSource
{
    private readonly IScreenCaptureService screenCaptureService;
    private readonly ITemplateResourceLoader templateResourceLoader;
    private readonly ITemplateMatcher templateMatcher;
    private readonly ITemplateMatchResultSink resultSink;
    private readonly ITemplateMatchDebugVisualizer debugVisualizer;
    private readonly TemplateMatchDebugVisibilityController visibilityController;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, MonitorSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TemplateMonitorStatus> statuses = new(StringComparer.OrdinalIgnoreCase);

    public TemplateMatchMonitor(
        IScreenCaptureService screenCaptureService,
        ITemplateResourceLoader templateResourceLoader,
        ITemplateMatcher templateMatcher,
        ITemplateMatchResultSink resultSink,
        ITemplateMatchDebugVisualizer debugVisualizer,
        TemplateMatchDebugVisibilityController visibilityController,
        CraftSequenceHotkeyLogService logger)
    {
        this.screenCaptureService = screenCaptureService;
        this.templateResourceLoader = templateResourceLoader;
        this.templateMatcher = templateMatcher;
        this.resultSink = resultSink;
        this.debugVisualizer = debugVisualizer;
        this.visibilityController = visibilityController;
        this.logger = logger;
    }

    public event EventHandler<TemplateMonitorStatusChangedEventArgs>? StatusChanged;

    public async Task StartAsync(
        TemplateMonitorDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (definition.Interval < TimeSpan.FromMilliseconds(50))
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Template monitor interval must be 50ms or greater.");
        }

        DateTimeOffset startRequestedAt = DateTimeOffset.Now;
        logger.LogInformation(
            $"Template monitor start requested. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, CaptureBounds={definition.MatchRequest.CaptureBounds}, SearchBounds={(definition.MatchRequest.SearchBounds?.ToString() ?? definition.MatchRequest.CaptureBounds.ToString())}, IntervalMs={definition.Interval.TotalMilliseconds:F0}, MinimumScore={definition.MatchRequest.MinimumScore:F3}, Scales={string.Join(',', definition.MatchRequest.Scales.Select(scale => scale.ToString("F2")))}");

        MonitorSession session;
        lock (syncRoot)
        {
            if (sessions.ContainsKey(definition.MonitorId))
            {
                throw new InvalidOperationException($"Template monitor already exists: {definition.MonitorId}");
            }

            CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            TaskCompletionSource startedCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            session = new MonitorSession(cancellationTokenSource, startedCompletion);
            sessions.Add(definition.MonitorId, session);
        }

        UpdateStatus(new TemplateMonitorStatus(
            definition.MonitorId,
            TemplateMonitorState.Starting,
            startRequestedAt,
            null,
            null,
            null,
            0,
            null,
            null,
            null,
            new TemplateMonitorMetricsAccumulator().Snapshot()));

        visibilityController.Reset(definition.MonitorId);
        session.Task = Task.Run(() => RunAsync(definition, startRequestedAt, session, session.CancellationTokenSource.Token), CancellationToken.None);
        await session.StartedCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(
        string monitorId,
        CancellationToken cancellationToken = default)
    {
        MonitorSession? session;
        lock (syncRoot)
        {
            sessions.TryGetValue(monitorId, out session);
        }

        TemplateMonitorStatus? status = GetStatus(monitorId);
        if (status is not null)
        {
            UpdateStatus(status with { State = TemplateMonitorState.Stopping });
        }

        if (session is null)
        {
            await debugVisualizer.HideAsync(monitorId, cancellationToken).ConfigureAwait(false);
            return;
        }

        session.CancellationTokenSource.Cancel();

        try
        {
            await session.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public TemplateMonitorStatus? GetStatus(string monitorId)
    {
        lock (syncRoot)
        {
            return statuses.TryGetValue(monitorId, out TemplateMonitorStatus? status)
                ? status
                : null;
        }
    }

    private async Task RunAsync(
        TemplateMonitorDefinition definition,
        DateTimeOffset startRequestedAt,
        MonitorSession session,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        bool cancelled = false;
        bool startCompletionSignaled = false;
        bool completedBySelf = false;
        long processedFrameCount = 0;
        DateTimeOffset? startedAt = null;
        DateTimeOffset? firstFrameCompletedAt = null;
        DateTimeOffset? previousFrameCompletedAt = null;
        TemplateResource resource = null!;
        TemplateMonitorMetricsAccumulator metrics = new();
        TemplateMatchStatus? previousStatus = null;
        double? previousScore = null;

        try
        {
            long resourceLoadStartedAt = Stopwatch.GetTimestamp();
            logger.LogDebug(
                $"Template resource loading started. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, TemplatePath={definition.Resource.MetadataPath}");
            resource = templateResourceLoader.Load(definition.Resource);
            logger.LogInformation(
                $"Template resource loading completed. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, Width={resource.Image.Width}, Height={resource.Image.Height}, ElapsedMs={Stopwatch.GetElapsedTime(resourceLoadStartedAt).TotalMilliseconds:F1}");

            startedAt = DateTimeOffset.Now;
            UpdateStatus(new TemplateMonitorStatus(
                definition.MonitorId,
                TemplateMonitorState.Running,
                GetStatus(definition.MonitorId)?.StartRequestedAt,
                startedAt,
                null,
                null,
                0,
                null,
                null,
                null,
                metrics.Snapshot()));

            if (definition.EnableDebugVisualization && definition.DebugViewMode != TemplateMatchDebugViewMode.None)
            {
                await debugVisualizer.ShowAsync(
                        new TemplateMatchDebugFrame(
                            definition.MonitorId,
                            definition.TargetName,
                            new TemplateMatchResult(
                                definition.MatchRequest.TemplateId,
                                TemplateMatchStatus.NotMatched,
                                definition.MatchRequest.CaptureBounds,
                                definition.MatchRequest.SearchBounds ?? definition.MatchRequest.CaptureBounds,
                                null,
                                null,
                                0d,
                                definition.MatchRequest.MinimumScore,
                                0d,
                                TimeSpan.Zero,
                                DateTimeOffset.Now,
                                "SEARCHING"),
                            definition.DebugViewMode,
                            null),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            startCompletionSignaled = session.StartedCompletion.TrySetResult();
            logger.LogInformation(
                $"Template monitor started. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, StartedAt={startedAt:yyyy-MM-dd HH:mm:ss.fff zzz}, StartupElapsedMs={(startedAt.Value - startRequestedAt).TotalMilliseconds:F1}");

            while (!cancellationToken.IsCancellationRequested)
            {
                long frameStartedAt = Stopwatch.GetTimestamp();
                long captureStartedAt = Stopwatch.GetTimestamp();
                TemplateMatchResult result;
                TemplateMatchCapturePreview? preview = null;
                TimeSpan captureDuration = TimeSpan.Zero;
                TimeSpan matchingDuration = TimeSpan.Zero;
                TimeSpan publishDuration = TimeSpan.Zero;
                TimeSpan visualizationDuration = TimeSpan.Zero;

                try
                {
                    using ScreenCaptureFrame capture = screenCaptureService.Capture(definition.MatchRequest.CaptureBounds);
                    captureDuration = Stopwatch.GetElapsedTime(captureStartedAt);
                    long matchingStartedAt = Stopwatch.GetTimestamp();
                    logger.LogDebug(
                        $"Template matching started. MonitorId={definition.MonitorId}, FrameNumber={processedFrameCount + 1}, TemplateId={definition.MatchRequest.TemplateId}, SearchBounds={(definition.MatchRequest.SearchBounds?.ToString() ?? definition.MatchRequest.CaptureBounds.ToString())}, ScaleCount={definition.MatchRequest.Scales.Count}");
                    result = templateMatcher.Match(capture, resource, definition.MatchRequest, cancellationToken);
                    matchingDuration = Stopwatch.GetElapsedTime(matchingStartedAt);
                    preview = new TemplateMatchCapturePreview(
                        capture.ScreenBounds,
                        capture.Width,
                        capture.Height,
                        capture.Stride,
                        [.. capture.Pixels]);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError("Template matching failed.", exception);
                    result = new TemplateMatchResult(
                        definition.MatchRequest.TemplateId,
                        TemplateMatchStatus.CaptureFailed,
                        definition.MatchRequest.CaptureBounds,
                        definition.MatchRequest.SearchBounds ?? definition.MatchRequest.CaptureBounds,
                        null,
                        null,
                        0d,
                        definition.MatchRequest.MinimumScore,
                        0d,
                        Stopwatch.GetElapsedTime(frameStartedAt),
                        DateTimeOffset.Now,
                        exception.Message);
                }

                processedFrameCount++;

                long publishStartedAt = Stopwatch.GetTimestamp();
                await resultSink.PublishAsync(definition.MonitorId, result, cancellationToken).ConfigureAwait(false);
                publishDuration = Stopwatch.GetElapsedTime(publishStartedAt);

                if (definition.EnableDebugVisualization && definition.DebugViewMode != TemplateMatchDebugViewMode.None)
                {
                    long visualizationStartedAt = Stopwatch.GetTimestamp();
                    await debugVisualizer.ShowAsync(
                            new TemplateMatchDebugFrame(
                                definition.MonitorId,
                                definition.TargetName,
                                result,
                                definition.DebugViewMode,
                                preview),
                            cancellationToken)
                        .ConfigureAwait(false);
                    visualizationDuration = Stopwatch.GetElapsedTime(visualizationStartedAt);
                }

                DateTimeOffset frameCompletedAt = DateTimeOffset.Now;
                firstFrameCompletedAt ??= frameCompletedAt;
                TimeSpan actualInterval = previousFrameCompletedAt is null
                    ? TimeSpan.Zero
                    : frameCompletedAt - previousFrameCompletedAt.Value;
                previousFrameCompletedAt = frameCompletedAt;

                TemplateMatchFrameMetrics frameMetrics = new(
                    captureDuration,
                    matchingDuration == TimeSpan.Zero ? result.ProcessingTime : matchingDuration,
                    publishDuration,
                    visualizationDuration,
                    Stopwatch.GetElapsedTime(frameStartedAt),
                    actualInterval);
                metrics.Record(result, frameMetrics);

                UpdateStatus(new TemplateMonitorStatus(
                    definition.MonitorId,
                    TemplateMonitorState.Running,
                    GetStatus(definition.MonitorId)?.StartRequestedAt,
                    startedAt,
                    firstFrameCompletedAt,
                    null,
                    processedFrameCount,
                    result.Status,
                    result.BestScore,
                    result.ErrorMessage,
                    metrics.Snapshot()));

                if (processedFrameCount == 1)
                {
                    logger.LogInformation(
                        $"Template monitor first frame completed. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, FrameNumber=1, Status={result.Status}, BestScore={result.BestScore:F3}, Threshold={result.Threshold:F3}, SelectedScale={result.Scale:F2}, CaptureMs={frameMetrics.CaptureDuration.TotalMilliseconds:F1}, MatchingMs={frameMetrics.MatchingDuration.TotalMilliseconds:F1}, PublishMs={frameMetrics.PublishDuration.TotalMilliseconds:F1}, VisualizationMs={frameMetrics.VisualizationDuration.TotalMilliseconds:F1}, TotalFrameMs={frameMetrics.TotalDuration.TotalMilliseconds:F1}, ElapsedFromStartMs={(frameCompletedAt - startedAt.Value).TotalMilliseconds:F1}");
                }

                if (previousStatus is not null
                    && (previousStatus != result.Status || previousScore != result.BestScore))
                {
                    logger.LogInformation(
                        $"Template match state changed. MonitorId={definition.MonitorId}, FrameNumber={processedFrameCount}, PreviousStatus={previousStatus}, CurrentStatus={result.Status}, PreviousScore={(previousScore?.ToString("F3") ?? "-")}, CurrentScore={result.BestScore:F3}");
                }

                previousStatus = result.Status;
                previousScore = result.BestScore;

                logger.LogDebug(
                    $"Template frame processed. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, FrameNumber={processedFrameCount}, Status={result.Status}, BestScore={result.BestScore:F3}, Threshold={result.Threshold:F3}, SelectedScale={result.Scale:F2}, CaptureMs={frameMetrics.CaptureDuration.TotalMilliseconds:F1}, MatchingMs={frameMetrics.MatchingDuration.TotalMilliseconds:F1}, PublishMs={frameMetrics.PublishDuration.TotalMilliseconds:F1}, VisualizationMs={frameMetrics.VisualizationDuration.TotalMilliseconds:F1}, TotalFrameMs={frameMetrics.TotalDuration.TotalMilliseconds:F1}, ActualIntervalMs={frameMetrics.ActualInterval.TotalMilliseconds:F1}, ConfiguredIntervalMs={definition.Interval.TotalMilliseconds:F1}");

                TimeSpan remaining = definition.Interval - frameMetrics.TotalDuration;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }
            }

            completedBySelf = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
            if (!startCompletionSignaled)
            {
                session.StartedCompletion.TrySetCanceled(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            if (!startCompletionSignaled)
            {
                session.StartedCompletion.TrySetException(exception);
            }

            await PublishFailureSafelyAsync(definition, exception, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            if (!session.StartedCompletion.Task.IsCompleted)
            {
                session.StartedCompletion.TrySetException(
                    new InvalidOperationException("Template monitor ended before startup completed."));
            }

            await CleanupSessionAsync(
                definition,
                session,
                startedAt,
                firstFrameCompletedAt,
                processedFrameCount,
                metrics.Snapshot(),
                cancelled,
                completedBySelf,
                failure).ConfigureAwait(false);
        }
    }

    private async Task PublishFailureSafelyAsync(
        TemplateMonitorDefinition definition,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError("Template resource loading failed.", exception);
        TemplateMatchResult loadFailedResult = new(
            definition.MatchRequest.TemplateId,
            TemplateMatchStatus.TemplateLoadFailed,
            definition.MatchRequest.CaptureBounds,
            definition.MatchRequest.SearchBounds ?? definition.MatchRequest.CaptureBounds,
            null,
            null,
            0d,
            definition.MatchRequest.MinimumScore,
            0d,
            TimeSpan.Zero,
            DateTimeOffset.Now,
            exception.Message);

        try
        {
            await resultSink.PublishAsync(definition.MonitorId, loadFailedResult, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception publishException)
        {
            logger.LogError("Failed to publish template monitor failure.", publishException);
        }

        if (!definition.EnableDebugVisualization || definition.DebugViewMode == TemplateMatchDebugViewMode.None)
        {
            return;
        }

        try
        {
            await debugVisualizer.ShowAsync(
                    new TemplateMatchDebugFrame(
                        definition.MonitorId,
                        definition.TargetName,
                        loadFailedResult,
                        definition.DebugViewMode,
                        null),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception visualizeException)
        {
            logger.LogError("Failed to visualize template monitor failure.", visualizeException);
        }
    }

    private async Task CleanupSessionAsync(
        TemplateMonitorDefinition definition,
        MonitorSession session,
        DateTimeOffset? startedAt,
        DateTimeOffset? firstFrameCompletedAt,
        long processedFrameCount,
        TemplateMonitorMetricsSnapshot metrics,
        bool cancelled,
        bool completedBySelf,
        Exception? failure)
    {
        try
        {
            await debugVisualizer.HideAsync(definition.MonitorId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to hide template-match debug view.", exception);
        }

        try
        {
            RemoveSessionIfCurrent(definition.MonitorId, session);
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to remove template monitor session.", exception);
        }

        try
        {
            session.CancellationTokenSource.Dispose();
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to dispose template monitor cancellation source.", exception);
        }

        try
        {
            visibilityController.Remove(definition.MonitorId);
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to clear template debug visibility state.", exception);
        }

        TemplateMonitorStatus? currentStatus = GetStatus(definition.MonitorId);
        TemplateMonitorState finalState = failure is not null
            ? TemplateMonitorState.Faulted
            : cancelled
                ? TemplateMonitorState.Stopped
                : completedBySelf
                    ? TemplateMonitorState.Completed
                    : TemplateMonitorState.Stopped;

        DateTimeOffset stoppedAt = DateTimeOffset.Now;
        UpdateStatus(new TemplateMonitorStatus(
            definition.MonitorId,
            finalState,
            currentStatus?.StartRequestedAt,
            startedAt,
            firstFrameCompletedAt,
            stoppedAt,
            processedFrameCount,
            currentStatus?.LastMatchStatus,
            currentStatus?.LastScore,
            failure?.Message ?? currentStatus?.ErrorMessage,
            metrics));

        if (failure is not null)
        {
            double totalElapsedMs = startedAt is null ? 0d : (stoppedAt - startedAt.Value).TotalMilliseconds;
            logger.LogError(
                $"Template monitor faulted. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, TotalElapsedMs={totalElapsedMs:F1}, ProcessedFrameCount={processedFrameCount}, LastStatus={currentStatus?.LastMatchStatus?.ToString() ?? "-"}, LastScore={(currentStatus?.LastScore?.ToString("F3") ?? "-")}",
                failure);
            return;
        }

        double elapsedMs = startedAt is null ? 0d : (stoppedAt - startedAt.Value).TotalMilliseconds;
        string logLabel = finalState == TemplateMonitorState.Completed
            ? "Template monitor completed"
            : "Template monitor stopped";
        string stopReason = finalState switch
        {
            TemplateMonitorState.Completed => "Completed",
            TemplateMonitorState.Stopped when cancelled => "Cancelled",
            TemplateMonitorState.Stopped => "UserRequested",
            _ => finalState.ToString()
        };

        logger.LogInformation(
            $"{logLabel}. MonitorId={definition.MonitorId}, TemplateId={definition.MatchRequest.TemplateId}, FinalState={finalState}, StopReason={stopReason}, StartedAt={startedAt:yyyy-MM-dd HH:mm:ss.fff zzz}, StoppedAt={stoppedAt:yyyy-MM-dd HH:mm:ss.fff zzz}, TotalElapsedMs={elapsedMs:F1}, ProcessedFrameCount={processedFrameCount}, MatchedFrameCount={metrics.MatchedFrameCount}, NotMatchedFrameCount={metrics.NotMatchedFrameCount}, ErrorFrameCount={metrics.ErrorFrameCount}, AverageCaptureMs={metrics.AverageCaptureDuration.TotalMilliseconds:F1}, AverageMatchingMs={metrics.AverageMatchingDuration.TotalMilliseconds:F1}, AveragePublishMs={metrics.AveragePublishDuration.TotalMilliseconds:F1}, AverageVisualizationMs={metrics.AverageVisualizationDuration.TotalMilliseconds:F1}, AverageTotalFrameMs={metrics.AverageFrameDuration.TotalMilliseconds:F1}, MaxFrameMs={metrics.MaxFrameDuration.TotalMilliseconds:F1}, LastStatus={currentStatus?.LastMatchStatus?.ToString() ?? "-"}, LastScore={(currentStatus?.LastScore?.ToString("F3") ?? "-")}");
    }

    private void RemoveSessionIfCurrent(string monitorId, MonitorSession expectedSession)
    {
        lock (syncRoot)
        {
            if (sessions.TryGetValue(monitorId, out MonitorSession? currentSession)
                && ReferenceEquals(currentSession, expectedSession))
            {
                sessions.Remove(monitorId);
            }
        }
    }

    private void UpdateStatus(TemplateMonitorStatus status)
    {
        lock (syncRoot)
        {
            statuses[status.MonitorId] = status;
        }

        StatusChanged?.Invoke(this, new TemplateMonitorStatusChangedEventArgs(status));
    }

    private sealed class MonitorSession
    {
        public MonitorSession(
            CancellationTokenSource cancellationTokenSource,
            TaskCompletionSource startedCompletion)
        {
            CancellationTokenSource = cancellationTokenSource;
            StartedCompletion = startedCompletion;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public TaskCompletionSource StartedCompletion { get; }

        public Task Task { get; set; } = Task.CompletedTask;
    }

    internal sealed class TemplateMonitorMetricsAccumulator
    {
        private long matchedFrameCount;
        private long notMatchedFrameCount;
        private long errorFrameCount;
        private TimeSpan captureDurationTotal;
        private TimeSpan matchingDurationTotal;
        private TimeSpan publishDurationTotal;
        private TimeSpan visualizationDurationTotal;
        private TimeSpan frameDurationTotal;
        private TimeSpan maxFrameDuration;
        private TemplateMatchFrameMetrics? lastFrameMetrics;

        public void Record(TemplateMatchResult result, TemplateMatchFrameMetrics metrics)
        {
            switch (result.Status)
            {
                case TemplateMatchStatus.Matched:
                    matchedFrameCount++;
                    break;
                case TemplateMatchStatus.NotMatched:
                    notMatchedFrameCount++;
                    break;
                default:
                    errorFrameCount++;
                    break;
            }

            captureDurationTotal += metrics.CaptureDuration;
            matchingDurationTotal += metrics.MatchingDuration;
            publishDurationTotal += metrics.PublishDuration;
            visualizationDurationTotal += metrics.VisualizationDuration;
            frameDurationTotal += metrics.TotalDuration;
            if (metrics.TotalDuration > maxFrameDuration)
            {
                maxFrameDuration = metrics.TotalDuration;
            }

            lastFrameMetrics = metrics;
        }

        public TemplateMonitorMetricsSnapshot Snapshot()
        {
            return new TemplateMonitorMetricsSnapshot(
                matchedFrameCount,
                notMatchedFrameCount,
                errorFrameCount,
                captureDurationTotal,
                matchingDurationTotal,
                publishDurationTotal,
                visualizationDurationTotal,
                frameDurationTotal,
                maxFrameDuration,
                lastFrameMetrics);
        }
    }
}
