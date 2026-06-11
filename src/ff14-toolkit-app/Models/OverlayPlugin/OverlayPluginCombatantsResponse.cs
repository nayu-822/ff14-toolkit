using System.Text.Json.Serialization;

namespace FF14Toolkit.App.Models.OverlayPlugin;

public sealed class OverlayPluginCombatantsResponse
{
    [JsonPropertyName("combatants")]
    public List<OverlayPluginCombatant> Combatants { get; set; } = [];

    [JsonPropertyName("rseq")]
    public long? Sequence { get; set; }
}
