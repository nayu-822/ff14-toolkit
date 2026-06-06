using FF14Toolkit.App.DependencyInjection;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Windows;

namespace FF14Toolkit.App;

public partial class App : Application
{
    private IHost? host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationServices();
            })
            .Build();

        await host.StartAsync();

        var localizationOptions = host.Services.GetRequiredService<IOptions<LocalizationOptions>>().Value;
        var localizationService = host.Services.GetRequiredService<ILocalizationService>();
        localizationService.Initialize(localizationOptions.DefaultCulture, localizationOptions.SupportedCultures);

        var mainWindow = host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }

        base.OnExit(e);
    }
}
