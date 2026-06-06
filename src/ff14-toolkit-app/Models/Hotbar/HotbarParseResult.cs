using System.Collections.Generic;

namespace FF14Toolkit.App.Models.Hotbar;

public sealed class HotbarParseResult
{
    public required HotbarFileHeader Header { get; init; }

    public required IReadOnlyList<HotbarSlotEntry> Entries { get; init; }
}
