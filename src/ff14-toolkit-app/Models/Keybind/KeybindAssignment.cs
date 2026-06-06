namespace FF14Toolkit.App.Models.Keybind;

public sealed class KeybindAssignment
{
    public string KeyCode { get; init; } = string.Empty;

    public string ModifierCode { get; init; } = string.Empty;

    public bool IsAssigned => !string.IsNullOrWhiteSpace(KeyCode) && KeyCode != "00";
}
