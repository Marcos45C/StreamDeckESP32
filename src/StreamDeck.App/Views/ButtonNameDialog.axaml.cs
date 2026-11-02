using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StreamDeck.App.Views;

public partial class ButtonNameDialog : Window
{
    public ButtonNameDialog(string? initialName = null)
    {
        InitializeComponent();
        NameTextBox.Text = initialName;
        NameTextBox.SelectAll();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
        => Close(NameTextBox.Text?.Trim());

    private void OnResetClick(object? sender, RoutedEventArgs e)
        => Close(string.Empty);

    private void OnCancelClick(object? sender, RoutedEventArgs e)
        => Close(null);

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Close(NameTextBox.Text?.Trim());
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }
}
