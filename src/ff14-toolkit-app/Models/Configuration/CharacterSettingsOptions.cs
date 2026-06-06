namespace FF14Toolkit.App.Models.Configuration;

public sealed class CharacterSettingsOptions
{
    public Guid? SelectedProfileId { get; set; }

    public List<CharacterProfile> Profiles { get; set; } = [];
}
