using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Localization;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FF14Toolkit.App.ViewModels;

public sealed class SettingsContentViewModel : ShellContentViewModel
{
    private readonly CacheOptions cacheOptions;
    private readonly CharacterSettingsStore characterSettingsStore;
    private readonly HotkeySettingsStore hotkeySettingsStore;
    private readonly ILocalizationService localizationService;
    private readonly RelayCommand saveCharacterCommand;
    private readonly RelayCommand beginCreateCharacterCommand;
    private readonly RelayCommand<CharacterProfileItemViewModel> editCharacterCommand;
    private readonly RelayCommand<CharacterProfileItemViewModel> deleteCharacterCommand;
    private readonly RelayCommand saveHotkeysCommand;
    private readonly RelayCommand openCacheRootPathCommand;

    private CharacterProfileItemViewModel? selectedCharacter;
    private Guid? editingCharacterId;
    private string characterName = string.Empty;
    private string worldName = string.Empty;
    private string characterSettingsPath = string.Empty;
    private string toggleOverlayHotKey = string.Empty;
    private string toggleOverlayEditHotKey = string.Empty;

    public SettingsContentViewModel(
        IOptions<CacheOptions> cacheOptions,
        CharacterSettingsStore characterSettingsStore,
        HotkeySettingsStore hotkeySettingsStore,
        ILocalizationService localizationService)
        : base("settings", "Nav_Settings", "Section_Settings_Description", localizationService)
    {
        this.cacheOptions = cacheOptions.Value;
        this.characterSettingsStore = characterSettingsStore;
        this.hotkeySettingsStore = hotkeySettingsStore;
        this.localizationService = localizationService;

        RegisteredCharacters = [];
        saveCharacterCommand = new RelayCommand(SaveCharacter, CanSaveCharacter);
        beginCreateCharacterCommand = new RelayCommand(BeginCreateCharacter);
        editCharacterCommand = new RelayCommand<CharacterProfileItemViewModel>(BeginEditCharacter);
        deleteCharacterCommand = new RelayCommand<CharacterProfileItemViewModel>(DeleteCharacter);
        saveHotkeysCommand = new RelayCommand(SaveHotkeys, CanSaveHotkeys);
        openCacheRootPathCommand = new RelayCommand(OpenCacheRootPath);

        toggleOverlayHotKey = hotkeySettingsStore.ToggleOverlayHotKey;
        toggleOverlayEditHotKey = hotkeySettingsStore.ToggleOverlayEditHotKey;

        RefreshRegisteredCharacters();
        BeginCreateCharacter();
    }

    public ObservableCollection<CharacterProfileItemViewModel> RegisteredCharacters { get; }

    public CharacterProfileItemViewModel? SelectedCharacter
    {
        get => selectedCharacter;
        set
        {
            if (!SetProperty(ref selectedCharacter, value))
            {
                return;
            }

            characterSettingsStore.Select(value?.ProfileId);
            OnPropertyChanged(nameof(CharacterSettingsCurrentPath));
        }
    }

    public string HotkeyCardTitle => localizationService["Settings_HotkeyCardTitle"];

    public string HotkeyCardDescription => localizationService["Settings_HotkeyCardDescription"];

    public string ToggleOverlayHotKeyLabel => localizationService["Settings_ToggleOverlayHotKeyLabel"];

    public string ToggleOverlayEditHotKeyLabel => localizationService["Settings_ToggleOverlayEditHotKeyLabel"];

    public string SaveHotkeysButtonLabel => localizationService["Settings_SaveButtonLabel"];

    public string HotkeyCapturingLabel => localizationService["Settings_HotkeyCapturingLabel"];

    public string CachePathTitle => localizationService["Settings_CachePathTitle"];

    public string CachePathDescription => localizationService["Settings_CachePathDescription"];

    public string CacheRootPath => cacheOptions.RootPath;

    public string CacheIconsPath => cacheOptions.IconsPath;

    public string OpenCacheRootPathLabel => localizationService["Settings_OpenCacheRootPathLabel"];

    public string CharacterSettingsCardTitle => localizationService["Settings_CharacterCardTitle"];

    public string CharacterSettingsSelectionLabel => localizationService["Settings_CharacterSelectionLabel"];

