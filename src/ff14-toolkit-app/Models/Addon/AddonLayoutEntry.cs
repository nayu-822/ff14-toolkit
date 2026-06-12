namespace FF14Toolkit.App.Models.Addon;

public sealed class AddonLayoutEntry
{
    public uint AddonNameHash { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Scale { get; set; }

    public uint ElementFlags { get; set; }

    public ushort Width { get; set; }

    public ushort Height { get; set; }

    public byte StateByte1 { get; set; }

    public byte StateByte2 { get; set; }

    public byte StateByte3 { get; set; }

    public byte Alpha { get; set; }

    public byte StateByte4 { get; set; }

    public byte StateByte5 { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int Area => Width * Height;
}
