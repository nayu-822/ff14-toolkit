namespace FF14Toolkit.App.Models.OverlayPlugin;

public sealed class OverlayPluginSnapshot
{
    public bool IsAvailable { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTimeOffset? LastUpdatedAt { get; init; }

    public OverlayPluginVersionInfo? Version { get; init; }

    public OverlayPluginLanguageInfo? Language { get; init; }

    public OverlayPluginCombatant? SelectedCharacterCombatant { get; init; }

    public int CombatantCount { get; init; }
}
