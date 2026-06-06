using FF14Toolkit.App.Services.Localization;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public abstract class ShellContentViewModel : ViewModelBase
{
    private readonly ILocalizationService localizationService;

    protected ShellContentViewModel(
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

    protected string TitleKey { get; }

    protected string DescriptionKey { get; }

    public virtual string DisplayName => localizationService[TitleKey];

    public virtual string Description => localizationService[DescriptionKey];

    public virtual string Badge => localizationService[$"SectionBadge_{SectionKey}"];

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ILocalizationService.CurrentCulture)
            or nameof(ILocalizationService.CurrentCultureName)
            or "Item[]")
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Badge));
            OnLocalized();
        }
    }

    protected virtual void OnLocalized()
    {
    }
}
