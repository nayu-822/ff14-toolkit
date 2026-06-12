using FF14Toolkit.App.Models.Addon;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Addon;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FF14Toolkit.App.ViewModels;

public sealed class AddonContentViewModel : ShellContentViewModel
{
    private readonly CharacterSettingsStore characterSettingsStore;
    private readonly IAddonDataService addonDataService;
    private CharacterProfile? selectedProfile;
    private string statusMessage = string.Empty;
    private string sourcePath = "-";
    private string dataSetName = "-";
    private int elementCount;
    private int resolvedCount;
    private int scaledElementCount;
    private int unresolvedCount;

    public AddonContentViewModel(
        ILocalizationService localizationService,
        CharacterSettingsStore characterSettingsStore,
        IAddonDataService addonDataService)
        : base("development-addon", "Nav_DevelopmentAddon", "Section_DevelopmentAddon_Description", localizationService)
    {
        this.characterSettingsStore = characterSettingsStore;
        this.addonDataService = addonDataService;
    }

    public ObservableCollection<AddonLayoutEntryViewModel> Entries { get; } = [];

    public ObservableCollection<AddonUnresolvedHashViewModel> UnresolvedEntries { get; } = [];

    public string CharacterLabel => "キャラクター";

    public string SourcePathLabel => "ADDON.DAT";

    public string DataSetLabel => "データセット";

    public string ElementCountLabel => "総数";

    public string ResolvedCountLabel => "解決済み";

    public string ScaledElementCountLabel => "拡大縮小あり";

    public string UnresolvedCountLabel => "未解決";

    public string UnresolvedPanelTitle => "未解決 hash";

    public string UnresolvedPanelDescription => "名前解決できていない項目です。0x... の値だけをこちらへ集約しています。";

    public string NameLabel => "名称";

    public string HashLabel => "Hash";

    public string PositionLabel => "座標";

    public string SizeLabel => "サイズ";

    public string ScaleLabel => "倍率";

    public string FlagsLabel => "Flags";

    public string OccurrenceLabel => "件数";

    public string SelectedCharacterDisplay => selectedProfile?.DisplayLabel ?? "-";

    public string SourcePath
    {
        get => sourcePath;
        private set => SetProperty(ref sourcePath, value);
    }

    public string DataSetName
    {
        get => dataSetName;
        private set => SetProperty(ref dataSetName, value);
    }