    public string CharacterSettingsCurrentPathLabel => localizationService["Settings_CharacterCurrentPathLabel"];

    public string CharacterSettingsCurrentPath => string.IsNullOrWhiteSpace(characterSettingsStore.RootPath)
        ? localizationService["Settings_Unconfigured"]
        : characterSettingsStore.RootPath;

    public string CharacterListNameColumnLabel => localizationService["Settings_CharacterNameColumnLabel"];

    public string CharacterListWorldColumnLabel => localizationService["Settings_WorldColumnLabel"];

    public string CharacterListEditColumnLabel => localizationService["Settings_EditColumnLabel"];

    public string CharacterListDeleteColumnLabel => localizationService["Settings_DeleteColumnLabel"];

    public string CharacterFormTitle => editingCharacterId.HasValue
        ? localizationService["Settings_CharacterEditTitle"]
        : localizationService["Settings_CharacterCreateTitle"];

    public string CharacterNameLabel => localizationService["Settings_CharacterNameLabel"];

    public string WorldNameLabel => localizationService["Settings_WorldNameLabel"];

    public string CharacterSettingsPathLabel => localizationService["Settings_CharacterSettingsPathLabel"];

    public string CharacterSettingsSaveButtonLabel => editingCharacterId.HasValue
        ? localizationService["Settings_CharacterUpdateButtonLabel"]
        : localizationService["Settings_CharacterCreateButtonLabel"];

    public string CharacterSettingsCreateButtonLabel => localizationService["Settings_CharacterNewButtonLabel"];

    public string CharacterListEmptyMessage => localizationService["Settings_CharacterEmptyMessage"];

    public string CharacterName
    {
        get => characterName;
        set
        {
            if (!SetProperty(ref characterName, value))
            {
                return;
            }

            saveCharacterCommand.NotifyCanExecuteChanged();
        }
    }

    public string WorldName
    {
        get => worldName;
        set
        {
            if (!SetProperty(ref worldName, value))
            {
                return;
            }

            saveCharacterCommand.NotifyCanExecuteChanged();
        }
    }

    public string CharacterSettingsPath
    {
        get => characterSettingsPath;
        set
        {
            if (!SetProperty(ref characterSettingsPath, value))
            {
                return;
            }

            saveCharacterCommand.NotifyCanExecuteChanged();
        }
    }

    public string ToggleOverlayHotKey
    {
        get => toggleOverlayHotKey;
        set
        {
            if (!SetProperty(ref toggleOverlayHotKey, value))
            {
                return;
            }

            saveHotkeysCommand.NotifyCanExecuteChanged();
        }
    }

    public string ToggleOverlayEditHotKey
    {
        get => toggleOverlayEditHotKey;
        set
        {
            if (!SetProperty(ref toggleOverlayEditHotKey, value))
            {
                return;
            }

            saveHotkeysCommand.NotifyCanExecuteChanged();
        }
    }

    public RelayCommand SaveCharacterSettingsCommand => saveCharacterCommand;

    public RelayCommand BeginCreateCharacterCommand => beginCreateCharacterCommand;

    public RelayCommand<CharacterProfileItemViewModel> EditCharacterCommand => editCharacterCommand;

    public RelayCommand<CharacterProfileItemViewModel> DeleteCharacterCommand => deleteCharacterCommand;

    public RelayCommand SaveHotkeysCommand => saveHotkeysCommand;

    public RelayCommand OpenCacheRootPathCommand => openCacheRootPathCommand;

    private void BeginCreateCharacter()
    {
        editingCharacterId = null;
        CharacterName = string.Empty;
        WorldName = string.Empty;
        CharacterSettingsPath = string.Empty;
        NotifyCharacterFormChanged();
    }

    private void BeginEditCharacter(CharacterProfileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        editingCharacterId = item.ProfileId;
        CharacterName = item.CharacterName;
        WorldName = item.WorldName;
        CharacterSettingsPath = item.RootPath;
        NotifyCharacterFormChanged();
    }

    private void DeleteCharacter(CharacterProfileItemViewModel? item)
    {
        if (item is null || !characterSettingsStore.Delete(item.ProfileId))
        {
            return;
        }

        RefreshRegisteredCharacters();
        BeginCreateCharacter();
    }

