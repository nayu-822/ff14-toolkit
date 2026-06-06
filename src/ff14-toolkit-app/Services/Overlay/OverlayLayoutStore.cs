using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.Overlay;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class OverlayLayoutStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;

    public OverlayLayoutStore(IOptions<CacheOptions> cacheOptions)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        storagePath = ResolveStoragePath(cacheOptions.Value);
    }

    public IReadOnlyList<OverlayWindowLayout> LoadLayouts()
    {
        if (!File.Exists(storagePath))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(storagePath);
            OverlayLayoutSettings? settings = JsonSerializer.Deserialize<OverlayLayoutSettings>(json, SerializerOptions);
            return settings?.Windows ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SaveLayouts(IEnumerable<OverlayWindowLayout> layouts)
    {
        string? directoryPath = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        OverlayLayoutSettings settings = new()
        {
            Windows = layouts.ToList()
        };

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    private static string ResolveStoragePath(CacheOptions cacheOptions)
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        return Path.Combine(rootPath, "Overlay", "overlay-window-layouts.json");
    }
}
