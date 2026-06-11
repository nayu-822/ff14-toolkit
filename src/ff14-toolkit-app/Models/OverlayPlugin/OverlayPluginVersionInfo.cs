using System.Text.Json.Serialization;

namespace FF14Toolkit.App.Models.OverlayPlugin;

public sealed class OverlayPluginVersionInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("rseq")]
    public long? Sequence { get; set; }
}
