namespace FF14Toolkit.App.Services.OverlayPlugin;

public sealed class OverlayPluginEventReceivedEventArgs : EventArgs
{
    public required string EventType { get; init; }

    public required string RawJson { get; init; }
}
