using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamDeck.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string AppName { get; set; } = "StreamDeck ESP32";

    [ObservableProperty]
    public partial string CurrentSectionName { get; set; } = "Editor";

    [ObservableProperty]
    public partial string DeviceStatusText { get; set; } = "Desconectado";

    [ObservableProperty]
    public partial string CenterEmptyStateTitle { get; set; } = "El editor todavía no está disponible";

    [ObservableProperty]
    public partial string CenterEmptyStateMessage { get; set; } =
        "En las próximas etapas vas a poder diseñar acá la distribución de botones de tu Stream Deck.";

    [ObservableProperty]
    public partial string PropertiesEmptyStateTitle { get; set; } = "Propiedades";

    [ObservableProperty]
    public partial string PropertiesEmptyStateMessage { get; set; } =
        "Cuando selecciones un elemento del editor, sus propiedades van a aparecer acá.";
}