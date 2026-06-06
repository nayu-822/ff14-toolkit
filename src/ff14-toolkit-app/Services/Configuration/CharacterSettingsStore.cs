using FF14Toolkit.App.Models.Configuration;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace FF14Toolkit.App.Services.Configuration;

public sealed class CharacterSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;
    private string rootPath;

    public CharacterSettingsStore(
        IOptions<CacheOptions> cacheOptions,
        IOptions<CharacterSettingsOptions> characterSettingsOptions)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        if (characterSettingsOptions is null)
        {
            throw new ArgumentNullException(nameof(characterSettingsOptions));
        }

        storagePath = ResolveStoragePath(cacheOptions.Value);
        rootPath = characterSettingsOptions.Value.RootPath ?? string.Empty;

        CharacterSettingsState? persistedState = Load();
        if (persistedState is not null)
        {
            rootPath = persistedState.RootPath ?? string.Empty;
        }
    }

    public string RootPath => rootPath;

    public void Save(string path)
    {
        rootPath = path?.Trim() ?? string.Empty;

        string? directoryPath = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        CharacterSettingsState state = new()
        {
            RootPath = rootPath
        };

        string json = JsonSerializer.Serialize(state, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    private CharacterSettingsState? Load()
    {
        if (!File.Exists(storagePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(storagePath);
            return JsonSerializer.Deserialize<CharacterSettingsState>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveStoragePath(CacheOptions cacheOptions)
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        return Path.Combine(rootPath, "Settings", "character-settings.json");
    }
}
