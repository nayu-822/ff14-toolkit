namespace FF14Toolkit.App.Models.Configuration;

public sealed class CharacterProfile
{
    public Guid ProfileId { get; set; } = Guid.NewGuid();

    public string CharacterName { get; set; } = string.Empty;

    public string WorldName { get; set; } = string.Empty;

    public string RootPath { get; set; } = string.Empty;

    public string DisplayLabel => string.IsNullOrWhiteSpace(WorldName)
        ? CharacterName
        : $"{CharacterName} ({WorldName})";
}
