using System;
using System.IO;
using FF14Toolkit.App.Services.Configuration;

namespace FF14Toolkit.App.Services.Hotbar;

public sealed class HotbarPathResolver
{
    private readonly CharacterSettingsStore characterSettingsStore;

    public HotbarPathResolver(CharacterSettingsStore characterSettingsStore)
    {
        this.characterSettingsStore = characterSettingsStore;
    }

    public string Resolve(string path)
    {
        path = string.IsNullOrWhiteSpace(path)
            ? characterSettingsStore.RootPath
            : path;

        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Directory.Exists(path))
        {
            string resolvedFromDirectory = Path.Combine(path, "HOTBAR.DAT");

            if (!File.Exists(resolvedFromDirectory))
            {
                throw new FileNotFoundException("HOTBAR.DAT was not found in the specified directory.", resolvedFromDirectory);
            }

            return resolvedFromDirectory;
        }

        if (File.Exists(path))
        {
            if (!string.Equals(Path.GetFileName(path), "HOTBAR.DAT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The specified file is not HOTBAR.DAT.");
            }

            return path;
        }

        if (string.Equals(Path.GetFileName(path), "HOTBAR.DAT", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("HOTBAR.DAT was not found at the specified path.", path);
        }

        throw new InvalidOperationException("Specify HOTBAR.DAT or a character settings directory.");
    }
}
