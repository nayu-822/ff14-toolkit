using System.IO;

namespace FF14Toolkit.App.Services.Addon;

public sealed class AddonPathResolver
{
    public string Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("A character settings path is not configured.");
        }

        path = Environment.ExpandEnvironmentVariables(path);
        path = Path.GetFullPath(path);

        if (Directory.Exists(path))
        {
            string resolvedFromDirectory = Path.Combine(path, "ADDON.DAT");
            if (!File.Exists(resolvedFromDirectory))
            {
                throw new FileNotFoundException("ADDON.DAT was not found in the specified directory.", resolvedFromDirectory);
            }

            return resolvedFromDirectory;
        }

        if (File.Exists(path))
        {
            if (!string.Equals(Path.GetFileName(path), "ADDON.DAT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The specified file is not ADDON.DAT.");
            }

            return path;
        }

        if (string.Equals(Path.GetFileName(path), "ADDON.DAT", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("ADDON.DAT was not found at the specified path.", path);
        }

        throw new InvalidOperationException("Specify ADDON.DAT or a character settings directory.");
    }
}
