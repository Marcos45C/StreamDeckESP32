using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StreamDeck.Domain.Entities;

namespace StreamDeck.App.Views;

public partial class KeyboardShortcutDialog : Window
{
    private static readonly KeyboardKey[] AvailableKeys = Enum.GetValues<KeyboardKey>();

    public KeyboardShortcutDialog(KeyboardShortcutAction? initialAction = null)
    {
        InitializeComponent();

        KeyComboBox.ItemsSource = AvailableKeys.Select(GetKeyDisplayName).ToArray();
        KeyComboBox.SelectedIndex = initialAction is null
            ? Array.IndexOf(AvailableKeys, KeyboardKey.C)
            : Array.IndexOf(AvailableKeys, initialAction.Key);

        CtrlCheckBox.IsChecked = initialAction?.Modifiers.Contains(KeyboardModifier.Ctrl) == true;
        AltCheckBox.IsChecked = initialAction?.Modifiers.Contains(KeyboardModifier.Alt) == true;
        ShiftCheckBox.IsChecked = initialAction?.Modifiers.Contains(KeyboardModifier.Shift) == true;
        SuperCheckBox.IsChecked = initialAction?.Modifiers.Contains(KeyboardModifier.Super) == true;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => TrySave();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TrySave();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void TrySave()
    {
        if (KeyComboBox.SelectedIndex < 0 || KeyComboBox.SelectedIndex >= AvailableKeys.Length)
        {
            ErrorTextBlock.Text = "Seleccioná una tecla principal.";
            ErrorTextBlock.IsVisible = true;
            return;
        }

        var modifiers = new List<KeyboardModifier>();

        if (CtrlCheckBox.IsChecked == true) modifiers.Add(KeyboardModifier.Ctrl);
        if (AltCheckBox.IsChecked == true) modifiers.Add(KeyboardModifier.Alt);
        if (ShiftCheckBox.IsChecked == true) modifiers.Add(KeyboardModifier.Shift);
        if (SuperCheckBox.IsChecked == true) modifiers.Add(KeyboardModifier.Super);

        try
        {
            var action = new KeyboardShortcutAction(AvailableKeys[KeyComboBox.SelectedIndex], modifiers);
            Close(action);
        }
        catch (ArgumentException ex)
        {
            ErrorTextBlock.Text = ex.Message;
            ErrorTextBlock.IsVisible = true;
        }
    }

    private static string GetKeyDisplayName(KeyboardKey key)
    {
        var name = key.ToString();

        if (name.StartsWith("Digit", StringComparison.Ordinal))
        {
            return name[5..];
        }

        return key switch
        {
            KeyboardKey.Space => "Espacio",
            KeyboardKey.Backspace => "Retroceso",
            KeyboardKey.Delete => "Supr",
            KeyboardKey.Home => "Inicio",
            KeyboardKey.End => "Fin",
            KeyboardKey.PageUp => "RePág",
            KeyboardKey.PageDown => "AvPág",
            KeyboardKey.ArrowUp => "Flecha arriba",
            KeyboardKey.ArrowDown => "Flecha abajo",
            KeyboardKey.ArrowLeft => "Flecha izquierda",
            KeyboardKey.ArrowRight => "Flecha derecha",
            _ => name,
        };
    }
}
