using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StreamDeck.Domain.Entities;

namespace StreamDeck.App.Views;

public partial class OpenUrlDialog : Window
{
    public OpenUrlDialog(string? initialUrl = null)
    {
        InitializeComponent();
        UrlTextBox.Text = initialUrl;
        UrlTextBox.SelectAll();
    }

    private void OnAcceptClick(object? sender, RoutedEventArgs e) => TryAccept();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryAccept();
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void TryAccept()
    {
        try
        {
            var action = new OpenUrlAction(UrlTextBox.Text ?? string.Empty);
            Close(action.Url);
        }
        catch (ArgumentException ex)
        {
            ErrorTextBlock.Text = ex.Message;
            ErrorTextBlock.IsVisible = true;
        }
    }
}
