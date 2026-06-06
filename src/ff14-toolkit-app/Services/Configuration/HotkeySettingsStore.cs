using FF14Toolkit.App.Models.Configuration;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace FF14Toolkit.App.Services.Configuration;

public sealed class HotkeySettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;
    private HotkeySettingsState state;

    public HotkeySettingsStore(
        IOptions<CacheOptions> cacheOptions,
        IOptions<HotkeySettingsOptions> hotkeyOptions)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        if (hotkeyOptions is null)
        {
            throw new ArgumentNullException(nameof(hotkeyOptions));
        }

        storagePath = ResolveStoragePath(cacheOptions.Value);
        state = new HotkeySettingsState
        {
            ToggleOverlayHotKey = hotkeyOptions.Value.ToggleOverlayHotKey,
            ToggleOverlayEditHotKey = hotkeyOptions.Value.ToggleOverlayEditHotKey
        };

        HotkeySettingsState? persistedState = Load();
        if (persistedState is not null)
        {
            state = persistedState;
        }

        NormalizeState();
    }

    public event EventHandler? SettingsChanged;

    public string ToggleOverlayHotKey => state.ToggleOverlayHotKey;

    public string ToggleOverlayEditHotKey => state.ToggleOverlayEditHotKey;

    public void Save(string toggleOverlayHotKey, string toggleOverlayEditHotKey)
    {
        state.ToggleOverlayHotKey = toggleOverlayHotKey?.Trim() ?? string.Empty;
        state.ToggleOverlayEditHotKey = toggleOverlayEditHotKey?.Trim() ?? string.Empty;
        NormalizeState();
        Persist();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Persist()
    {
        string? directoryPath = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string json = JsonSerializer.Serialize(state, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    private HotkeySettingsState? Load()
    {
        if (!File.Exists(storagePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(storagePath);
            return JsonSerializer.Deserialize<HotkeySettingsState>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private void NormalizeState()
    {
        if (string.IsNullOrWhiteSpace(state.ToggleOverlayHotKey))
        {
            state.ToggleOverlayHotKey = "Ctrl+Shift+O";
        }

        if (string.IsNullOrWhiteSpace(state.ToggleOverlayEditHotKey))
        {
            state.ToggleOverlayEditHotKey = "Ctrl+Shift+P";
        }
    }

    private static string ResolveStoragePath(CacheOptions cacheOptions)
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        return Path.Combine(rootPath, "Settings", "hotkey-settings.json");
    }
}
