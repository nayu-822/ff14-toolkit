using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.Hotbar;
using FF14Toolkit.App.Models.Keybind;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Hotbar;
using FF14Toolkit.App.Services.Keybind;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace FF14Toolkit.App.ViewModels;

public sealed class HotbarContentViewModel : ShellContentViewModel
{
    private readonly ILocalizationService localizationService;
    private readonly CharacterSettingsStore characterSettingsStore;
    private readonly IHotbarDataService hotbarDataService;
    private readonly IGameDataService gameDataService;
    private readonly IKeybindDataService keybindDataService;
    private HotbarAnalysisResult? analysisResult;
    private Dictionary<string, string> keybindDisplayByCommand = new(StringComparer.OrdinalIgnoreCase);
    private CharacterProfile? selectedProfile;
    private HotbarJobOptionViewModel? selectedJob;
    private string statusMessage = string.Empty;

    public HotbarContentViewModel(
        ILocalizationService localizationService,
        CharacterSettingsStore characterSettingsStore,
        IHotbarDataService hotbarDataService,
        IKeybindDataService keybindDataService,
        IGameDataService gameDataService)
        : base("development-hotbar", "Nav_Hotbar", "Section_Hotbar_Description", localizationService)
    {
        this.localizationService = localizationService;
        this.characterSettingsStore = characterSettingsStore;
        this.hotbarDataService = hotbarDataService;
        this.keybindDataService = keybindDataService;
        this.gameDataService = gameDataService;
    }

    public ObservableCollection<HotbarJobOptionViewModel> AvailableJobs { get; } = [];

    public ObservableCollection<HotbarRowViewModel> HotbarRows { get; } = [];

    public string CharacterLabel => localizationService["Hotbar_CharacterLabel"];

    public string JobLabel => localizationService["Hotbar_JobLabel"];

    public string SelectedCharacterDisplay => selectedProfile?.DisplayLabel ?? "-";

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

    public HotbarJobOptionViewModel? SelectedJob
    {
        get => selectedJob;
        set
        {
            if (!SetProperty(ref selectedJob, value) || value is null)
            {
                return;
            }

            BuildRows();
        }
    }

