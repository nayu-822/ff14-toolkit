using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.OverlayPlugin;
using FF14Toolkit.App.Services.GameData;
using System.Text.Json;
using System.Windows;

namespace FF14Toolkit.App.Services.OverlayPlugin;

public sealed class OverlayPluginConnectionStateService : ObservableObject, IDisposable
{
    private static readonly string[] DefaultSubscribedEvents =
    [
        "LogLine",
        "ChangeZone",
        "ChangePrimaryPlayer",
        "ChangeMap"
    ];

    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly IGameDataService gameDataService;
    private readonly OverlayPluginLogService logService;
    private readonly IOverlayPluginWebSocketSessionService sessionService;
    private TaskCompletionSource<bool> initialChangeMapReceived = CreateSignal();
    private TaskCompletionSource<bool> initialChangeZoneReceived = CreateSignal();
    private CancellationTokenSource? monitorCancellationTokenSource;
    private Task? monitorTask;
    private int autoConnectAttempted;
    private bool isConnected;
    private string characterName = "-";
    private string worldName = "-";
    private string currentMapName = "-";
    private string currentCoordinatesText = "-";
    private int currentJobId;
    private int currentLevel;
    private long currentHp;
    private long maxHp;
    private long currentMp;
    private long maxMp;
    private long currentGp;
    private long maxGp;
    private long currentCp;
    private long maxCp;
    private double rawPosX;
    private double rawPosY;
    private double rawPosZ;
    private double currentHeading;
    private string? primaryPlayerName;
    private uint? currentMapId;
    private uint? currentTerritoryTypeId;

    public OverlayPluginConnectionStateService(
        IOverlayPluginWebSocketSessionService sessionService,
        IGameDataService gameDataService,
        OverlayPluginLogService logService)
    {
        this.sessionService = sessionService;
        this.gameDataService = gameDataService;
        this.logService = logService;
        this.sessionService.EventReceived += OnSessionEventReceived;
        this.sessionService.ConnectionStateChanged += OnSessionConnectionStateChanged;
        isConnected = sessionService.IsStarted;
    }

    public bool IsConnected
    {
        get => isConnected;
        private set
        {
            if (SetProperty(ref isConnected, value))
            {
                OnPropertyChanged(nameof(CharacterWorldDisplay));
            }
        }
    }

    public string CharacterName
    {
        get => characterName;
        private set
        {
            if (SetProperty(ref characterName, value))
            {
                OnPropertyChanged(nameof(CharacterWorldDisplay));
            }
        }
    }

    public string WorldName
    {
        get => worldName;
        private set
        {
            if (SetProperty(ref worldName, value))
            {
                OnPropertyChanged(nameof(CharacterWorldDisplay));
            }
        }
    }

    public string CharacterWorldDisplay => IsConnected
        ? $"{CharacterName} / {WorldName}"
        : "- / -";

    public string CurrentMapName
    {
        get => currentMapName;
        private set => SetProperty(ref currentMapName, value);
    }

    public string CurrentCoordinatesText
    {
        get => currentCoordinatesText;
        private set => SetProperty(ref currentCoordinatesText, value);
    }

    public int CurrentJobId
    {
        get => currentJobId;
        private set => SetProperty(ref currentJobId, value);
    }

    public int CurrentLevel
    {
        get => currentLevel;
        private set => SetProperty(ref currentLevel, value);
    }

    public long CurrentHp
    {
        get => currentHp;
        private set => SetProperty(ref currentHp, value);
    }

    public long MaxHp
    {
        get => maxHp;
        private set => SetProperty(ref maxHp, value);
    }

    public long CurrentMp
    {
        get => currentMp;
        private set => SetProperty(ref currentMp, value);
    }

    public long MaxMp
    {
        get => maxMp;
        private set => SetProperty(ref maxMp, value);
    }

    public long CurrentGp
    {
        get => currentGp;
        private set => SetProperty(ref currentGp, value);
    }

