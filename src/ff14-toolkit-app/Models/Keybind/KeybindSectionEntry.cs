namespace FF14Toolkit.App.Models.Keybind;

public sealed class KeybindSectionEntry
{
    public int Index { get; init; }

    public int Offset { get; init; }

    public byte Tag { get; init; }

    public char TagCharacter => (char)Tag;

    public string TagDisplay => char.IsControl((char)Tag)
        ? $"0x{Tag:X2}"
        : ((char)Tag).ToString();

    public ushort DeclaredSize { get; init; }

    public int PayloadLength { get; init; }

    public string Content { get; init; } = string.Empty;

    public byte[] PayloadBytes { get; init; } = [];
}
