using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StreamDeck.App.ViewModels;
using StreamDeck.App.Views;
using StreamDeck.Application.Profiles;
using StreamDeck.Infrastructure.Persistence.Json;

namespace StreamDeck.App;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var profileRepository = new JsonProfileRepository();
            var profileService = new ProfileService(profileRepository);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(profileService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}