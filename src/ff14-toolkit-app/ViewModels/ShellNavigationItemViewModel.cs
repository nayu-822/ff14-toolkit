using FF14Toolkit.App.Services.Localization;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class ShellNavigationItemViewModel : ViewModelBase
{
    private readonly ILocalizationService localizationService;

    public ShellNavigationItemViewModel(
        string sectionKey,
        string titleKey,
        string descriptionKey,
        ILocalizationService localizationService)
    {
        SectionKey = sectionKey;
        TitleKey = titleKey;
        DescriptionKey = descriptionKey;
        this.localizationService = localizationService;
        this.localizationService.PropertyChanged += OnLocalizationPropertyChanged;
    }

    public string SectionKey { get; }

    public string TitleKey { get; }

    public string DescriptionKey { get; }

    public string DisplayName => localizationService[TitleKey];

    public string Description => localizationService[DescriptionKey];

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ILocalizationService.CurrentCulture)
            or nameof(ILocalizationService.CurrentCultureName)
            or "Item[]")
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Description));
        }
    }
}
