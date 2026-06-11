namespace FF14Toolkit.App.Models.Configuration;

public sealed class OverlayPluginOptions
{
    public string WebSocketUri { get; set; } = "ws://127.0.0.1:10501/ws";

    public int RequestTimeoutSeconds { get; set; } = 3;
}
