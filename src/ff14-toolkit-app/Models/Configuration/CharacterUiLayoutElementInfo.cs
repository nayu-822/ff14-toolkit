namespace FF14Toolkit.App.Models.Configuration;

public sealed class CharacterUiLayoutElementInfo
{
    public string ElementId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    public float Scale { get; set; }

    public ushort Width { get; set; }

    public ushort Height { get; set; }
}
