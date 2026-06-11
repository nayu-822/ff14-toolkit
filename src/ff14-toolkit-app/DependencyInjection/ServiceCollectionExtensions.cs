using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Hotbar;
using FF14Toolkit.App.Services.Keybind;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.Services.OverlayPlugin;
using FF14Toolkit.App.Services.TemplateMatching;
using FF14Toolkit.App.Services.TemplateMatching.Capture;
using FF14Toolkit.App.Services.TemplateMatching.Debug;
using FF14Toolkit.App.Services.TemplateMatching.Matching;
using FF14Toolkit.App.Services.TemplateMatching.Monitoring;
using FF14Toolkit.App.Services.TemplateMatching.Resources;
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
        services.AddOptions<DevelopmentOptions>()
            .BindConfiguration("Development");
        services.AddOptions<CharacterSettingsOptions>()
            .BindConfiguration("CharacterSettings");
        services.AddOptions<HotkeySettingsOptions>()
            .BindConfiguration("Hotkeys");
        services.AddOptions<OverlayPluginOptions>()
            .BindConfiguration("OverlayPlugin");
        services.AddOptions<LuminaOptions>()
            .BindConfiguration("Lumina");
        services.AddSingleton(LocalizationService.Instance);
        services.AddSingleton<ILocalizationService>(serviceProvider => serviceProvider.GetRequiredService<LocalizationService>());
        services.AddSingleton<CharacterSettingsStore>();
        services.AddSingleton<HotkeySettingsStore>();
        services.AddSingleton<HotkeyCaptureState>();
        services.AddSingleton<CraftActionSequenceStore>();
        services.AddSingleton<CraftSequenceHotkeyStore>();
        services.AddSingleton<CraftSequenceHotkeyLogService>();
        services.AddSingleton<CraftStartButtonAutomationService>();
        services.AddSingleton<CraftSequenceHotkeyExecutionService>();
        services.AddSingleton<IGameDataService, LuminaGameDataService>();
        services.AddSingleton<HotbarDatParser>();
        services.AddSingleton<HotbarPathResolver>();
        services.AddSingleton<IHotbarDataService, HotbarDataService>();
        services.AddSingleton<KeybindDatParser>();
        services.AddSingleton<KeybindPathResolver>();
        services.AddSingleton<IKeybindDataService, KeybindDataService>();
        services.AddSingleton<OverlayLayoutStore>();
        services.AddSingleton<OverlayWorkspaceService>();
        services.AddSingleton<IOverlayFrameStore, OverlayFrameStore>();
        services.AddSingleton<TemplateMatchOverlayFrameAdapter>();
        services.AddSingleton<TemplateMatchOverlayFrameFactory>();
        services.AddSingleton<TemplateMatchOverlayService>();
        services.AddSingleton<IOverlayService>(serviceProvider => serviceProvider.GetRequiredService<TemplateMatchOverlayService>());
        services.AddSingleton<IOverlayEventSource>(serviceProvider => serviceProvider.GetRequiredService<TemplateMatchOverlayService>());
        services.AddSingleton<ITemplateResourceLoader, PpmP6TemplateLoader>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<ITemplateMatcher, TemplateMatcher>();
        services.AddSingleton<ITemplateMatchResultSink, TemplateMatchResultPublisher>();
        services.AddSingleton<TemplateMatchResultPublisher>(serviceProvider => (TemplateMatchResultPublisher)serviceProvider.GetRequiredService<ITemplateMatchResultSink>());
        services.AddSingleton<TemplateMatchDebugVisibilityController>();
        services.AddSingleton<TemplateMatchDebugWindowService>();
        services.AddSingleton<ITemplateMatchDebugVisualizer, TemplateMatchDebugVisualizer>();
        services.AddSingleton<ITemplateMatchMonitor, TemplateMatchMonitor>();
        services.AddSingleton<ITemplateMonitorStatusSource>(serviceProvider => (TemplateMatchMonitor)serviceProvider.GetRequiredService<ITemplateMatchMonitor>());
        services.AddSingleton<IOverlayPluginWebSocketService, OverlayPluginWebSocketService>();
        services.AddSingleton<IOverlayPluginWebSocketSessionService, OverlayPluginWebSocketSessionService>();
        services.AddSingleton<OverlayPluginLogService>();
        services.AddSingleton<OverlayPluginConnectionStateService>();
        services.AddSingleton<OverlayPluginSnapshotService>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
