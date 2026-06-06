using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftActionSequenceSummaryViewModel : ObservableObject
{
    private CraftActionSequence sequence;

    public CraftActionSequenceSummaryViewModel(CraftActionSequence sequence)
    {
        this.sequence = sequence;
    }

    public Guid SequenceId => sequence.SequenceId;

    public string Name => sequence.Name;

    public int StepCount => sequence.Steps.Count;

    public int TotalWaitMilliseconds => sequence.Steps.Sum(step => step.WaitMilliseconds);

    public string StepSummary => $"{StepCount} actions";

    public string WaitSummary => $"{TotalWaitMilliseconds} ms";

    public IReadOnlyList<CraftActionSequenceStep> Steps => sequence.Steps;

    public void Update(CraftActionSequence nextSequence)
    {
        sequence = nextSequence;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(TotalWaitMilliseconds));
        OnPropertyChanged(nameof(StepSummary));
        OnPropertyChanged(nameof(WaitSummary));
        OnPropertyChanged(nameof(Steps));
    }
}
