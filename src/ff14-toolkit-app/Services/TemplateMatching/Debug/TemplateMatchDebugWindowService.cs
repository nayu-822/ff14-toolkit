using System.Windows;
using System.Windows.Threading;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Views;
using Microsoft.Extensions.Options;

namespace FF14Toolkit.App.Services.TemplateMatching.Debug;

public sealed class TemplateMatchDebugWindowService
{
    private readonly Dispatcher dispatcher;
    private readonly bool isEnabled;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly Dictionary<string, TemplateMatchDebugWindow> windows = new(StringComparer.OrdinalIgnoreCase);

    public TemplateMatchDebugWindowService(
        IOptions<DevelopmentOptions> developmentOptions,
        CraftSequenceHotkeyLogService logger)
    {
        dispatcher = Application.Current.Dispatcher;
        isEnabled = developmentOptions.Value.ShowTemplateMatchOverlay;
        this.logger = logger;
    }

    public Task ShowAsync(TemplateMatchDebugFrame frame, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isEnabled)
        {
            return Task.CompletedTask;
        }

        DispatcherOperation operation = dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (!windows.TryGetValue(frame.MonitorId, out TemplateMatchDebugWindow? window))
                {
                    window = new TemplateMatchDebugWindow();
                    windows.Add(frame.MonitorId, window);
                }

                window.ShowFrame(frame);
                if (!window.IsVisible)
                {
                    window.Show();
                }
            }
            catch (Exception exception)
            {
                logger.LogError("Normal template-match debug view update failed.", exception);
                throw;
            }
        });

        return ObserveAsync(operation);
    }

    public Task HideAsync(string monitorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isEnabled)
        {
            return Task.CompletedTask;
        }

        DispatcherOperation operation = dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (windows.TryGetValue(monitorId, out TemplateMatchDebugWindow? window))
                {
                    window.Hide();
                }
            }
            catch (Exception exception)
            {
                logger.LogError("Normal template-match debug view hide failed.", exception);
                throw;
            }
        });

        return ObserveAsync(operation);
    }

    private async Task ObserveAsync(DispatcherOperation operation)
    {
        try
        {
            await operation.Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError("Dispatcher exception logging: normal template-match debug view update failed.", exception);
        }
    }
}
