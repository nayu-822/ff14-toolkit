using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Localization;
using Microsoft.Extensions.Options;

namespace FF14Toolkit.App.ViewModels;

public sealed class SettingsContentViewModel : ShellContentViewModel
{
    private readonly CacheOptions cacheOptions;
    private readonly ILocalizationService localizationService;

    public SettingsContentViewModel(
        IOptions<CacheOptions> cacheOptions,
        ILocalizationService localizationService)
        : base("settings", "Nav_Settings", "Section_Settings_Description", localizationService)
    {
        this.cacheOptions = cacheOptions.Value;
        this.localizationService = localizationService;
    }

    public string LanguageCardTitle => localizationService["Settings_LanguageCardTitle"];

    public string LanguageCardDescription => localizationService["Settings_LanguageCardDescription"];

    public string CachePathTitle => localizationService["Settings_CachePathTitle"];

    public string CachePathDescription => localizationService["Settings_CachePathDescription"];

    public string CacheRootPath => cacheOptions.RootPath;

    public string CacheIconsPath => cacheOptions.IconsPath;

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(LanguageCardTitle));
        OnPropertyChanged(nameof(LanguageCardDescription));
        OnPropertyChanged(nameof(CachePathTitle));
        OnPropertyChanged(nameof(CachePathDescription));
    }
}
