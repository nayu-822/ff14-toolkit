using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Hotbar;
using FF14Toolkit.App.Services.Keybind;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.ViewModels;
using FF14Toolkit.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FF14Toolkit.App.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddOptions<LocalizationOptions>()
            .BindConfiguration("Localization");
        services.AddOptions<CacheOptions>()
            .BindConfiguration("Cache");
        services.AddOptions<LuminaOptions>()
            .BindConfiguration("Lumina");
        services.AddSingleton(LocalizationService.Instance);
        services.AddSingleton<ILocalizationService>(serviceProvider => serviceProvider.GetRequiredService<LocalizationService>());
        services.AddSingleton<IGameDataService, LuminaGameDataService>();
        services.AddSingleton<HotbarDatParser>();
        services.AddSingleton<HotbarPathResolver>();
        services.AddSingleton<IHotbarDataService, HotbarDataService>();
        services.AddSingleton<KeybindDatParser>();
        services.AddSingleton<KeybindPathResolver>();
        services.AddSingleton<IKeybindDataService, KeybindDataService>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
