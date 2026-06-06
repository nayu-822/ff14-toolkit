using FF14Toolkit.App.Services.Localization;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class OverlayWindowViewModel : ViewModelBase
{
    private readonly ILocalizationService localizationService;
    private readonly string titleKey;
    private bool isEditMode;
    private string modeHint;

    public OverlayWindowViewModel(ILocalizationService localizationService, string titleKey)
    {
        this.localizationService = localizationService;
        this.titleKey = titleKey;
        modeHint = localizationService["Overlay_ModeNormal"];
        this.localizationService.PropertyChanged += OnLocalizationPropertyChanged;
    }

    public string Title => localizationService[titleKey];

    public string ModeHint
    {
        get => modeHint;
        private set => SetProperty(ref modeHint, value);
    }

    public void SetEditMode(bool isEditMode)
    {
        this.isEditMode = isEditMode;
        UpdateModeHint();
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ILocalizationService.CurrentCulture)
            or nameof(ILocalizationService.CurrentCultureName)
            or "Item[]")
        {
            OnPropertyChanged(nameof(Title));
            UpdateModeHint();
        }
    }

    private void UpdateModeHint()
    {
        ModeHint = isEditMode
            ? localizationService["Overlay_ModeEdit"]
            : localizationService["Overlay_ModeNormal"];
    }
}
