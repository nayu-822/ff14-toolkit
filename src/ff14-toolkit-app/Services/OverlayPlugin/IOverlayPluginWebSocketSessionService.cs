namespace FF14Toolkit.App.Services.OverlayPlugin;

public interface IOverlayPluginWebSocketSessionService
{
    event EventHandler<OverlayPluginEventReceivedEventArgs>? EventReceived;
    event EventHandler? ConnectionStateChanged;

    bool IsStarted { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SubscribeAsync(IEnumerable<string> events, CancellationToken cancellationToken = default);

    Task<string> SendRequestAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);
}
