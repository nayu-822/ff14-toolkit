using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class ShellNavigationItemViewModel : ViewModelBase
{
    private readonly ILocalizationService localizationService;
    private readonly Action<ShellNavigationItemViewModel> onInvoked;
    private bool isExpanded;
    private bool isSelected;

    public ShellNavigationItemViewModel(
        string? sectionKey,
        string titleKey,
        string descriptionKey,
        ILocalizationService localizationService,
        Action<ShellNavigationItemViewModel> onInvoked,
        bool isSelectable = true,
        IEnumerable<ShellNavigationItemViewModel>? children = null)
    {
        SectionKey = sectionKey;
        TitleKey = titleKey;
        DescriptionKey = descriptionKey;
        this.localizationService = localizationService;
        this.onInvoked = onInvoked;
        IsSelectable = isSelectable;
        Children = new ObservableCollection<ShellNavigationItemViewModel>(children ?? []);
        InvokeCommand = new RelayCommand(Invoke);

        foreach (ShellNavigationItemViewModel child in Children)
        {
            child.Parent = this;
        }

        this.localizationService.PropertyChanged += OnLocalizationPropertyChanged;
    }

    public string? SectionKey { get; }

    public string TitleKey { get; }

    public string DescriptionKey { get; }

    public bool IsSelectable { get; }

    public ShellNavigationItemViewModel? Parent { get; private set; }

    public ObservableCollection<ShellNavigationItemViewModel> Children { get; }

    public RelayCommand InvokeCommand { get; }

    public bool HasChildren => Children.Count > 0;

    public bool IsExpandableGroup => HasChildren && !IsSelectable;

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string DisplayName => localizationService[TitleKey];

    public string Description => localizationService[DescriptionKey];

    public void ExpandAncestors()
    {
        ShellNavigationItemViewModel? parent = Parent;
        while (parent is not null)
        {
            parent.IsExpanded = true;
            parent = parent.Parent;
        }
    }

    private void Invoke()
    {
        if (HasChildren && !IsSelectable)
        {
            IsExpanded = !IsExpanded;
            return;
        }

        onInvoked(this);
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ILocalizationService.CurrentCulture)
            or nameof(ILocalizationService.CurrentCultureName)
            or "Item[]")
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Description));
        }
    }
}
