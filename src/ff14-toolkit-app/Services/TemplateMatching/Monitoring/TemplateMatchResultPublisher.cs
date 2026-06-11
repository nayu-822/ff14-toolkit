namespace FF14Toolkit.App.Services.TemplateMatching.Monitoring;

public sealed class TemplateMatchResultPublisher : ITemplateMatchResultSink
{
    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, TemplateMatchResult> latestResults = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<TemplateMatchResultPublishedEventArgs>? ResultPublished;

    public ValueTask PublishAsync(
        string monitorId,
        TemplateMatchResult result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            latestResults[monitorId] = result;
        }

        ResultPublished?.Invoke(this, new TemplateMatchResultPublishedEventArgs(monitorId, result));
        return ValueTask.CompletedTask;
    }

    public bool TryGetLatestResult(string monitorId, out TemplateMatchResult? result)
    {
        lock (syncRoot)
        {
            if (latestResults.TryGetValue(monitorId, out TemplateMatchResult? latest))
            {
                result = latest;
                return true;
            }
        }

        result = null;
        return false;
    }
}

public sealed class TemplateMatchResultPublishedEventArgs : EventArgs
{
    public TemplateMatchResultPublishedEventArgs(string monitorId, TemplateMatchResult result)
    {
        MonitorId = monitorId;
        Result = result;
    }

    public string MonitorId { get; }

    public TemplateMatchResult Result { get; }
}
