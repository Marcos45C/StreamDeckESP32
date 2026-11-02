using Avalonia.Media;

namespace StreamDeck.App.ViewModels;

public sealed class ButtonColorOption
{
    public string Name { get; }
    public string HexCode { get; }
    public IBrush PreviewBrush { get; }

    public ButtonColorOption(string name, string hexCode)
    {
        Name = name;
        HexCode = hexCode;
        PreviewBrush = new SolidColorBrush(Color.Parse(hexCode));
    }

    public override string ToString() => Name;
}