    public long MaxGp
    {
        get => maxGp;
        private set => SetProperty(ref maxGp, value);
    }

    public long CurrentCp
    {
        get => currentCp;
        private set => SetProperty(ref currentCp, value);
    }

    public long MaxCp
    {
        get => maxCp;
        private set => SetProperty(ref maxCp, value);
    }

    public double RawPosX
    {
        get => rawPosX;
        private set => SetProperty(ref rawPosX, value);
    }

    public double RawPosY
    {
        get => rawPosY;
        private set => SetProperty(ref rawPosY, value);
    }

    public double RawPosZ
    {
        get => rawPosZ;
        private set => SetProperty(ref rawPosZ, value);
    }

    public double CurrentHeading
    {
        get => currentHeading;
        private set => SetProperty(ref currentHeading, value);
    }

    public uint? CurrentMapId
    {
        get => currentMapId;
        private set => SetProperty(ref currentMapId, value);
    }

    public uint? CurrentTerritoryTypeId
    {
        get => currentTerritoryTypeId;
        private set => SetProperty(ref currentTerritoryTypeId, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref autoConnectAttempted, 1) == 1)
        {
            return;
        }

        try
        {
            await StartAsync(cancellationToken);
        }
        catch
        {
            ResetConnectionInfo();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        initialChangeMapReceived = CreateSignal();
        initialChangeZoneReceived = CreateSignal();
        GameDataStatus gameDataStatus = await gameDataService.CheckAvailabilityAsync().ConfigureAwait(false);
        logService.LogInformation(
            $"Overlay startup: Lumina state={gameDataStatus.State}, available={gameDataStatus.IsAvailable}, sqPackPath={gameDataStatus.SqPackPath ?? "-"}");
        await sessionService.StartAsync(cancellationToken);
        logService.LogInformation("Overlay startup: WSServer connected.");
        await sessionService.SubscribeAsync(DefaultSubscribedEvents, cancellationToken);
        logService.LogInformation($"Overlay startup: subscribed events={string.Join(", ", DefaultSubscribedEvents)}");
        await WaitForInitialMapContextAsync(cancellationToken);
        UpdateConnectionState(true);
        StartMonitorLoop();
        await RefreshCharacterStateAsync(cancellationToken);
    }

    public void Dispose()
    {
        sessionService.EventReceived -= OnSessionEventReceived;
        sessionService.ConnectionStateChanged -= OnSessionConnectionStateChanged;
        StopMonitorLoop();
        refreshGate.Dispose();
    }

    private void OnSessionConnectionStateChanged(object? sender, EventArgs e)
    {
        if (sessionService.IsStarted)
        {
            UpdateConnectionState(true);
            StartMonitorLoop();
            _ = RefreshCharacterStateAsync();
            return;
        }

        StopMonitorLoop();
        ResetConnectionInfo();
    }

