namespace FF14Toolkit.App.Models.Hotbar;

public sealed class HotbarFileHeader
{
    public required int HeaderSize { get; init; }

    public required byte[] RawBytes { get; init; }

    public string HexText => Convert.ToHexString(RawBytes);
}
