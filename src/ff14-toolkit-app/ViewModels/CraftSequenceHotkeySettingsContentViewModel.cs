using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.TemplateMatching;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftSequenceHotkeySettingsContentViewModel : ShellContentViewModel
{
    private const string CraftingLogMonitorId = "crafting-log-monitor";

    private readonly CraftStartButtonAutomationService craftStartButtonAutomationService;
    private readonly ITemplateMonitorStatusSource templateMonitorStatusSource;
    private readonly ILocalizationService localizationService;
    private readonly CraftActionSequenceStore craftActionSequenceStore;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly AsyncRelayCommand startMonitoringCommand;
    private readonly AsyncRelayCommand stopMonitoringCommand;
    private readonly RelayCommand saveCommand;
    private bool isTemplateMatchMonitoring;
    private string monitorStatusSummary = "状態: Stopped";
    private string monitorStatusDetails = "フレーム数: 0";

    public CraftSequenceHotkeySettingsContentViewModel(
        ILocalizationService localizationService,
        CraftActionSequenceStore craftActionSequenceStore,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        CraftStartButtonAutomationService craftStartButtonAutomationService,
        ITemplateMonitorStatusSource templateMonitorStatusSource)
        : base(
            "crafting-sequence-hotkeys",
            "Nav_CraftingSequenceHotkeys",
            "Section_CraftingSequenceHotkeys_Description",
            localizationService)
    {
        this.craftStartButtonAutomationService = craftStartButtonAutomationService;
        this.templateMonitorStatusSource = templateMonitorStatusSource;
        this.localizationService = localizationService;
        this.craftActionSequenceStore = craftActionSequenceStore;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;

        AvailableSequences = [];
        HotkeySlots = new ObservableCollection<CraftSequenceHotkeySlotViewModel>(
            craftSequenceHotkeyStore.Bindings.Select(binding => new CraftSequenceHotkeySlotViewModel(binding)));
        saveCommand = new RelayCommand(Save);
        startMonitoringCommand = new AsyncRelayCommand(StartMonitoringAsync, () => !IsTemplateMatchMonitoring);
        stopMonitoringCommand = new AsyncRelayCommand(StopMonitoringAsync, () => IsTemplateMatchMonitoring);
        isTemplateMatchMonitoring = craftStartButtonAutomationService.IsMonitoring;

        craftActionSequenceStore.Sequences.CollectionChanged += OnSequencesChanged;
        craftStartButtonAutomationService.MonitoringStateChanged += OnMonitoringStateChanged;
        templateMonitorStatusSource.StatusChanged += OnTemplateMonitorStatusChanged;
        RefreshAvailableSequences();
        RefreshMonitorStatus();
    }

    public ObservableCollection<CraftSequenceOptionViewModel> AvailableSequences { get; }

    public ObservableCollection<CraftSequenceHotkeySlotViewModel> HotkeySlots { get; }

    public RelayCommand SaveCommand => saveCommand;

    public AsyncRelayCommand StartMonitoringCommand => startMonitoringCommand;

    public AsyncRelayCommand StopMonitoringCommand => stopMonitoringCommand;

    public string SaveButtonLabel => localizationService["CraftingSequenceHotkeys_SaveButton"];

    public string StartMonitoringButtonLabel => "CRAFTING LOG 監視を開始";

    public string StopMonitoringButtonLabel => "CRAFTING LOG 監視を停止";

    public string TemplateMatchMonitoringStatusLabel => IsTemplateMatchMonitoring
        ? "CRAFTING LOG サンプル監視: 実行中"
        : "CRAFTING LOG サンプル監視: 停止中";

    public string MonitorStatusSummary => monitorStatusSummary;

    public string MonitorStatusDetails => monitorStatusDetails;

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

    private async Task StartMonitoringAsync()
    {
        try
        {
            await craftStartButtonAutomationService.StartTemplateMatchMonitoringAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            monitorStatusSummary = "状態: Faulted";
            monitorStatusDetails = $"エラー: {exception.Message}";
            OnPropertyChanged(nameof(MonitorStatusSummary));
            OnPropertyChanged(nameof(MonitorStatusDetails));
        }
    }

    private async Task StopMonitoringAsync()
    {
        try
        {
            await craftStartButtonAutomationService.StopTemplateMatchMonitoringAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            monitorStatusSummary = "状態: Faulted";
            monitorStatusDetails = $"停止エラー: {exception.Message}";
            OnPropertyChanged(nameof(MonitorStatusSummary));
            OnPropertyChanged(nameof(MonitorStatusDetails));
        }
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

    private void OnTemplateMonitorStatusChanged(object? sender, TemplateMonitorStatusChangedEventArgs e)
    {
        if (!string.Equals(e.Status.MonitorId, CraftingLogMonitorId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            ApplyStatus(e.Status);
            return;
        }

        _ = Application.Current.Dispatcher.InvokeAsync(() => ApplyStatus(e.Status));
    }

    private void RefreshMonitorStatus()
    {
        TemplateMonitorStatus? status = templateMonitorStatusSource.GetStatus(CraftingLogMonitorId);
        if (status is null)
        {
            ApplyStatus(new TemplateMonitorStatus(CraftingLogMonitorId, TemplateMonitorState.Stopped, null, null, null, null, 0, null, null, null));
            return;
        }

        ApplyStatus(status);
    }

    private void ApplyStatus(TemplateMonitorStatus status)
    {
        monitorStatusSummary = $"状態: {status.State}";
        monitorStatusDetails =
            $"開始: {FormatTimestamp(status.StartedAt ?? status.StartRequestedAt)} / " +
            $"初回フレーム: {FormatTimestamp(status.FirstFrameCompletedAt)} / " +
            $"停止: {FormatTimestamp(status.StoppedAt)} / " +
            $"フレーム数: {status.ProcessedFrameCount} / " +
            $"最新結果: {status.LastMatchStatus?.ToString() ?? "-"} / " +
            $"Best score: {FormatScore(status.LastScore)}" +
            (string.IsNullOrWhiteSpace(status.ErrorMessage) ? string.Empty : $" / エラー: {status.ErrorMessage}");
        OnPropertyChanged(nameof(MonitorStatusSummary));
        OnPropertyChanged(nameof(MonitorStatusDetails));
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "-";
    }

    private static string FormatScore(double? score)
    {
        return score is double value ? value.ToString("F3") : "-";
    }
}
