using System.Text.Json.Serialization;

namespace FF14Toolkit.App.Models.OverlayPlugin;

public sealed class OverlayPluginCombatant
{
    [JsonPropertyName("ID")]
    public ulong Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Job")]
    public int JobId { get; set; }

    [JsonPropertyName("Level")]
    public int Level { get; set; }

    [JsonPropertyName("CurrentHP")]
    public long CurrentHp { get; set; }

    [JsonPropertyName("MaxHP")]
    public long MaxHp { get; set; }

    [JsonPropertyName("CurrentMP")]
    public long CurrentMp { get; set; }

    [JsonPropertyName("MaxMP")]
    public long MaxMp { get; set; }

    [JsonPropertyName("CurrentGP")]
    public long CurrentGp { get; set; }

    [JsonPropertyName("MaxGP")]
    public long MaxGp { get; set; }

    [JsonPropertyName("CurrentCP")]
    public long CurrentCp { get; set; }

    [JsonPropertyName("MaxCP")]
    public long MaxCp { get; set; }

    [JsonPropertyName("PosX")]
    public double PosX { get; set; }

    [JsonPropertyName("PosY")]
    public double PosY { get; set; }

    [JsonPropertyName("PosZ")]
    public double PosZ { get; set; }

    [JsonPropertyName("Heading")]
    public double Heading { get; set; }

    [JsonPropertyName("WorldID")]
    public int WorldId { get; set; }

    [JsonPropertyName("CurrentWorldID")]
    public int CurrentWorldId { get; set; }

    [JsonPropertyName("WorldName")]
    public string WorldName { get; set; } = string.Empty;

    [JsonPropertyName("Type")]
    public int Type { get; set; }

    [JsonPropertyName("IsTargetable")]
    public bool IsTargetable { get; set; }
}
