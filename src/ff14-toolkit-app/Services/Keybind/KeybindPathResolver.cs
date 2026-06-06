using System;
using System.IO;

namespace FF14Toolkit.App.Services.Keybind;

public sealed class KeybindPathResolver
{
    public string Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Directory.Exists(path))
        {
            string resolvedFromDirectory = Path.Combine(path, "KEYBIND.DAT");

            if (!File.Exists(resolvedFromDirectory))
            {
                throw new FileNotFoundException("KEYBIND.DAT was not found in the specified directory.", resolvedFromDirectory);
            }

            return resolvedFromDirectory;
        }

        if (File.Exists(path))
        {
            if (!string.Equals(Path.GetFileName(path), "KEYBIND.DAT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The specified file is not KEYBIND.DAT.");
            }

            return path;
        }

        if (string.Equals(Path.GetFileName(path), "KEYBIND.DAT", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("KEYBIND.DAT was not found at the specified path.", path);
        }

        throw new InvalidOperationException("Specify KEYBIND.DAT or a character settings directory.");
    }
}
