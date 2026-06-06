using System.Collections.Generic;

namespace FF14Toolkit.App.Models.Keybind;

public sealed class KeybindParseResult
{
    public KeybindFileHeader Header { get; init; } = new();

    public byte[] DecodedBody { get; init; } = [];

    public IReadOnlyList<KeybindSectionEntry> Sections { get; init; } = [];

    public IReadOnlyList<KeybindEntry> Entries { get; init; } = [];

    public byte[] PaddingBytes { get; init; } = [];
}
