using System;
using System.IO;

namespace FF14Toolkit.App.Services.Hotbar;

public sealed class HotbarPathResolver
{
    public string Resolve(string path)
    {
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
