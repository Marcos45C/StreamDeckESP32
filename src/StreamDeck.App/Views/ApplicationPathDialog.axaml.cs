using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StreamDeck.Domain.Entities;

namespace StreamDeck.App.Views;

public partial class ApplicationPathDialog : Window
{
    public ApplicationPathDialog(string? initialPath = null)
    {
        InitializeComponent();
        PathTextBox.Text = initialPath;
        PathTextBox.SelectAll();
    }

    // Ahora
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        ErrorTextBlock.IsVisible = false;

        IReadOnlyList<IStorageFile> files;

        try
        {
            files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Seleccionar ejecutable",
                AllowMultiple = false,
            });
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"No se pudo abrir el selector de archivos: {ex.Message}";
            ErrorTextBlock.IsVisible = true;
            return; // Se conserva la ruta ya escrita; el usuario puede seguir escribiéndola manualmente.
        }

        if (files.Count == 0)
        {
            return; // Selector cancelado: se conserva el contenido actual del campo.
        }

        var localPath = files[0].TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(localPath))
        {
            PathTextBox.Text = localPath;
        }
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
            var action = new LaunchApplicationAction(PathTextBox.Text ?? string.Empty);
            Close(action.ApplicationPath);
        }
        catch (ArgumentException ex)
        {
            ErrorTextBlock.Text = ex.Message;
            ErrorTextBlock.IsVisible = true;
        }
    }
}