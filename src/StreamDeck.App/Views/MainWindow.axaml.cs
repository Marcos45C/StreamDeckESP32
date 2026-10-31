using Avalonia.Controls;
using StreamDeck.App.ViewModels;

namespace StreamDeck.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RequestProfileNameAsync = ShowProfileNameDialogAsync;
            viewModel.RequestConfirmationAsync = ShowConfirmationDialogAsync;
        }
    }

    private async Task<string?> ShowProfileNameDialogAsync(string? currentName)
    {
        var dialog = new ProfileNameDialog(currentName);
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<bool> ShowConfirmationDialogAsync(string message)
    {
        var dialog = new ConfirmationDialog(message);
        var result = await dialog.ShowDialog<bool?>(this);
        return result == true;
    }
}