using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftSequenceHotkeySettingsContentViewModel : ShellContentViewModel
{
    private readonly CraftStartButtonAutomationService craftStartButtonAutomationService;
    private readonly ILocalizationService localizationService;
    private readonly CraftActionSequenceStore craftActionSequenceStore;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly RelayCommand startMonitoringCommand;
    private readonly RelayCommand stopMonitoringCommand;
    private readonly RelayCommand saveCommand;
    private bool isTemplateMatchMonitoring;

    public CraftSequenceHotkeySettingsContentViewModel(
        ILocalizationService localizationService,
        CraftActionSequenceStore craftActionSequenceStore,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        CraftStartButtonAutomationService craftStartButtonAutomationService)
        : base(
            "crafting-sequence-hotkeys",
            "Nav_CraftingSequenceHotkeys",
            "Section_CraftingSequenceHotkeys_Description",
            localizationService)
    {
        this.craftStartButtonAutomationService = craftStartButtonAutomationService;
        this.localizationService = localizationService;
        this.craftActionSequenceStore = craftActionSequenceStore;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;

        AvailableSequences = [];
        HotkeySlots = new ObservableCollection<CraftSequenceHotkeySlotViewModel>(
            craftSequenceHotkeyStore.Bindings.Select(binding => new CraftSequenceHotkeySlotViewModel(binding)));
        saveCommand = new RelayCommand(Save);
        startMonitoringCommand = new RelayCommand(StartMonitoring, () => !IsTemplateMatchMonitoring);
        stopMonitoringCommand = new RelayCommand(StopMonitoring, () => IsTemplateMatchMonitoring);
        isTemplateMatchMonitoring = craftStartButtonAutomationService.IsMonitoring;

        craftActionSequenceStore.Sequences.CollectionChanged += OnSequencesChanged;
        craftStartButtonAutomationService.MonitoringStateChanged += OnMonitoringStateChanged;
        RefreshAvailableSequences();
    }

    public ObservableCollection<CraftSequenceOptionViewModel> AvailableSequences { get; }

    public ObservableCollection<CraftSequenceHotkeySlotViewModel> HotkeySlots { get; }

    public RelayCommand SaveCommand => saveCommand;

    public RelayCommand StartMonitoringCommand => startMonitoringCommand;

    public RelayCommand StopMonitoringCommand => stopMonitoringCommand;

    public string SaveButtonLabel => localizationService["CraftingSequenceHotkeys_SaveButton"];

    public string StartMonitoringButtonLabel => "CRAFTING LOG 監視を開始";

    public string StopMonitoringButtonLabel => "CRAFTING LOG 監視を停止";

    public string TemplateMatchMonitoringStatusLabel => IsTemplateMatchMonitoring
        ? "CRAFTING LOG サンプル監視: 実行中"
        : "CRAFTING LOG サンプル監視: 停止中";

    public string EnabledColumnLabel => localizationService["CraftingSequenceHotkeys_EnabledColumn"];

    public string HotkeyColumnLabel => localizationService["CraftingSequenceHotkeys_HotkeyColumn"];

    public string SequenceColumnLabel => localizationService["CraftingSequenceHotkeys_SequenceColumn"];

    public string RepeatColumnLabel => localizationService["CraftingSequenceHotkeys_RepeatColumn"];

    public string EmptySequenceOptionLabel => localizationService["CraftingSequenceHotkeys_SequenceEmptyOption"];

    public string EmptySequencesMessage => localizationService["CraftingSequenceHotkeys_EmptySequences"];

    public string OverlayOnlyNotice => localizationService["CraftingSequenceHotkeys_OverlayOnlyNotice"];

    public string HotkeyCapturingLabel => localizationService["Settings_HotkeyCapturingLabel"];

    public bool IsTemplateMatchMonitoring
    {
        get => isTemplateMatchMonitoring;
        private set
        {
            if (!SetProperty(ref isTemplateMatchMonitoring, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TemplateMatchMonitoringStatusLabel));
            startMonitoringCommand.NotifyCanExecuteChanged();
            stopMonitoringCommand.NotifyCanExecuteChanged();
        }
    }

    private void Save()
    {
        craftSequenceHotkeyStore.Save(HotkeySlots.Select(slot => slot.ToBinding()));
    }

    private void StartMonitoring()
    {
        _ = craftStartButtonAutomationService.StartTemplateMatchMonitoringAsync();
    }

    private void StopMonitoring()
    {
        _ = craftStartButtonAutomationService.StopTemplateMatchMonitoringAsync();
    }

    private void OnSequencesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAvailableSequences();
    }

    private void RefreshAvailableSequences()
    {
        AvailableSequences.Clear();
        AvailableSequences.Add(new CraftSequenceOptionViewModel
        {
            SequenceId = null,
            DisplayName = EmptySequenceOptionLabel
        });

        foreach (CraftActionSequence sequence in craftActionSequenceStore.Sequences.OrderBy(sequence => sequence.Name, StringComparer.CurrentCulture))
        {
            AvailableSequences.Add(new CraftSequenceOptionViewModel
            {
                SequenceId = sequence.SequenceId,
                DisplayName = sequence.Name
            });
        }

        HashSet<Guid> knownSequenceIds = craftActionSequenceStore.Sequences
            .Select(sequence => sequence.SequenceId)
            .ToHashSet();

        foreach (CraftSequenceHotkeySlotViewModel slot in HotkeySlots)
        {
            if (slot.SelectedSequenceId is Guid sequenceId && !knownSequenceIds.Contains(sequenceId))
            {
                slot.SelectedSequenceId = null;
            }
        }
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(SaveButtonLabel));
        OnPropertyChanged(nameof(EnabledColumnLabel));
        OnPropertyChanged(nameof(HotkeyColumnLabel));
        OnPropertyChanged(nameof(SequenceColumnLabel));
        OnPropertyChanged(nameof(RepeatColumnLabel));
        OnPropertyChanged(nameof(EmptySequenceOptionLabel));
        OnPropertyChanged(nameof(EmptySequencesMessage));
        OnPropertyChanged(nameof(OverlayOnlyNotice));
        OnPropertyChanged(nameof(HotkeyCapturingLabel));
        OnPropertyChanged(nameof(StartMonitoringButtonLabel));
        OnPropertyChanged(nameof(StopMonitoringButtonLabel));
        OnPropertyChanged(nameof(TemplateMatchMonitoringStatusLabel));
        RefreshAvailableSequences();
    }

    private void OnMonitoringStateChanged(object? sender, EventArgs e)
    {
        IsTemplateMatchMonitoring = craftStartButtonAutomationService.IsMonitoring;
    }
}