    private bool CanSaveCharacter()
    {
        return !string.IsNullOrWhiteSpace(CharacterName)
            && !string.IsNullOrWhiteSpace(WorldName)
            && !string.IsNullOrWhiteSpace(CharacterSettingsPath);
    }

    private void SaveCharacter()
    {
        CharacterProfile profile = new()
        {
            ProfileId = editingCharacterId ?? Guid.NewGuid(),
            CharacterName = CharacterName,
            WorldName = WorldName,
            RootPath = CharacterSettingsPath
        };

        CharacterProfile savedProfile = characterSettingsStore.Save(profile);
        RefreshRegisteredCharacters();
        SelectedCharacter = RegisteredCharacters.FirstOrDefault(item => item.ProfileId == savedProfile.ProfileId);
        BeginCreateCharacter();
    }

    private bool CanSaveHotkeys()
    {
        return !string.IsNullOrWhiteSpace(ToggleOverlayHotKey)
            && !string.IsNullOrWhiteSpace(ToggleOverlayEditHotKey);
    }

    private void SaveHotkeys()
    {
        hotkeySettingsStore.Save(ToggleOverlayHotKey, ToggleOverlayEditHotKey);
        ToggleOverlayHotKey = hotkeySettingsStore.ToggleOverlayHotKey;
        ToggleOverlayEditHotKey = hotkeySettingsStore.ToggleOverlayEditHotKey;
    }

    private void OpenCacheRootPath()
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(rootPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{rootPath}\"",
            UseShellExecute = true
        });
    }

    private void RefreshRegisteredCharacters()
    {
        RegisteredCharacters.Clear();

        foreach (CharacterProfile profile in characterSettingsStore.Profiles)
        {
            RegisteredCharacters.Add(new CharacterProfileItemViewModel(profile));
        }

        SelectedCharacter = RegisteredCharacters.FirstOrDefault(item => item.ProfileId == characterSettingsStore.SelectedProfileId);
        OnPropertyChanged(nameof(CharacterSettingsCurrentPath));
    }

    private void NotifyCharacterFormChanged()
    {
        OnPropertyChanged(nameof(CharacterFormTitle));
        OnPropertyChanged(nameof(CharacterSettingsSaveButtonLabel));
        saveCharacterCommand.NotifyCanExecuteChanged();
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(HotkeyCardTitle));
        OnPropertyChanged(nameof(HotkeyCardDescription));
        OnPropertyChanged(nameof(ToggleOverlayHotKeyLabel));
        OnPropertyChanged(nameof(ToggleOverlayEditHotKeyLabel));
        OnPropertyChanged(nameof(SaveHotkeysButtonLabel));
        OnPropertyChanged(nameof(HotkeyCapturingLabel));
        OnPropertyChanged(nameof(CachePathTitle));
        OnPropertyChanged(nameof(CachePathDescription));
        OnPropertyChanged(nameof(OpenCacheRootPathLabel));
        OnPropertyChanged(nameof(CharacterSettingsCardTitle));
        OnPropertyChanged(nameof(CharacterSettingsSelectionLabel));
        OnPropertyChanged(nameof(CharacterSettingsCurrentPathLabel));
        OnPropertyChanged(nameof(CharacterSettingsCurrentPath));
        OnPropertyChanged(nameof(CharacterListNameColumnLabel));
        OnPropertyChanged(nameof(CharacterListWorldColumnLabel));
        OnPropertyChanged(nameof(CharacterListEditColumnLabel));
        OnPropertyChanged(nameof(CharacterListDeleteColumnLabel));
        OnPropertyChanged(nameof(CharacterFormTitle));
        OnPropertyChanged(nameof(CharacterNameLabel));
        OnPropertyChanged(nameof(WorldNameLabel));
        OnPropertyChanged(nameof(CharacterSettingsPathLabel));
        OnPropertyChanged(nameof(CharacterSettingsSaveButtonLabel));
        OnPropertyChanged(nameof(CharacterSettingsCreateButtonLabel));
        OnPropertyChanged(nameof(CharacterListEmptyMessage));
    }
}
