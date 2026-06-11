using FF14Toolkit.App.Services.Crafting;

namespace FF14Toolkit.App.Services.TemplateMatching.Monitoring;

public sealed class TemplateMatchMonitor : ITemplateMatchMonitor, ITemplateMonitorStatusSource
{
    private readonly IScreenCaptureService screenCaptureService;
    private readonly ITemplateResourceLoader templateResourceLoader;
    private readonly ITemplateMatcher templateMatcher;
    private readonly ITemplateMatchResultSink resultSink;
    private readonly ITemplateMatchDebugVisualizer debugVisualizer;
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
        CraftSequenceHotkeyLogService logger)
    {
        this.screenCaptureService = screenCaptureService;
        this.templateResourceLoader = templateResourceLoader;
        this.templateMatcher = templateMatcher;
        this.resultSink = resultSink;
        this.debugVisualizer = debugVisualizer;
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
            null));

        session.Task = Task.Run(() => RunAsync(definition, session, session.CancellationTokenSource.Token), CancellationToken.None);

        logger.LogInformation($"Template debug visualization: MonitorId={definition.MonitorId} Requested={definition.EnableDebugVisualization} ViewMode={definition.DebugViewMode} GlobalEnabled=<configured-in-visualizer>");
        logger.LogInformation($"Template monitor started: {definition.MonitorId}");
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
        MonitorSession session,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        bool completedBySelf = false;
        long processedFrameCount = 0;
        DateTimeOffset? startedAt = null;
        DateTimeOffset? firstFrameCompletedAt = null;
        TemplateResource resource = null!;

        try
        {
            resource = templateResourceLoader.Load(definition.Resource);
            logger.LogInformation($"Template resource loaded: {definition.Resource.TemplateId}");

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
                null));

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

            session.StartedCompletion.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.StartedCompletion.TrySetCanceled(cancellationToken);
            return;
        }
        catch (Exception exception)
        {
            failure = exception;
            session.StartedCompletion.TrySetException(exception);
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
            await resultSink.PublishAsync(definition.MonitorId, loadFailedResult, cancellationToken).ConfigureAwait(false);
            if (definition.EnableDebugVisualization)
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

            UpdateStatus(new TemplateMonitorStatus(
                definition.MonitorId,
                TemplateMonitorState.Faulted,
                GetStatus(definition.MonitorId)?.StartRequestedAt,
                startedAt,
                firstFrameCompletedAt,
                DateTimeOffset.Now,
                processedFrameCount,
                loadFailedResult.Status,
                loadFailedResult.BestScore,
                loadFailedResult.ErrorMessage));
        }

        try
        {
            if (failure is null)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        long frameStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                        TemplateMatchResult result;
                        TemplateMatchCapturePreview? preview = null;

                        try
                        {
                            using ScreenCaptureFrame capture = screenCaptureService.Capture(definition.MatchRequest.CaptureBounds);
                            result = templateMatcher.Match(capture, resource, definition.MatchRequest, cancellationToken);
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
                                System.Diagnostics.Stopwatch.GetElapsedTime(frameStartedAt),
                                DateTimeOffset.Now,
                                exception.Message);
                        }

                        processedFrameCount++;
                        firstFrameCompletedAt ??= DateTimeOffset.Now;

                        await resultSink.PublishAsync(definition.MonitorId, result, cancellationToken).ConfigureAwait(false);

                        if (definition.EnableDebugVisualization && definition.DebugViewMode != TemplateMatchDebugViewMode.None)
                        {
                            await debugVisualizer.ShowAsync(
                                    new TemplateMatchDebugFrame(
                                        definition.MonitorId,
                                        definition.TargetName,
                                        result,
                                        definition.DebugViewMode,
                                        preview),
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }

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
                            result.ErrorMessage));

                        logger.LogDebug(
                            $"Frame processed. MonitorId={definition.MonitorId}, Status={result.Status}, BestScore={result.BestScore:F3}, Threshold={result.Threshold:F3}, Scale={result.Scale:F2}, ProcessingTime={result.ProcessingTime.TotalMilliseconds:F1}ms");

                        TimeSpan remaining = definition.Interval - System.Diagnostics.Stopwatch.GetElapsedTime(frameStartedAt);
                        if (remaining > TimeSpan.Zero)
                        {
                            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    completedBySelf = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }
        }
        finally
        {
            await debugVisualizer.HideAsync(definition.MonitorId, CancellationToken.None).ConfigureAwait(false);
            session.CancellationTokenSource.Dispose();
            RemoveSessionIfCurrent(definition.MonitorId, session);

            TemplateMonitorStatus? currentStatus = GetStatus(definition.MonitorId);

            TemplateMonitorState finalState = failure is not null
                ? TemplateMonitorState.Faulted
                : cancellationToken.IsCancellationRequested
                    ? TemplateMonitorState.Stopped
                    : completedBySelf
                        ? TemplateMonitorState.Completed
                        : TemplateMonitorState.Stopped;

            UpdateStatus(new TemplateMonitorStatus(
                definition.MonitorId,
                finalState,
                currentStatus?.StartRequestedAt,
                startedAt,
                firstFrameCompletedAt,
                DateTimeOffset.Now,
                processedFrameCount,
                currentStatus?.LastMatchStatus,
                currentStatus?.LastScore,
                failure?.Message ?? currentStatus?.ErrorMessage));

            if (failure is not null)
            {
                logger.LogError($"Template monitor faulted: {definition.MonitorId}", failure);
            }
            else
            {
                logger.LogInformation($"Template monitor stopped: {definition.MonitorId}, State={finalState}, Frames={processedFrameCount}");
            }
        }
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
}
