using FF14Toolkit.App.ViewModels;
using System.Windows;

namespace FF14Toolkit.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
