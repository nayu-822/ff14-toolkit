using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.Localization;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Localization;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ILocalizationService localizationService;
    private readonly Dictionary<string, ShellContentViewModel> contentBySectionKey;
    private readonly IReadOnlyList<ShellNavigationItemViewModel> allNavigationItems;
    private readonly CraftActionSequenceListContentViewModel craftActionSequenceListContentViewModel;
    private readonly CraftActionSequenceContentViewModel craftActionSequenceEditorContentViewModel;
    private readonly CraftSequenceHotkeySettingsContentViewModel craftSequenceHotkeySettingsContentViewModel;
    private UiLanguageOption? selectedLanguage;
    private ShellNavigationItemViewModel? selectedNavigationItem;
    private ShellContentViewModel? currentContentViewModel;

    public MainWindowViewModel(
        ILocalizationService localizationService,
        IOptions<CacheOptions> cacheOptions,
        CharacterSettingsStore characterSettingsStore,
        HotkeySettingsStore hotkeySettingsStore,
        IGameDataService gameDataService,
        CraftActionSequenceStore craftActionSequenceStore,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        CraftSequenceHotkeyActivityState craftSequenceHotkeyActivityState,
        CraftSequenceHotkeyRegistrationState craftSequenceHotkeyRegistrationState)
    {
        this.localizationService = localizationService;
        this.localizationService.PropertyChanged += OnLocalizationPropertyChanged;

        craftActionSequenceListContentViewModel = new CraftActionSequenceListContentViewModel(
            localizationService,
            craftActionSequenceStore,
            () => OpenCraftActionSequenceEditor(null),
            sequenceId => OpenCraftActionSequenceEditor(sequenceId));
        craftActionSequenceEditorContentViewModel = new CraftActionSequenceContentViewModel(
            localizationService,
            gameDataService,
            craftActionSequenceStore,
            ShowCraftActionSequenceList);
        craftSequenceHotkeySettingsContentViewModel = new CraftSequenceHotkeySettingsContentViewModel(
            localizationService,
            craftSequenceHotkeyActivityState,
            craftActionSequenceStore,
            craftSequenceHotkeyStore,
            craftSequenceHotkeyRegistrationState);

        contentBySectionKey = CreateContentMap(
            cacheOptions,
            characterSettingsStore,
            hotkeySettingsStore,
            craftActionSequenceListContentViewModel,
            craftActionSequenceEditorContentViewModel,
            craftSequenceHotkeySettingsContentViewModel);

        NavigationItems = new ObservableCollection<ShellNavigationItemViewModel>(CreateNavigationItems());
        allNavigationItems = FlattenNavigationItems(NavigationItems).ToArray();

        SyncSelectedLanguage();
        ShowContent("overview");
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
        private set => SetProperty(ref selectedNavigationItem, value);
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

    private IReadOnlyList<ShellNavigationItemViewModel> CreateNavigationItems()
    {
        ShellNavigationItemViewModel craftingSequences = CreateNavigationItem(
            "crafting-sequences",
            "Nav_CraftingSequences",
            "Section_CraftingSequences_Description");
        ShellNavigationItemViewModel craftingSequenceHotkeys = CreateNavigationItem(
            "crafting-sequence-hotkeys",
            "Nav_CraftingSequenceHotkeys",
            "Section_CraftingSequenceHotkeys_Description");

        ShellNavigationItemViewModel crafting = CreateNavigationItem(
            null,
            "Nav_Crafting",
            "Section_Crafting_Description",
            isSelectable: false,
            children: [craftingSequences, craftingSequenceHotkeys]);

        return
        [
            CreateNavigationItem("overview", "Nav_Overview", "Section_Overview_Description"),
            CreateNavigationItem("hotbar", "Nav_Hotbar", "Section_Hotbar_Description"),
            CreateNavigationItem("keybind", "Nav_Keybind", "Section_Keybind_Description"),
            crafting,
            CreateNavigationItem("icons", "Nav_Icons", "Section_Icons_Description"),
            CreateNavigationItem("settings", "Nav_Settings", "Section_Settings_Description")
        ];
    }

    private ShellNavigationItemViewModel CreateNavigationItem(
        string? sectionKey,
        string titleKey,
        string descriptionKey,
        bool isSelectable = true,
        IEnumerable<ShellNavigationItemViewModel>? children = null)
    {
        return new ShellNavigationItemViewModel(
            sectionKey,
            titleKey,
            descriptionKey,
            localizationService,
            HandleNavigationInvoked,
            isSelectable,
            children);
    }

    private void HandleNavigationInvoked(ShellNavigationItemViewModel item)
    {
        if (!item.IsSelectable || string.IsNullOrWhiteSpace(item.SectionKey))
        {
            return;
        }

        ShowContent(item.SectionKey, item);
    }

    private void ShowContent(string sectionKey, ShellNavigationItemViewModel? selectedItem = null)
    {
        if (!contentBySectionKey.TryGetValue(sectionKey, out ShellContentViewModel? contentViewModel))
        {
            return;
        }

        CurrentContentViewModel = contentViewModel;

        ShellNavigationItemViewModel? nextSelectedItem = selectedItem
            ?? allNavigationItems.FirstOrDefault(item => string.Equals(item.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase));

        UpdateSelection(nextSelectedItem);
        InitializeCurrentContent();
    }

    private void UpdateSelection(ShellNavigationItemViewModel? nextSelectedItem)
    {
        foreach (ShellNavigationItemViewModel navigationItem in allNavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, nextSelectedItem);
        }

        SelectedNavigationItem = nextSelectedItem;
        nextSelectedItem?.ExpandAncestors();
    }

    private void OpenCraftActionSequenceEditor(Guid? sequenceId)
    {
        if (sequenceId is Guid existingSequenceId)
        {
            craftActionSequenceEditorContentViewModel.BeginEdit(existingSequenceId);
        }
        else
        {
            craftActionSequenceEditorContentViewModel.BeginCreate();
        }

        CurrentContentViewModel = craftActionSequenceEditorContentViewModel;
        InitializeCurrentContent();
    }

    private void ShowCraftActionSequenceList()
    {
        ShowContent("crafting-sequences");
    }

    private async void InitializeCurrentContent()
    {
        if (CurrentContentViewModel is not CraftActionSequenceContentViewModel craftingContentViewModel)
        {
            return;
        }

        await craftingContentViewModel.InitializeAsync();
    }

    private Dictionary<string, ShellContentViewModel> CreateContentMap(
        IOptions<CacheOptions> cacheOptions,
        CharacterSettingsStore characterSettingsStore,
        HotkeySettingsStore hotkeySettingsStore,
        CraftActionSequenceListContentViewModel craftActionSequenceListContentViewModel,
        CraftActionSequenceContentViewModel craftActionSequenceEditorContentViewModel,
        CraftSequenceHotkeySettingsContentViewModel craftSequenceHotkeySettingsContentViewModel)
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
            ["crafting-sequences"] = craftActionSequenceListContentViewModel,
            ["crafting-sequence-editor"] = craftActionSequenceEditorContentViewModel,
            ["crafting-sequence-hotkeys"] = craftSequenceHotkeySettingsContentViewModel,
            ["icons"] = new PlaceholderContentViewModel(
                "icons",
                "Nav_Icons",
                "Section_Icons_Description",
                "Icons_PrimaryTitle",
                "Icons_PrimaryBody",
                "Icons_SecondaryTitle",
                "Icons_SecondaryBody",
                localizationService),
            ["settings"] = new SettingsContentViewModel(
                cacheOptions,
                characterSettingsStore,
                hotkeySettingsStore,
                localizationService)
        };
    }

    private static IEnumerable<ShellNavigationItemViewModel> FlattenNavigationItems(IEnumerable<ShellNavigationItemViewModel> items)
    {
        foreach (ShellNavigationItemViewModel item in items)
        {
            yield return item;

            foreach (ShellNavigationItemViewModel child in FlattenNavigationItems(item.Children))
            {
                yield return child;
            }
        }
    }
}
