using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.OverlayPlugin;
using System.Collections.ObjectModel;
using System.Windows;

namespace FF14Toolkit.App.ViewModels;

public sealed class OverlayPluginWSServerContentViewModel : ShellContentViewModel
{
    private static readonly string[] DefaultSubscribedEvents =
    [
        "LogLine",
        "ChangeZone",
        "ChangePrimaryPlayer",
        "AddCombatant",
        "RemoveCombatant",
        "PartyList",
        "PlayerStats",
        "ChangeMap"
    ];

    private readonly ILocalizationService localizationService;
    private readonly IOverlayPluginWebSocketSessionService sessionService;
    private readonly RelayCommand startCommand;
    private readonly RelayCommand stopCommand;
    private readonly RelayCommand sendGetLanguageCommand;
    private readonly RelayCommand sendGetVersionCommand;
    private readonly RelayCommand sendGetCombatantsCommand;
    private readonly RelayCommand sendLoadDataCommand;
    private readonly RelayCommand sendGetConfigCommand;
    private readonly RelayCommand sendGetEnmityCommand;
    private readonly RelayCommand sendGetEncounterCommand;
    private readonly RelayCommand sendGetPartyListCommand;
    private readonly RelayCommand sendGetCombatDataCommand;
    private string connectionStateText;

    public OverlayPluginWSServerContentViewModel(
        ILocalizationService localizationService,
        IOverlayPluginWebSocketSessionService sessionService)
        : base(
            "development-overlayplugin-wsserver",
            "Nav_DevelopmentOverlayPluginWSServer",
            "Section_DevelopmentOverlayPluginWSServer_Description",
            localizationService)
    {
        this.localizationService = localizationService;
        this.sessionService = sessionService;
        connectionStateText = localizationService["Development_OverlayPluginWSServer_Disconnected"];
        LogEntries = [];
        this.sessionService.EventReceived += OnSessionEventReceived;
        this.sessionService.ConnectionStateChanged += OnSessionConnectionStateChanged;

        startCommand = new RelayCommand(() => _ = StartAsync(), CanStart);
        stopCommand = new RelayCommand(() => _ = StopAsync(), CanStop);
        sendGetLanguageCommand = new RelayCommand(() => _ = SendAsync("getLanguage"), CanSendRequest);
        sendGetVersionCommand = new RelayCommand(() => _ = SendAsync("getVersion"), CanSendRequest);
        sendGetCombatantsCommand = new RelayCommand(() => _ = SendAsync("getCombatants"), CanSendRequest);
        sendLoadDataCommand = new RelayCommand(() => _ = SendAsync("loadData", new Dictionary<string, object?> { ["key"] = "ff14-toolkit-probe" }), CanSendRequest);
        sendGetConfigCommand = new RelayCommand(() => _ = SendAsync("getConfig"), CanSendRequest);
        sendGetEnmityCommand = new RelayCommand(() => _ = SendAsync("getEnmity"), CanSendRequest);
        sendGetEncounterCommand = new RelayCommand(() => _ = SendAsync("getEncounter"), CanSendRequest);
        sendGetPartyListCommand = new RelayCommand(() => _ = SendAsync("getPartyList"), CanSendRequest);
        sendGetCombatDataCommand = new RelayCommand(() => _ = SendAsync("getCombatData"), CanSendRequest);
    }

    public ObservableCollection<string> LogEntries { get; }

    public RelayCommand StartCommand => startCommand;

    public RelayCommand StopCommand => stopCommand;

    public RelayCommand SendGetLanguageCommand => sendGetLanguageCommand;

    public RelayCommand SendGetVersionCommand => sendGetVersionCommand;

    public RelayCommand SendGetCombatantsCommand => sendGetCombatantsCommand;

    public RelayCommand SendLoadDataCommand => sendLoadDataCommand;

    public RelayCommand SendGetConfigCommand => sendGetConfigCommand;

    public RelayCommand SendGetEnmityCommand => sendGetEnmityCommand;

    public RelayCommand SendGetEncounterCommand => sendGetEncounterCommand;

    public RelayCommand SendGetPartyListCommand => sendGetPartyListCommand;

    public RelayCommand SendGetCombatDataCommand => sendGetCombatDataCommand;

    public string ConnectionCardTitle => localizationService["Development_OverlayPluginWSServer_ConnectionCardTitle"];

    public string ConnectionStateLabel => localizationService["Development_OverlayPluginWSServer_ConnectionStateLabel"];

    public string ConnectionStateText
    {
        get => connectionStateText;
        private set => SetProperty(ref connectionStateText, value);
    }

    public string StartButtonLabel => localizationService["Development_OverlayPluginWSServer_StartButton"];

    public string StopButtonLabel => localizationService["Development_OverlayPluginWSServer_StopButton"];

    public string RequestCardTitle => localizationService["Development_OverlayPluginWSServer_RequestCardTitle"];

