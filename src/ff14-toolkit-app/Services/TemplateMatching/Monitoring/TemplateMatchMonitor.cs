using FF14Toolkit.App.Services.Crafting;

namespace FF14Toolkit.App.Services.TemplateMatching.Monitoring;

public sealed class TemplateMatchMonitor : ITemplateMatchMonitor
{
    private readonly IScreenCaptureService screenCaptureService;
    private readonly ITemplateResourceLoader templateResourceLoader;
    private readonly ITemplateMatcher templateMatcher;
    private readonly ITemplateMatchResultSink resultSink;
    private readonly ITemplateMatchDebugVisualizer debugVisualizer;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, MonitorSession> sessions = new(StringComparer.OrdinalIgnoreCase);

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

    public Task StartAsync(
        TemplateMonitorDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (definition.Interval < TimeSpan.FromMilliseconds(50))
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Template monitor interval must be 50ms or greater.");
        }

        lock (syncRoot)
        {
            if (sessions.ContainsKey(definition.MonitorId))
            {
                throw new InvalidOperationException($"Template monitor already exists: {definition.MonitorId}");
            }

            CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task task = Task.Run(() => RunAsync(definition, cancellationTokenSource.Token), cancellationTokenSource.Token);
            sessions.Add(definition.MonitorId, new MonitorSession(cancellationTokenSource, task));
        }

        logger.LogInformation($"Template monitor started: {definition.MonitorId}");
        return Task.CompletedTask;
    }

    public async Task StopAsync(
        string monitorId,
        CancellationToken cancellationToken = default)
    {
        MonitorSession? session;
        lock (syncRoot)
        {
            sessions.TryGetValue(monitorId, out session);
            if (session is not null)
            {
                sessions.Remove(monitorId);
            }
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
        finally
        {
            session.CancellationTokenSource.Dispose();
            await debugVisualizer.HideAsync(monitorId, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"Template monitor stopped: {monitorId}");
        }
    }

    private async Task RunAsync(TemplateMonitorDefinition definition, CancellationToken cancellationToken)
    {
        TemplateResource resource;

        try
        {
            resource = templateResourceLoader.Load(definition.Resource);
            logger.LogInformation($"Template resource loaded: {definition.Resource.TemplateId}");
        }
        catch (Exception exception)
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

            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            TemplateMatchResult result;

            try
            {
                using ScreenCaptureFrame capture = screenCaptureService.Capture(definition.MatchRequest.CaptureBounds);
                result = templateMatcher.Match(capture, resource, definition.MatchRequest, cancellationToken);
                if (definition.EnableDebugVisualization && definition.DebugViewMode != TemplateMatchDebugViewMode.None)
                {
                    await debugVisualizer.ShowAsync(
                            CreateDebugFrame(definition, capture, result),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
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
                    System.Diagnostics.Stopwatch.GetElapsedTime(startedAt),
                    DateTimeOffset.Now,
                    exception.Message);
            }

            await resultSink.PublishAsync(definition.MonitorId, result, cancellationToken).ConfigureAwait(false);

            logger.LogDebug(
                $"Frame processed. MonitorId={definition.MonitorId}, Status={result.Status}, BestScore={result.BestScore:F3}, Threshold={result.Threshold:F3}, Scale={result.Scale:F2}, ProcessingTime={result.ProcessingTime.TotalMilliseconds:F1}ms");

            TimeSpan remaining = definition.Interval - System.Diagnostics.Stopwatch.GetElapsedTime(startedAt);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static TemplateMatchDebugFrame CreateDebugFrame(
        TemplateMonitorDefinition definition,
        ScreenCaptureFrame capture,
        TemplateMatchResult result)
    {
        TemplateMatchCapturePreview preview = new(
            capture.ScreenBounds,
            capture.Width,
            capture.Height,
            capture.Stride,
            [.. capture.Pixels]);

        return new TemplateMatchDebugFrame(
            definition.MonitorId,
            definition.TargetName,
            result,
            definition.DebugViewMode,
            preview);
    }

    private sealed record MonitorSession(CancellationTokenSource CancellationTokenSource, Task Task);
}
