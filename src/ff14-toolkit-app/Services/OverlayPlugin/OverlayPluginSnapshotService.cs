using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.OverlayPlugin;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Infrastructure;

namespace FF14Toolkit.App.Services.OverlayPlugin;

public sealed class OverlayPluginSnapshotService : ObservableObject
{
    private readonly CharacterSettingsStore characterSettingsStore;
    private readonly IOverlayPluginWebSocketService overlayPluginWebSocketService;
    private OverlayPluginSnapshot currentSnapshot = new()
    {
        IsAvailable = false
    };

    public OverlayPluginSnapshotService(
        CharacterSettingsStore characterSettingsStore,
        IOverlayPluginWebSocketService overlayPluginWebSocketService)
    {
        this.characterSettingsStore = characterSettingsStore;
        this.overlayPluginWebSocketService = overlayPluginWebSocketService;
    }

    public OverlayPluginSnapshot CurrentSnapshot
    {
        get => currentSnapshot;
        private set => SetProperty(ref currentSnapshot, value);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            OverlayPluginVersionInfo? version = await overlayPluginWebSocketService.GetVersionAsync(cancellationToken);
            OverlayPluginLanguageInfo? language = await overlayPluginWebSocketService.GetLanguageAsync(cancellationToken);
            IReadOnlyList<OverlayPluginCombatant> combatants = await overlayPluginWebSocketService.GetCombatantsAsync(cancellationToken);

            CurrentSnapshot = new OverlayPluginSnapshot
            {
                IsAvailable = true,
                ErrorMessage = null,
                LastUpdatedAt = DateTimeOffset.Now,
                Version = version,
                Language = language,
                CombatantCount = combatants.Count,
                SelectedCharacterCombatant = FindSelectedCharacterCombatant(combatants, characterSettingsStore.SelectedProfile)
            };
        }
        catch (Exception exception)
        {
            CurrentSnapshot = new OverlayPluginSnapshot
            {
                IsAvailable = false,
                ErrorMessage = exception.Message,
                LastUpdatedAt = DateTimeOffset.Now
            };
        }
    }

    private static OverlayPluginCombatant? FindSelectedCharacterCombatant(
        IReadOnlyList<OverlayPluginCombatant> combatants,
        CharacterProfile? selectedProfile)
    {
        if (selectedProfile is null || string.IsNullOrWhiteSpace(selectedProfile.CharacterName))
        {
            return null;
        }

        return combatants.FirstOrDefault(combatant =>
            string.Equals(combatant.Name, selectedProfile.CharacterName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(selectedProfile.WorldName)
                || string.Equals(combatant.WorldName, selectedProfile.WorldName, StringComparison.OrdinalIgnoreCase)));
    }
}
