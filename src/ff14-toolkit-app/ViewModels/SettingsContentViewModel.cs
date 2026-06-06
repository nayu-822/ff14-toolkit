using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Infrastructure;
using Microsoft.Extensions.Options;

namespace FF14Toolkit.App.ViewModels;

public sealed class SettingsContentViewModel : ShellContentViewModel
{
    private readonly CacheOptions cacheOptions;
    private readonly CharacterSettingsStore characterSettingsStore;
    private readonly ILocalizationService localizationService;
    private readonly RelayCommand saveCharacterSettingsCommand;
    private string characterSettingsPath;

    public SettingsContentViewModel(
        IOptions<CacheOptions> cacheOptions,
        CharacterSettingsStore characterSettingsStore,
        ILocalizationService localizationService)
        : base("settings", "Nav_Settings", "Section_Settings_Description", localizationService)
    {
        this.cacheOptions = cacheOptions.Value;
        this.characterSettingsStore = characterSettingsStore;
        this.localizationService = localizationService;
        characterSettingsPath = characterSettingsStore.RootPath;
        saveCharacterSettingsCommand = new RelayCommand(SaveCharacterSettings);
    }

    public string LanguageCardTitle => localizationService["Settings_LanguageCardTitle"];

    public string LanguageCardDescription => localizationService["Settings_LanguageCardDescription"];

    public string CachePathTitle => localizationService["Settings_CachePathTitle"];

    public string CachePathDescription => localizationService["Settings_CachePathDescription"];

    public string CacheRootPath => cacheOptions.RootPath;

    public string CacheIconsPath => cacheOptions.IconsPath;

    public string CharacterSettingsCardTitle => "キャラクター設定フォルダ";

    public string CharacterSettingsCardDescription => "HOTBAR.DAT や KEYBIND.DAT が入っているキャラクター設定フォルダのパスを指定します。";

    public string CharacterSettingsPath
    {
        get => characterSettingsPath;
        set => SetProperty(ref characterSettingsPath, value);
    }

    public string CharacterSettingsSaveButtonLabel => "保存";

    public RelayCommand SaveCharacterSettingsCommand => saveCharacterSettingsCommand;

    public string CharacterSettingsCurrentPath => string.IsNullOrWhiteSpace(characterSettingsStore.RootPath)
        ? "未設定"
        : characterSettingsStore.RootPath;

    private void SaveCharacterSettings()
    {
        characterSettingsStore.Save(CharacterSettingsPath);
        CharacterSettingsPath = characterSettingsStore.RootPath;
        OnPropertyChanged(nameof(CharacterSettingsCurrentPath));
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(LanguageCardTitle));
        OnPropertyChanged(nameof(LanguageCardDescription));
        OnPropertyChanged(nameof(CachePathTitle));
        OnPropertyChanged(nameof(CachePathDescription));
        OnPropertyChanged(nameof(CharacterSettingsCardTitle));
        OnPropertyChanged(nameof(CharacterSettingsCardDescription));
        OnPropertyChanged(nameof(CharacterSettingsSaveButtonLabel));
        OnPropertyChanged(nameof(CharacterSettingsCurrentPath));
    }
}
