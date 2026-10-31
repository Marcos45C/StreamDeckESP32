using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StreamDeck.App.Views;

public partial class ProfileNameDialog : Window
{
    public ProfileNameDialog(string? initialName = null)
    {
        InitializeComponent();
        NameTextBox.Text = initialName;
        NameTextBox.SelectAll();
    }

    private void OnAcceptClick(object? sender, RoutedEventArgs e) => Close(NameTextBox.Text);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Close(NameTextBox.Text);
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }
}