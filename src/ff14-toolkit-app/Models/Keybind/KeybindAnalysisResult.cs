using System.Collections.Generic;
using System.Text.Json;

namespace FF14Toolkit.App.Models.Keybind;

public sealed class KeybindAnalysisResult
{
    public required string SourcePath { get; init; }

    public required KeybindParseResult ParseResult { get; init; }

    public required IReadOnlyList<KeybindEntry> AssignedEntries { get; init; }

    public required IReadOnlyList<KeybindCommandPrefixSummary> CommandPrefixSummaries { get; init; }

    public string ToJson(bool writeIndented = true)
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = writeIndented
        });
    }
}
