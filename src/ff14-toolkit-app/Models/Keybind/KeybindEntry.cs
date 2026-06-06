namespace FF14Toolkit.App.Models.Keybind;

public sealed class KeybindEntry
{
    public int Index { get; init; }

    public char CommandSectionType { get; init; }

    public char BindingSectionType { get; init; }

    public string Command { get; init; } = string.Empty;

    public string RawBindingText { get; init; } = string.Empty;

    public KeybindAssignment Primary { get; init; } = new();

    public KeybindAssignment Secondary { get; init; } = new();

    public bool HasAnyAssignment => Primary.IsAssigned || Secondary.IsAssigned;
}
