using System.Windows;
using iTunesMatchTagger.Wpf.ViewModels;

namespace iTunesMatchTagger.Wpf;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var error = _viewModel.Validate();
        if (error is not null)
        {
            MessageBox.Show(this, error, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