    public string RequestSectionLabel => localizationService["Development_OverlayPluginWSServer_RequestSectionLabel"];

    public string ResponseCardTitle => localizationService["Development_OverlayPluginWSServer_ResponseCardTitle"];

    public string EmptyLogMessage => localizationService["Development_OverlayPluginWSServer_EmptyLogMessage"];

    public string GetLanguageButtonLabel => "getLanguage";

    public string GetVersionButtonLabel => "getVersion";

    public string GetCombatantsButtonLabel => "getCombatants";

    public string LoadDataButtonLabel => "loadData";

    public string GetConfigButtonLabel => "getConfig";

    public string GetEnmityButtonLabel => "getEnmity";

    public string GetEncounterButtonLabel => "getEncounter";

    public string GetPartyListButtonLabel => "getPartyList";

    public string GetCombatDataButtonLabel => "getCombatData";

    private bool CanStart() => !sessionService.IsStarted;

    private bool CanStop() => sessionService.IsStarted;

    private bool CanSendRequest() => sessionService.IsStarted;

    private async Task StartAsync()
    {
        try
        {
            await sessionService.StartAsync();
            await sessionService.SubscribeAsync(DefaultSubscribedEvents);
            ConnectionStateText = localizationService["Development_OverlayPluginWSServer_Connected"];
            AppendLog("INFO", "WebSocket started.");
            AppendLog("INFO", $"Subscribed: {string.Join(", ", DefaultSubscribedEvents)}");
            NotifyCommandStates();
        }
        catch (Exception exception)
        {
            ConnectionStateText = localizationService["Development_OverlayPluginWSServer_Disconnected"];
            AppendLog("ERROR", $"Start failed: {exception.Message}");
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await sessionService.StopAsync();
            ConnectionStateText = localizationService["Development_OverlayPluginWSServer_Disconnected"];
            AppendLog("INFO", "WebSocket stopped.");
            NotifyCommandStates();
        }
        catch (Exception exception)
        {
            AppendLog("ERROR", $"Stop failed: {exception.Message}");
        }
    }

    private async Task SendAsync(string call, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        try
        {
            AppendLog("SEND", BuildRequestSummary(call, parameters));
            string response = await sessionService.SendRequestAsync(call, parameters);
            AppendLog("RECV", response);
        }
        catch (Exception exception)
        {
            AppendLog("ERROR", $"{call}: {exception.Message}");
        }
        finally
        {
            RefreshConnectionState();
            NotifyCommandStates();
        }
    }

    private string BuildRequestSummary(string call, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return call;
        }

        return $"{call} {string.Join(", ", parameters.Select(pair => $"{pair.Key}={pair.Value ?? "null"}"))}";
    }

    private void AppendLog(string level, string message)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() => AppendLog(level, message));
            return;
        }

        string timestamp = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
        LogEntries.Add($"[{timestamp}] [{level}] {message}");
        OnPropertyChanged(nameof(HasLogEntries));
    }

    public bool HasLogEntries => LogEntries.Count > 0;

    private void NotifyCommandStates()
    {
        startCommand.NotifyCanExecuteChanged();
        stopCommand.NotifyCanExecuteChanged();
        sendGetLanguageCommand.NotifyCanExecuteChanged();
        sendGetVersionCommand.NotifyCanExecuteChanged();
        sendGetCombatantsCommand.NotifyCanExecuteChanged();
        sendLoadDataCommand.NotifyCanExecuteChanged();
        sendGetConfigCommand.NotifyCanExecuteChanged();
        sendGetEnmityCommand.NotifyCanExecuteChanged();
        sendGetEncounterCommand.NotifyCanExecuteChanged();
        sendGetPartyListCommand.NotifyCanExecuteChanged();
        sendGetCombatDataCommand.NotifyCanExecuteChanged();
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(ConnectionCardTitle));
        OnPropertyChanged(nameof(ConnectionStateLabel));
        OnPropertyChanged(nameof(StartButtonLabel));
        OnPropertyChanged(nameof(StopButtonLabel));
        OnPropertyChanged(nameof(RequestCardTitle));
        OnPropertyChanged(nameof(RequestSectionLabel));
        OnPropertyChanged(nameof(ResponseCardTitle));
        OnPropertyChanged(nameof(EmptyLogMessage));

        RefreshConnectionState();
    }

    private void RefreshConnectionState()
    {
        ConnectionStateText = sessionService.IsStarted
            ? localizationService["Development_OverlayPluginWSServer_Connected"]
            : localizationService["Development_OverlayPluginWSServer_Disconnected"];
    }

    private void OnSessionEventReceived(object? sender, OverlayPluginEventReceivedEventArgs e)
    {
        AppendLog("EVENT", $"{e.EventType}: {e.RawJson}");
    }

    private void OnSessionConnectionStateChanged(object? sender, EventArgs e)
    {
        RefreshConnectionState();
        NotifyCommandStates();
    }
}