    public string ElementCountText
    {
        get => elementCount.ToString(CultureInfo.CurrentCulture);
        private set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
            {
                elementCount = parsed;
                OnPropertyChanged();
            }
        }
    }

    public string ResolvedCountText
    {
        get => resolvedCount.ToString(CultureInfo.CurrentCulture);
        private set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
            {
                resolvedCount = parsed;
                OnPropertyChanged();
            }
        }
    }

    public string ScaledElementCountText
    {
        get => scaledElementCount.ToString(CultureInfo.CurrentCulture);
        private set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
            {
                scaledElementCount = parsed;
                OnPropertyChanged();
            }
        }
    }

    public string UnresolvedCountText
    {
        get => unresolvedCount.ToString(CultureInfo.CurrentCulture);
        private set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed))
            {
                unresolvedCount = parsed;
                OnPropertyChanged();
            }
        }
    }

    public bool HasUnresolvedEntries => UnresolvedEntries.Count > 0;

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
        UnresolvedEntries.Clear();
        OnPropertyChanged(nameof(HasUnresolvedEntries));

        SourcePath = "-";
        DataSetName = "-";
        ElementCountText = "0";
        ResolvedCountText = "0";
        ScaledElementCountText = "0";
        UnresolvedCountText = "0";

        if (selectedProfile is null || string.IsNullOrWhiteSpace(selectedProfile.RootPath))
        {
            StatusMessage = "キャラクターが選択されていません。";
            return;
        }

        try
        {
            AddonAnalysisResult analysisResult = await Task.Run(() => addonDataService.AnalyzePath(selectedProfile.RootPath));
            SourcePath = analysisResult.SourcePath;
            DataSetName = analysisResult.ParseResult.Header.DataSetName;
            ElementCountText = analysisResult.Entries.Count.ToString(CultureInfo.CurrentCulture);
            ScaledElementCountText = analysisResult.NonDefaultScaleEntryCount.ToString(CultureInfo.CurrentCulture);

            AddonLayoutEntry[] resolvedEntries = analysisResult.Entries
                .Where(entry => !IsUnresolved(entry))
                .OrderByDescending(item => item.Area)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (AddonLayoutEntry entry in resolvedEntries)
            {
                Entries.Add(new AddonLayoutEntryViewModel(entry));
            }

            IReadOnlyList<AddonUnresolvedHashViewModel> unresolvedEntries = analysisResult.Entries
                .Where(IsUnresolved)
                .GroupBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AddonUnresolvedHashViewModel(group.Key, group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.HashText, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (AddonUnresolvedHashViewModel unresolvedEntry in unresolvedEntries)
            {
                UnresolvedEntries.Add(unresolvedEntry);
            }

            OnPropertyChanged(nameof(HasUnresolvedEntries));
            ResolvedCountText = resolvedEntries.Length.ToString(CultureInfo.CurrentCulture);
            UnresolvedCountText = unresolvedEntries.Count.ToString(CultureInfo.CurrentCulture);

            StatusMessage = resolvedEntries.Length == 0 && unresolvedEntries.Count == 0
                ? "UI 要素は見つかりませんでした。"
                : string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"ADDON.DAT の読み込みに失敗しました: {ex.Message}";
        }
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(CharacterLabel));
        OnPropertyChanged(nameof(SourcePathLabel));
        OnPropertyChanged(nameof(DataSetLabel));
        OnPropertyChanged(nameof(ElementCountLabel));
        OnPropertyChanged(nameof(ResolvedCountLabel));
        OnPropertyChanged(nameof(ScaledElementCountLabel));
        OnPropertyChanged(nameof(UnresolvedCountLabel));
        OnPropertyChanged(nameof(UnresolvedPanelTitle));
        OnPropertyChanged(nameof(UnresolvedPanelDescription));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(HashLabel));
        OnPropertyChanged(nameof(PositionLabel));
        OnPropertyChanged(nameof(SizeLabel));
        OnPropertyChanged(nameof(ScaleLabel));
        OnPropertyChanged(nameof(FlagsLabel));
        OnPropertyChanged(nameof(OccurrenceLabel));
        OnPropertyChanged(nameof(SelectedCharacterDisplay));
        OnPropertyChanged(nameof(ElementCountText));
        OnPropertyChanged(nameof(ResolvedCountText));
        OnPropertyChanged(nameof(ScaledElementCountText));
        OnPropertyChanged(nameof(UnresolvedCountText));
        _ = InitializeAsync();
    }

    private static bool IsUnresolved(AddonLayoutEntry entry)
    {
        return entry.DisplayName.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AddonLayoutEntryViewModel
{
    public AddonLayoutEntryViewModel(AddonLayoutEntry entry)
    {
        DisplayName = entry.DisplayName;
        HashText = $"0x{entry.AddonNameHash:X8}";
        PositionText = $"{entry.X:0.##}, {entry.Y:0.##}";
        SizeText = $"{entry.Width} x {entry.Height}";
        ScaleText = entry.Scale.ToString("0.##", CultureInfo.CurrentCulture);
        FlagsText = $"0x{entry.ElementFlags:X8}";
        StateText = $"{entry.StateByte1}/{entry.StateByte2}/{entry.StateByte3}/{entry.Alpha}";
    }

    public string DisplayName { get; }

    public string HashText { get; }

    public string PositionText { get; }

    public string SizeText { get; }

    public string ScaleText { get; }

    public string FlagsText { get; }

    public string StateText { get; }
}

public sealed class AddonUnresolvedHashViewModel
{
    public AddonUnresolvedHashViewModel(string hashText, int count)
    {
        HashText = hashText;
        Count = count;
        CountText = count.ToString(CultureInfo.CurrentCulture);
    }

    public string HashText { get; }

    public int Count { get; }

    public string CountText { get; }
}
