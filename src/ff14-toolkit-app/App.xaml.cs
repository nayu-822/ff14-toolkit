using FF14Toolkit.App.DependencyInjection;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.Services.OverlayPlugin;
using FF14Toolkit.App.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Windows;

namespace FF14Toolkit.App;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    private IHost? host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.Sources.Clear();
                configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationServices();
            })
            .Build();

        await host.StartAsync();
        Services = host.Services;

        var localizationOptions = host.Services.GetRequiredService<IOptions<LocalizationOptions>>().Value;
        var localizationService = host.Services.GetRequiredService<ILocalizationService>();
        localizationService.Initialize(localizationOptions.DefaultCulture, localizationOptions.SupportedCultures);
        var gameDataService = host.Services.GetRequiredService<IGameDataService>();
        var overlayPluginLogService = host.Services.GetRequiredService<OverlayPluginLogService>();
        GameDataStatus gameDataStatus = await gameDataService.CheckAvailabilityAsync();
        overlayPluginLogService.LogInformation(
            $"App startup: Lumina initialized. state={gameDataStatus.State}, available={gameDataStatus.IsAvailable}, sqPackPath={gameDataStatus.SqPackPath ?? "-"}");
        var overlayPluginConnectionStateService = host.Services.GetRequiredService<OverlayPluginConnectionStateService>();
        _ = overlayPluginConnectionStateService.InitializeAsync();
        overlayPluginLogService.LogInformation("App startup: OverlayPlugin auto-connect started.");

        var mainWindow = host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (host is not null)
        {
            host.Services.GetRequiredService<TemplateMatchOverlayService>().Shutdown();
            host.Services.GetRequiredService<OverlayWorkspaceService>().Shutdown();
            await host.StopAsync();
            host.Dispose();
        }

        Services = null;

        base.OnExit(e);
    }
}
