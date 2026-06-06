using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Localization;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Localization;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace FF14Toolkit.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ILocalizationService localizationService;
    private readonly Dictionary<string, ShellContentViewModel> contentBySectionKey;
    private UiLanguageOption? selectedLanguage;
    private ShellNavigationItemViewModel? selectedNavigationItem;
    private ShellContentViewModel? currentContentViewModel;

    public MainWindowViewModel(
        ILocalizationService localizationService,
        IOptions<CacheOptions> cacheOptions)
    {
        this.localizationService = localizationService;
        this.localizationService.PropertyChanged += OnLocalizationPropertyChanged;

        NavigationItems =
        [
            CreateNavigationItem("overview", "Nav_Overview", "Section_Overview_Description"),
            CreateNavigationItem("hotbar", "Nav_Hotbar", "Section_Hotbar_Description"),
            CreateNavigationItem("keybind", "Nav_Keybind", "Section_Keybind_Description"),
            CreateNavigationItem("crafting", "Nav_Crafting", "Section_Crafting_Description"),
            CreateNavigationItem("icons", "Nav_Icons", "Section_Icons_Description"),
            CreateNavigationItem("settings", "Nav_Settings", "Section_Settings_Description")
        ];

        contentBySectionKey = CreateContentMap(cacheOptions);

        SyncSelectedLanguage();
        SelectedNavigationItem = NavigationItems.FirstOrDefault();
    }

    public ObservableCollection<ShellNavigationItemViewModel> NavigationItems { get; }

    public IReadOnlyList<UiLanguageOption> SupportedLanguages => localizationService.SupportedLanguages;

    public ShellContentViewModel? CurrentContentViewModel
    {
        get => currentContentViewModel;
        private set => SetProperty(ref currentContentViewModel, value);
    }

    public UiLanguageOption? SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (!SetProperty(ref selectedLanguage, value) || value is null)
            {
                return;
            }

            localizationService.SetCulture(value.CultureName);
        }
    }

    public ShellNavigationItemViewModel? SelectedNavigationItem
    {
        get => selectedNavigationItem;
        set
        {
            if (!SetProperty(ref selectedNavigationItem, value) || value is null)
            {
                return;
            }

            CurrentContentViewModel = contentBySectionKey.GetValueOrDefault(value.SectionKey);
        }
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ILocalizationService.SupportedLanguages))
        {
            OnPropertyChanged(nameof(SupportedLanguages));
        }

        if (e.PropertyName is nameof(ILocalizationService.CurrentCulture) or nameof(ILocalizationService.CurrentCultureName))
        {
            SyncSelectedLanguage();
        }
    }

    private void SyncSelectedLanguage()
    {
        selectedLanguage = SupportedLanguages.FirstOrDefault(language =>
            language.CultureName.Equals(localizationService.CurrentCultureName, StringComparison.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(SelectedLanguage));
    }

    private ShellNavigationItemViewModel CreateNavigationItem(string sectionKey, string titleKey, string descriptionKey)
    {
        return new ShellNavigationItemViewModel(sectionKey, titleKey, descriptionKey, localizationService);
    }

    private Dictionary<string, ShellContentViewModel> CreateContentMap(IOptions<CacheOptions> cacheOptions)
    {
        return new Dictionary<string, ShellContentViewModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["overview"] = new PlaceholderContentViewModel(
                "overview",
                "Nav_Overview",
                "Section_Overview_Description",
                "Overview_PrimaryTitle",
                "Overview_PrimaryBody",
                "Overview_SecondaryTitle",
                "Overview_SecondaryBody",
                localizationService),
            ["hotbar"] = new PlaceholderContentViewModel(
                "hotbar",
                "Nav_Hotbar",
                "Section_Hotbar_Description",
                "Hotbar_PrimaryTitle",
                "Hotbar_PrimaryBody",
                "Hotbar_SecondaryTitle",
                "Hotbar_SecondaryBody",
                localizationService),
            ["keybind"] = new PlaceholderContentViewModel(
                "keybind",
                "Nav_Keybind",
                "Section_Keybind_Description",
                "Keybind_PrimaryTitle",
                "Keybind_PrimaryBody",
                "Keybind_SecondaryTitle",
                "Keybind_SecondaryBody",
                localizationService),
            ["crafting"] = new PlaceholderContentViewModel(
                "crafting",
                "Nav_Crafting",
                "Section_Crafting_Description",
                "Crafting_PrimaryTitle",
                "Crafting_PrimaryBody",
                "Crafting_SecondaryTitle",
                "Crafting_SecondaryBody",
                localizationService),
            ["icons"] = new PlaceholderContentViewModel(
                "icons",
                "Nav_Icons",
                "Section_Icons_Description",
                "Icons_PrimaryTitle",
                "Icons_PrimaryBody",
                "Icons_SecondaryTitle",
                "Icons_SecondaryBody",
                localizationService),
            ["settings"] = new SettingsContentViewModel(cacheOptions, localizationService)
        };
    }
}
