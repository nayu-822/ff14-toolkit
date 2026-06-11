namespace FF14Toolkit.App.Services.TemplateMatching;

public interface ITemplateResourceLoader
{
    TemplateResource Load(TemplateResourceDefinition definition);
}

public interface IScreenCaptureService
{
    ScreenCaptureFrame Capture(System.Drawing.Rectangle screenBounds);
}

public interface ITemplateMatcher
{
    TemplateMatchResult Match(
        ScreenCaptureFrame capture,
        TemplateResource template,
        TemplateMatchRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITemplateMatchExecutor
{
    Task<TemplateMatchExecutionResult> ExecuteAsync(
        TemplateMatchExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITemplateMatchMonitor
{
    Task StartAsync(
        TemplateMonitorDefinition definition,
        CancellationToken cancellationToken = default);

    Task StopAsync(
        string monitorId,
        CancellationToken cancellationToken = default);
}

public interface ITemplateMatchDebugVisualizer
{
    Task ShowAsync(
        TemplateMatchDebugFrame frame,
        CancellationToken cancellationToken = default);

    Task HideAsync(
        string monitorId,
        CancellationToken cancellationToken = default);
}

public interface ITemplateMatchResultSink
{
    ValueTask PublishAsync(
        string monitorId,
        TemplateMatchResult result,
        CancellationToken cancellationToken = default);
}

public interface ITemplateMonitorStatusSource
{
    event EventHandler<TemplateMonitorStatusChangedEventArgs>? StatusChanged;

    TemplateMonitorStatus? GetStatus(string monitorId);
}
