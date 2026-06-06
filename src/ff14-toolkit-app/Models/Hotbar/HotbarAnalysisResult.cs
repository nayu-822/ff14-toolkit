using System.Collections.Generic;
using System.Text.Json;

namespace FF14Toolkit.App.Models.Hotbar;

public sealed class HotbarAnalysisResult
{
    public required string SourcePath { get; init; }

    public required HotbarParseResult ParseResult { get; init; }

    public required IReadOnlyList<HotbarSlotEntry> NonEmptyEntries { get; init; }

    public required IReadOnlyList<HotbarSlotTypeSummary> SlotTypeSummaries { get; init; }

    public required IReadOnlyList<HotbarGroupSummary> GroupSummaries { get; init; }

    public string ToJson(bool writeIndented = true)
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = writeIndented
        });
    }
}
