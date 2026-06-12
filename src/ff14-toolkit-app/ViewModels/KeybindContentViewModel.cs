using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.Keybind;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Keybind;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FF14Toolkit.App.ViewModels;

public sealed class KeybindContentViewModel : ShellContentViewModel
{
    private readonly CharacterSettingsStore characterSettingsStore;
    private readonly IKeybindDataService keybindDataService;
    private CharacterProfile? selectedProfile;
    private string statusMessage = string.Empty;
    private string sourcePath = "-";
    private int assignedCount;
    private int num0MatchCount;

    public KeybindContentViewModel(
        ILocalizationService localizationService,
        CharacterSettingsStore characterSettingsStore,
        IKeybindDataService keybindDataService)
        : base("development-keybind", "Nav_DevelopmentKeybind", "Section_DevelopmentKeybind_Description", localizationService)
    {
        this.characterSettingsStore = characterSettingsStore;
        this.keybindDataService = keybindDataService;
    }

    public ObservableCollection<KeybindCommandViewModel> Entries { get; } = [];

    public ObservableCollection<KeybindCommandViewModel> Num0Entries { get; } = [];

    public string CharacterLabel => "キャラクター";

    public string SourcePathLabel => "KEYBIND.DAT";

    public string AssignedCountLabel => "割り当て数";

    public string Num0CountLabel => "Num0 該当";

    public string Num0PanelTitle => "Num0 の割り当て";

    public string Num0PanelDescription => "Num0 が設定されているコマンドだけを先に表示します。";

    public string CommandLabel => "コマンド";

    public string PrimaryLabel => "主キー";

    public string SecondaryLabel => "副キー";

    public string RawBindingLabel => "生データ";

    public string SelectedCharacterDisplay => selectedProfile?.DisplayLabel ?? "-";

    public string SourcePath
    {
        get => sourcePath;
        private set => SetProperty(ref sourcePath, value);
    }

    public string AssignedCountText
    {
        get => assignedCount.ToString(CultureInfo.CurrentCulture);
        private set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
            {
                assignedCount = parsed;
                OnPropertyChanged();
            }
        }
    }

    public string Num0MatchCountText
    {
        get => num0MatchCount.ToString(CultureInfo.CurrentCulture);
        private set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
            {
                num0MatchCount = parsed;
                OnPropertyChanged();
            }
        }
    }

    public bool HasNum0Entries => Num0Entries.Count > 0;

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            if (!SetProperty(ref statusMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public async Task InitializeAsync()
    {
        selectedProfile = characterSettingsStore.SelectedProfile;
        OnPropertyChanged(nameof(SelectedCharacterDisplay));

        Entries.Clear();
        Num0Entries.Clear();
        OnPropertyChanged(nameof(HasNum0Entries));

        SourcePath = "-";
        AssignedCountText = "0";
        Num0MatchCountText = "0";

        if (selectedProfile is null || string.IsNullOrWhiteSpace(selectedProfile.RootPath))
        {
            StatusMessage = "キャラクターが選択されていません。";
            return;
        }

        try
        {
            KeybindAnalysisResult analysisResult = await Task.Run(() => keybindDataService.AnalyzePath(selectedProfile.RootPath));
            SourcePath = analysisResult.SourcePath;
            AssignedCountText = analysisResult.AssignedEntries.Count.ToString(CultureInfo.CurrentCulture);

            KeybindCommandViewModel[] allEntries = analysisResult.AssignedEntries
                .Select(entry => new KeybindCommandViewModel(entry))
                .OrderBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (KeybindCommandViewModel entry in allEntries)
            {
                Entries.Add(entry);
            }

            KeybindCommandViewModel[] num0Entries = allEntries
                .Where(entry => entry.ContainsNum0)
                .OrderBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (KeybindCommandViewModel entry in num0Entries)
            {
                Num0Entries.Add(entry);
            }

            OnPropertyChanged(nameof(HasNum0Entries));
            Num0MatchCountText = num0Entries.Length.ToString(CultureInfo.CurrentCulture);

            StatusMessage = allEntries.Length == 0
                ? "割り当て済みのキー設定は見つかりませんでした。"
                : string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"KEYBIND.DAT の読み込みに失敗しました: {ex.Message}";
        }
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(CharacterLabel));
        OnPropertyChanged(nameof(SourcePathLabel));
        OnPropertyChanged(nameof(AssignedCountLabel));
        OnPropertyChanged(nameof(Num0CountLabel));
        OnPropertyChanged(nameof(Num0PanelTitle));
        OnPropertyChanged(nameof(Num0PanelDescription));
        OnPropertyChanged(nameof(CommandLabel));
        OnPropertyChanged(nameof(PrimaryLabel));
        OnPropertyChanged(nameof(SecondaryLabel));
        OnPropertyChanged(nameof(RawBindingLabel));
        OnPropertyChanged(nameof(SelectedCharacterDisplay));
        OnPropertyChanged(nameof(AssignedCountText));
        OnPropertyChanged(nameof(Num0MatchCountText));
        _ = InitializeAsync();
    }
}

public sealed class KeybindCommandViewModel
{
    public KeybindCommandViewModel(KeybindEntry entry)
    {
        Command = entry.Command;
        PrimaryText = KeybindDisplayFormatter.Format(entry.Primary);
        SecondaryText = KeybindDisplayFormatter.Format(entry.Secondary);
        RawBindingText = entry.RawBindingText;
        ContainsNum0 =
            string.Equals(PrimaryText, "Num0", StringComparison.OrdinalIgnoreCase)
            || PrimaryText.Contains("+Num0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(SecondaryText, "Num0", StringComparison.OrdinalIgnoreCase)
            || SecondaryText.Contains("+Num0", StringComparison.OrdinalIgnoreCase);
    }

    public string Command { get; }

    public string PrimaryText { get; }

    public string SecondaryText { get; }

    public string RawBindingText { get; }

    public bool ContainsNum0 { get; }
}
