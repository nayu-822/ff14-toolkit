using System.IO;
using System.Windows;
using Microsoft.Win32;
using FF14Toolkit.TemplateMatchWorkbench.ViewModels;

namespace FF14Toolkit.TemplateMatchWorkbench;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainWindowViewModel();
        DataContext = viewModel;
    }

    private void OpenImageButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "検証画像を選択",
            InitialDirectory = Directory.Exists(viewModel.InputImagesRootPath)
                ? viewModel.InputImagesRootPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|すべてのファイル|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.LoadImage(dialog.FileName);
        }
    }

    private async void RunMatchButton_Click(object sender, RoutedEventArgs e)
    {
        await viewModel.ExecuteMatchAsync();
    }

    private void RefreshProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.RefreshProfiles();
    }
}
