using FF14Toolkit.App.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FF14Toolkit.App.Views;

public partial class CraftActionSequenceContentView : UserControl
{
    private bool initializationRequested;

    public CraftActionSequenceContentView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeViewModelAsync();
    }

    private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        await InitializeViewModelAsync();
    }

    private async Task InitializeViewModelAsync()
    {
        if (initializationRequested || DataContext is not CraftActionSequenceContentViewModel viewModel)
        {
            return;
        }

        initializationRequested = true;
        await viewModel.InitializeAsync();
    }
}
