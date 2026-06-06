namespace FF14Toolkit.App.Models.Configuration;

public sealed class CharacterSettingsState
{
    public Guid? SelectedProfileId { get; set; }

    public List<CharacterProfile> Profiles { get; set; } = [];
}
