using System.Text.Json.Serialization;

namespace FF14Toolkit.App.Models.OverlayPlugin;

public sealed class OverlayPluginLanguageInfo
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("languageId")]
    public string LanguageId { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("regionId")]
    public string RegionId { get; set; } = string.Empty;

    [JsonPropertyName("rseq")]
    public long? Sequence { get; set; }
}
