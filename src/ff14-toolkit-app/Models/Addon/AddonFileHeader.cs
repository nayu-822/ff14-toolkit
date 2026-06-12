namespace FF14Toolkit.App.Models.Addon;

public sealed class AddonFileHeader
{
    public int HeaderSize { get; set; }

    public uint FileSize { get; set; }

    public uint DataSize { get; set; }

    public string Magic { get; set; } = string.Empty;

    public uint Version { get; set; }

    public string DataSetName { get; set; } = string.Empty;

    public byte[] RawBytes { get; set; } = [];
}
