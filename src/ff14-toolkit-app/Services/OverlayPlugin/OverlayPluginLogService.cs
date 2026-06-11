using FF14Toolkit.App.Models.Configuration;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text;

namespace FF14Toolkit.App.Services.OverlayPlugin;

public sealed class OverlayPluginLogService
{
    private readonly string logFilePath;
    private readonly Lock syncRoot = new();

    public OverlayPluginLogService(IOptions<CacheOptions> cacheOptions)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.Value.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        logFilePath = Path.Combine(rootPath, "Logs", "overlayplugin.log");
    }

    public void LogInformation(string message)
    {
        Write("INFO", message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        if (exception is null)
        {
            Write("ERROR", message);
            return;
        }

        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private void Write(string level, string message)
    {
        try
        {
            string? directoryPath = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            StringBuilder builder = new();
            builder.Append('[');
            builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            builder.Append("] [");
            builder.Append(level);
            builder.Append("] ");
            builder.AppendLine(message);

            lock (syncRoot)
            {
                File.AppendAllText(logFilePath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}
