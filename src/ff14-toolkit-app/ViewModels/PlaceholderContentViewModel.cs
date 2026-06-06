using FF14Toolkit.App.Services.Localization;

namespace FF14Toolkit.App.ViewModels;

public sealed class PlaceholderContentViewModel : ShellContentViewModel
{
    private readonly ILocalizationService localizationService;

    public PlaceholderContentViewModel(
        string sectionKey,
        string titleKey,
        string descriptionKey,
        string primaryTitleKey,
        string primaryBodyKey,
        string secondaryTitleKey,
        string secondaryBodyKey,
        ILocalizationService localizationService)
        : base(sectionKey, titleKey, descriptionKey, localizationService)
    {
        PrimaryTitleKey = primaryTitleKey;
        PrimaryBodyKey = primaryBodyKey;
        SecondaryTitleKey = secondaryTitleKey;
        SecondaryBodyKey = secondaryBodyKey;
        this.localizationService = localizationService;
    }

    public string PrimaryTitleKey { get; }

    public string PrimaryBodyKey { get; }

    public string SecondaryTitleKey { get; }

    public string SecondaryBodyKey { get; }

    public string PrimaryTitle => localizationService[PrimaryTitleKey];

    public string PrimaryBody => localizationService[PrimaryBodyKey];

    public string SecondaryTitle => localizationService[SecondaryTitleKey];

    public string SecondaryBody => localizationService[SecondaryBodyKey];

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(PrimaryTitle));
        OnPropertyChanged(nameof(PrimaryBody));
        OnPropertyChanged(nameof(SecondaryTitle));
        OnPropertyChanged(nameof(SecondaryBody));
    }
}
