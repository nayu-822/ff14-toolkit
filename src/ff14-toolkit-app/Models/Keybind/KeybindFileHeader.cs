namespace FF14Toolkit.App.Models.Keybind;

public sealed class KeybindFileHeader
{
    public int HeaderSize { get; init; }

    public uint FileSize { get; init; }

    public uint DataSize { get; init; }

    public byte[] UnknownPrefixBytes { get; init; } = [];

    public byte[] UnknownSuffixBytes { get; init; } = [];

    public byte[] RawBytes { get; init; } = [];

    public string HexText => Convert.ToHexString(RawBytes);
}