    public async Task InitializeAsync()
    {
        selectedProfile = characterSettingsStore.SelectedProfile;
        OnPropertyChanged(nameof(SelectedCharacterDisplay));

        AvailableJobs.Clear();
        HotbarRows.Clear();
        analysisResult = null;
        keybindDisplayByCommand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (selectedProfile is null || string.IsNullOrWhiteSpace(selectedProfile.RootPath))
        {
            SelectedJob = null;
            StatusMessage = localizationService["Hotbar_NoCharacterSelected"];
            return;
        }

        try
        {
            HotbarAnalysisResult loadedAnalysis = await Task.Run(() => hotbarDataService.AnalyzePath(selectedProfile.RootPath));
            KeybindAnalysisResult loadedKeybindAnalysis = await Task.Run(() => keybindDataService.AnalyzePath(selectedProfile.RootPath));
            analysisResult = loadedAnalysis;
            keybindDisplayByCommand = BuildKeybindDisplayMap(loadedKeybindAnalysis);
            BuildJobOptions();
        }
        catch (Exception ex)
        {
            SelectedJob = null;
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                localizationService["Hotbar_LoadFailed"],
                ex.Message);
        }
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(CharacterLabel));
        OnPropertyChanged(nameof(JobLabel));
        OnPropertyChanged(nameof(SelectedCharacterDisplay));
        _ = InitializeAsync();
    }

    private void BuildJobOptions()
    {
        if (analysisResult is null)
        {
            StatusMessage = localizationService["Hotbar_NoJobAvailable"];
            return;
        }

        int? previousSelection = selectedJob?.ClassJobId;
        List<HotbarJobOptionViewModel> options = analysisResult.NonEmptyEntries
            .Select(entry => HotbarGroupDefinitions.TryGetDefinition(entry.GroupId, out HotbarGroupDefinition? definition)
                ? definition
                : null)
            .Where(definition => definition is not null && definition.Category is HotbarGroupCategory.ClassJob && definition.ClassJobId is not null)
            .GroupBy(definition => definition!.ClassJobId!.Value)
            .Select(group =>
            {
                int classJobId = group.Key;
                HotbarGroupDefinition sample = group.First()!;
                return new HotbarJobOptionViewModel(
                    classJobId,
                    gameDataService.ResolveClassJobName(classJobId) ?? sample.DisplayName);
            })
            .OrderBy(option => option.ClassJobId)
            .ToList();

        AvailableJobs.Clear();
        foreach (HotbarJobOptionViewModel option in options)
        {
            AvailableJobs.Add(option);
        }

        if (AvailableJobs.Count == 0)
        {
            SelectedJob = null;
            StatusMessage = localizationService["Hotbar_NoJobAvailable"];
            return;
        }

        SelectedJob = AvailableJobs.FirstOrDefault(option => option.ClassJobId == previousSelection)
            ?? AvailableJobs[0];
    }

    private void BuildRows()
    {
        HotbarRows.Clear();

        if (analysisResult is null || selectedJob is null)
        {
            return;
        }

        List<byte> targetGroupIds = analysisResult.ParseResult.Entries
            .Select(entry => entry.GroupId)
            .Distinct()
            .Where(groupId =>
                HotbarGroupDefinitions.TryGetDefinition(groupId, out HotbarGroupDefinition? definition)
                && definition is not null
                && definition.Category is HotbarGroupCategory.ClassJob
                && definition.ClassJobId == selectedJob.ClassJobId)
            .ToList();

        List<HotbarRowViewModel> rows = analysisResult.ParseResult.Entries
            .Where(entry => targetGroupIds.Contains(entry.GroupId))
            .GroupBy(entry => entry.HotbarId)
            .OrderBy(group => group.Key)
            .Select(CreateHotbarRow)
            .Where(row => row.Slots.Any(slot => !slot.IsEmpty))
            .ToList();

        foreach (HotbarRowViewModel row in rows)
        {
            HotbarRows.Add(row);
        }

        StatusMessage = HotbarRows.Count == 0
            ? localizationService["Hotbar_NoSlots"]
            : string.Empty;
    }

    private HotbarRowViewModel CreateHotbarRow(IGrouping<byte, HotbarSlotEntry> group)
    {
        Dictionary<byte, HotbarSlotEntry> entriesBySlotId = group
            .GroupBy(entry => entry.SlotId)
            .ToDictionary(slotGroup => slotGroup.Key, slotGroup => slotGroup.First());

        List<HotbarSlotViewModel> slots = [];
        for (byte slotId = 0; slotId < 12; slotId++)
        {
            if (entriesBySlotId.TryGetValue(slotId, out HotbarSlotEntry? entry))
            {
                slots.Add(CreateSlot(entry));
            }
            else
            {
                slots.Add(HotbarSlotViewModel.Empty(slotId));
            }
        }

        return new HotbarRowViewModel((group.Key + 1).ToString(CultureInfo.InvariantCulture), slots);
    }

    private HotbarSlotViewModel CreateSlot(HotbarSlotEntry entry)
    {
        if (entry.IsEmpty)
        {
            return HotbarSlotViewModel.Empty(entry.SlotId);
        }

        string hotbarCommand = BuildHotbarCommand(entry.HotbarId, entry.SlotId);
        string tooltip = !string.IsNullOrWhiteSpace(entry.ResolvedCommandName)
            ? entry.ResolvedCommandName!
            : $"{entry.SlotTypeName} #{entry.CommandId}";
        string keybindText = keybindDisplayByCommand.TryGetValue(hotbarCommand, out string? resolvedKeybind)
            ? resolvedKeybind
            : "-";

        return new HotbarSlotViewModel(
            entry.SlotId,
            false,
            tooltip,
            tooltip,
            keybindText,
            gameDataService.ResolveHotbarCommandIcon(entry.SlotTypeId, entry.CommandId));
    }

    private static Dictionary<string, string> BuildKeybindDisplayMap(KeybindAnalysisResult analysisResult)
    {
        return analysisResult.ParseResult.Entries
            .ToDictionary(
                entry => entry.Command,
                FormatAssignmentDisplay,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string FormatAssignmentDisplay(KeybindEntry entry)
    {
        List<string> parts = [];
        string primary = KeybindDisplayFormatter.Format(entry.Primary);
        string secondary = KeybindDisplayFormatter.Format(entry.Secondary);

        if (!string.IsNullOrWhiteSpace(primary))
        {
            parts.Add(primary);
        }

        if (!string.IsNullOrWhiteSpace(secondary) && !string.Equals(secondary, primary, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(secondary);
        }

        return parts.Count == 0
            ? "-"
            : string.Join(" / ", parts);
    }

    private static string BuildHotbarCommand(byte hotbarId, byte slotId)
    {
        string hotbarToken = hotbarId switch
        {
            <= 9 => (hotbarId + 1).ToString(CultureInfo.InvariantCulture),
            10 => "EX",
            _ => (hotbarId + 1).ToString(CultureInfo.InvariantCulture)
        };

        return $"HOTBAR_{hotbarToken}_{GetHotbarSlotToken(slotId)}";
    }

    private static string GetHotbarSlotToken(byte slotId)
    {
        return slotId switch
        {
            <= 8 => (slotId + 1).ToString(CultureInfo.InvariantCulture),
            9 => "0",
            10 => "A",
            11 => "B",
            _ => (slotId + 1).ToString(CultureInfo.InvariantCulture)
        };
    }
}

public sealed class HotbarJobOptionViewModel
{
    public HotbarJobOptionViewModel(int classJobId, string displayName)
    {
        ClassJobId = classJobId;
        DisplayName = displayName;
    }

    public int ClassJobId { get; }

    public string DisplayName { get; }
}

public sealed class HotbarRowViewModel
{
    public HotbarRowViewModel(string label, IReadOnlyList<HotbarSlotViewModel> slots)
    {
        Label = label;
        Slots = slots;
    }

    public string Label { get; }

    public IReadOnlyList<HotbarSlotViewModel> Slots { get; }
}

public sealed class HotbarSlotViewModel
{
    public HotbarSlotViewModel(byte slotId, bool isEmpty, string displayName, string tooltipText, string keybindText, BitmapSource? iconSource)
    {
        SlotId = slotId;
        IsEmpty = isEmpty;
        DisplayName = displayName;
        ToolTipText = tooltipText;
        KeybindText = keybindText;
        IconSource = iconSource;
    }

    public byte SlotId { get; }

    public bool IsEmpty { get; }

    public string DisplayName { get; }

    public string ToolTipText { get; }

    public string KeybindText { get; }

    public BitmapSource? IconSource { get; }

    public bool HasIcon => IconSource is not null;

    public string FallbackText => IsEmpty ? string.Empty : "?";

    public static HotbarSlotViewModel Empty(byte slotId)
    {
        return new HotbarSlotViewModel(slotId, true, string.Empty, string.Empty, "-", null);
    }
}
