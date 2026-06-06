namespace FF14Toolkit.App.Models.Crafting;

public sealed class CraftActionSequence
{
    public required Guid SequenceId { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<CraftActionSequenceStep> Steps { get; init; }
}

public sealed class CraftActionSequenceStep
{
    public required CrafterActionId ActionId { get; init; }

    public required int WaitMilliseconds { get; init; }
}
