using FF14Toolkit.App.Models.OverlayPlugin;
using System.Text.Json;

namespace FF14Toolkit.App.Services.OverlayPlugin;

public interface IOverlayPluginWebSocketService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

    Task<JsonDocument> CallAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<OverlayPluginLanguageInfo?> GetLanguageAsync(CancellationToken cancellationToken = default);

    Task<OverlayPluginVersionInfo?> GetVersionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OverlayPluginCombatant>> GetCombatantsAsync(CancellationToken cancellationToken = default);
}
