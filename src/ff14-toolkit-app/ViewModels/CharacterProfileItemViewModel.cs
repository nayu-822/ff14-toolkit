using FF14Toolkit.App.Models.Configuration;

namespace FF14Toolkit.App.ViewModels;

public sealed class CharacterProfileItemViewModel : ViewModelBase
{
    public CharacterProfileItemViewModel(CharacterProfile profile)
    {
        ProfileId = profile.ProfileId;
        CharacterName = profile.CharacterName;
        WorldName = profile.WorldName;
        RootPath = profile.RootPath;
        UiLayoutInfo = profile.UiLayoutInfo;
    }

    public Guid ProfileId { get; }

    public string CharacterName { get; }

    public string WorldName { get; }

    public string RootPath { get; }

    public CharacterUiLayoutInfo? UiLayoutInfo { get; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(WorldName)
        ? CharacterName
        : $"{CharacterName} ({WorldName})";
}
