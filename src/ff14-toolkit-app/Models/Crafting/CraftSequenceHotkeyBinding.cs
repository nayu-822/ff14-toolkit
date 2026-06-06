namespace FF14Toolkit.App.Models.Crafting;

public sealed class CraftSequenceHotkeyBinding
{
    public required int SlotNumber { get; init; }

    public required string HotkeyText { get; init; }

    public required bool IsEnabled { get; init; }

    public Guid? SequenceId { get; init; }

    public required int RepeatCount { get; init; }
}