    private void OnSessionEventReceived(object? sender, OverlayPluginEventReceivedEventArgs e)
    {
        if (!sessionService.IsStarted)
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(e.RawJson);
            JsonElement root = document.RootElement;

            switch (e.EventType)
            {
                case "ChangePrimaryPlayer":
                    HandlePrimaryPlayerChanged(root);
                    _ = RefreshCharacterStateAsync();
                    break;
                case "ChangeMap":
                    HandleMapChanged(root);
                    break;
                case "ChangeZone":
                    HandleZoneChanged(root);
                    break;
                case "LogLine":
                    HandleLogLine(root);
                    break;
            }
        }
        catch
        {
        }
    }

    private void HandlePrimaryPlayerChanged(JsonElement root)
    {
        string? nextPrimaryPlayerName = FirstNonEmptyString(
            TryGetString(root, "charName"),
            TryGetString(root, "name"),
            TryGetString(root, "playerName"));
        if (!string.IsNullOrWhiteSpace(nextPrimaryPlayerName))
        {
            primaryPlayerName = nextPrimaryPlayerName;
        }
    }

    private void HandleMapChanged(JsonElement root)
    {
        CurrentMapId = TryFindUInt(root, "mapID")
            ?? TryFindUInt(root, "mapId")
            ?? TryFindUInt(root, "MapID")
            ?? TryFindUInt(root, "MapId")
            ?? TryFindUInt(root, "id");

        string? mapName = FirstNonEmptyString(
            TryFindString(root, "mapName"),
            TryFindString(root, "MapName"),
            TryFindString(root, "zoneName"),
            TryFindString(root, "ZoneName"),
            TryFindString(root, "placeName"),
            TryFindString(root, "PlaceName"),
            TryFindString(root, "name"),
            TryFindString(root, "Name"));

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            ExecuteOnUiThread(() => CurrentMapName = mapName);
        }

        initialChangeMapReceived.TrySetResult(true);
        logService.LogInformation($"Overlay event ChangeMap: mapId={CurrentMapId?.ToString() ?? "-"}, mapName={mapName ?? "-"}");
        _ = RefreshCharacterStateAsync();
    }

    private void HandleZoneChanged(JsonElement root)
    {
        CurrentTerritoryTypeId = TryFindUInt(root, "zoneID")
            ?? TryFindUInt(root, "zoneId")
            ?? TryFindUInt(root, "ZoneID")
            ?? TryFindUInt(root, "ZoneId")
            ?? TryFindUInt(root, "territoryTypeID")
            ?? TryFindUInt(root, "territoryTypeId")
            ?? TryFindUInt(root, "TerritoryTypeID")
            ?? TryFindUInt(root, "TerritoryTypeId");
        CurrentMapId ??= TryFindUInt(root, "mapID")
            ?? TryFindUInt(root, "mapId")
            ?? TryFindUInt(root, "MapID")
            ?? TryFindUInt(root, "MapId");

        string? mapName = FirstNonEmptyString(
            TryFindString(root, "zoneName"),
            TryFindString(root, "ZoneName"),
            TryFindString(root, "name"),
            TryFindString(root, "Name"),
            TryFindString(root, "placeName"),
            TryFindString(root, "PlaceName"));

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            ExecuteOnUiThread(() => CurrentMapName = mapName);
        }

        initialChangeZoneReceived.TrySetResult(true);
        logService.LogInformation(
            $"Overlay event ChangeZone: territoryTypeId={CurrentTerritoryTypeId?.ToString() ?? "-"}, mapName={mapName ?? "-"}");
        _ = RefreshCharacterStateAsync();
    }

    private void HandleLogLine(JsonElement root)
    {
        if (!root.TryGetProperty("line", out JsonElement lineElement) || lineElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        string[] tokens = lineElement.EnumerateArray()
            .Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
            .ToArray();

        if (tokens.Length == 0)
        {
            return;
        }

        int typeIndex = Array.FindIndex(tokens, static item => item is "01" or "02" or "03" or "40");
        if (typeIndex < 0)
        {
            return;
        }

        if (tokens[typeIndex] == "01")
        {
            if (tokens.Length > typeIndex + 1 && uint.TryParse(tokens[typeIndex + 1], out uint zoneId))
            {
                CurrentTerritoryTypeId = zoneId;
            }

            string? zoneName = tokens.LastOrDefault(static item => !string.IsNullOrWhiteSpace(item));
            if (!string.IsNullOrWhiteSpace(zoneName))
            {
                ExecuteOnUiThread(() => CurrentMapName = zoneName);
            }

            logService.LogInformation(
                $"Overlay logline 01: territoryTypeId={CurrentTerritoryTypeId?.ToString() ?? "-"}, zoneName={zoneName ?? "-"}");
        }

        if (tokens[typeIndex] == "02" && tokens.Length > typeIndex + 1)
        {
            primaryPlayerName = tokens[typeIndex + 1];
        }
    }

    private async Task RefreshCharacterStateAsync(CancellationToken cancellationToken = default)
    {
        if (!sessionService.IsStarted)
        {
            ResetConnectionInfo();
            return;
        }

        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            string response = await sessionService.SendRequestAsync("getCombatants", cancellationToken: cancellationToken);
            OverlayPluginCombatantsResponse? combatantsResponse = JsonSerializer.Deserialize<OverlayPluginCombatantsResponse>(response);
            OverlayPluginCombatant? playerCombatant = FindPrimaryPlayerCombatant(combatantsResponse?.Combatants ?? []);

            if (playerCombatant is null)
            {
                UpdateConnectionState(true);
                return;
            }

            UpdateConnectionState(true);
            string formattedCoordinates = gameDataService.FormatMapCoordinates(
                CurrentMapId,
                CurrentTerritoryTypeId,
                CurrentMapName,
                playerCombatant.PosX,
                playerCombatant.PosY);
            string coordinateResolution = gameDataService.DescribeMapCoordinateResolution(
                CurrentMapId,
                CurrentTerritoryTypeId,
                CurrentMapName,
                playerCombatant.PosX,
                playerCombatant.PosY);
            ExecuteOnUiThread(() =>
            {
                CharacterName = string.IsNullOrWhiteSpace(playerCombatant.Name) ? "-" : playerCombatant.Name;
                WorldName = string.IsNullOrWhiteSpace(playerCombatant.WorldName) ? "-" : playerCombatant.WorldName;
                CurrentJobId = playerCombatant.JobId;
                CurrentLevel = playerCombatant.Level;
                CurrentHp = playerCombatant.CurrentHp;
                MaxHp = playerCombatant.MaxHp;
                CurrentMp = playerCombatant.CurrentMp;
                MaxMp = playerCombatant.MaxMp;
                CurrentGp = playerCombatant.CurrentGp;
                MaxGp = playerCombatant.MaxGp;
                CurrentCp = playerCombatant.CurrentCp;
                MaxCp = playerCombatant.MaxCp;
                RawPosX = playerCombatant.PosX;
                RawPosY = playerCombatant.PosY;
                RawPosZ = playerCombatant.PosZ;
                CurrentHeading = playerCombatant.Heading;
                CurrentCoordinatesText = formattedCoordinates;
            });
            logService.LogInformation(
                $"Overlay getCombatants: player={playerCombatant.Name}, world={playerCombatant.WorldName}, mapId={CurrentMapId?.ToString() ?? "-"}, territoryTypeId={CurrentTerritoryTypeId?.ToString() ?? "-"}, mapName={CurrentMapName}, rawPosX={playerCombatant.PosX:0.000}, rawPosY={playerCombatant.PosY:0.000}, rawPosZ={playerCombatant.PosZ:0.000}, displayed={formattedCoordinates}");
            logService.LogInformation($"Overlay coordinate resolution: {coordinateResolution}");

            if (!string.IsNullOrWhiteSpace(playerCombatant.Name))
            {
                primaryPlayerName = playerCombatant.Name;
            }
        }
        catch (Exception exception)
        {
            logService.LogError("Overlay getCombatants refresh failed.", exception);
            if (!sessionService.IsStarted)
            {
                ResetConnectionInfo();
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private OverlayPluginCombatant? FindPrimaryPlayerCombatant(IReadOnlyList<OverlayPluginCombatant> combatants)
    {
        IEnumerable<OverlayPluginCombatant> players = combatants.Where(static item => item.Type == 1);

        if (!string.IsNullOrWhiteSpace(primaryPlayerName))
        {
            OverlayPluginCombatant? matched = players.FirstOrDefault(item =>
                string.Equals(item.Name, primaryPlayerName, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched;
            }
        }

        if (!string.IsNullOrWhiteSpace(CharacterName) && CharacterName != "-")
        {
            OverlayPluginCombatant? matched = players.FirstOrDefault(item =>
                string.Equals(item.Name, CharacterName, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched;
            }
        }

        return players.FirstOrDefault();
    }

    private void StartMonitorLoop()
    {
        if (monitorTask is not null && !monitorTask.IsCompleted)
        {
            return;
        }

        StopMonitorLoop();
        monitorCancellationTokenSource = new CancellationTokenSource();
        monitorTask = Task.Run(() => MonitorLoopAsync(monitorCancellationTokenSource.Token));
    }

    private void StopMonitorLoop()
    {
        monitorCancellationTokenSource?.Cancel();
        monitorCancellationTokenSource?.Dispose();
        monitorCancellationTokenSource = null;
        monitorTask = null;
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(2));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshCharacterStateAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateConnectionState(bool connected)
    {
        ExecuteOnUiThread(() => IsConnected = connected);
    }

    private void ResetConnectionInfo()
    {
        ExecuteOnUiThread(() =>
        {
            IsConnected = false;
            CharacterName = "-";
            WorldName = "-";
            CurrentMapName = "-";
            CurrentCoordinatesText = "-";
            CurrentJobId = 0;
            CurrentLevel = 0;
            CurrentHp = 0;
            MaxHp = 0;
            CurrentMp = 0;
            MaxMp = 0;
            CurrentGp = 0;
            MaxGp = 0;
            CurrentCp = 0;
            MaxCp = 0;
            RawPosX = 0;
            RawPosY = 0;
            RawPosZ = 0;
            CurrentHeading = 0;
        });
        primaryPlayerName = null;
        CurrentMapId = null;
        CurrentTerritoryTypeId = null;
        initialChangeMapReceived = CreateSignal();
        initialChangeZoneReceived = CreateSignal();
    }

    private static void ExecuteOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            _ = Application.Current.Dispatcher.InvokeAsync(action);
            return;
        }

        action();
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static string? FirstNonEmptyString(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }

    private async Task WaitForInitialMapContextAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            await Task.WhenAll(initialChangeMapReceived.Task, initialChangeZoneReceived.Task).WaitAsync(timeoutTokenSource.Token);
            logService.LogInformation(
                $"Overlay startup: initial map context received. mapId={CurrentMapId?.ToString() ?? "-"}, territoryTypeId={CurrentTerritoryTypeId?.ToString() ?? "-"}, mapName={CurrentMapName}");
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation(
                $"Overlay startup: initial map context wait timed out. mapId={CurrentMapId?.ToString() ?? "-"}, territoryTypeId={CurrentTerritoryTypeId?.ToString() ?? "-"}, mapName={CurrentMapName}");
        }
    }

    private static TaskCompletionSource<bool> CreateSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static uint? TryGetUInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt32(out uint numberValue))
        {
            return numberValue;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            string? text = element.GetString();
            if (uint.TryParse(text, out uint parsedValue))
            {
                return parsedValue;
            }

            if (!string.IsNullOrWhiteSpace(text)
                && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && uint.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out uint hexValue))
            {
                return hexValue;
            }
        }

        return null;
    }

    private static string? TryFindString(JsonElement root, string propertyName)
    {
        if (TryGetString(root, propertyName) is string value)
        {
            return value;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                string? nestedValue = TryFindString(property.Value, propertyName);
                if (!string.IsNullOrWhiteSpace(nestedValue))
                {
                    return nestedValue;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in root.EnumerateArray())
            {
                string? nestedValue = TryFindString(item, propertyName);
                if (!string.IsNullOrWhiteSpace(nestedValue))
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }

    private static uint? TryFindUInt(JsonElement root, string propertyName)
    {
        if (TryGetUInt(root, propertyName) is uint value)
        {
            return value;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                uint? nestedValue = TryFindUInt(property.Value, propertyName);
                if (nestedValue.HasValue)
                {
                    return nestedValue;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in root.EnumerateArray())
            {
                uint? nestedValue = TryFindUInt(item, propertyName);
                if (nestedValue.HasValue)
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }
}
