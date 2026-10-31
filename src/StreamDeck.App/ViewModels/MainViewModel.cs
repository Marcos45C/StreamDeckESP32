using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamDeck.Application.Profiles;
using StreamDeck.Domain.Entities;

namespace StreamDeck.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ProfileService _profileService;

    public MainViewModel(ProfileService profileService)
    {
        ArgumentNullException.ThrowIfNull(profileService);
        _profileService = profileService;

        LoadProfilesCommand.Execute(null);
    }

    [ObservableProperty]
    public partial string AppName { get; set; } = "StreamDeck ESP32";

    [ObservableProperty]
    public partial string CurrentSectionName { get; set; } = "Perfiles";

    [ObservableProperty]
    public partial string DeviceStatusText { get; set; } = "Desconectado";

    [ObservableProperty]
    public partial string PropertiesEmptyStateTitle { get; set; } = "Propiedades";

    [ObservableProperty]
    public partial string PropertiesEmptyStateMessage { get; set; } =
        "Cuando selecciones un elemento del editor, sus propiedades van a aparecer acá.";

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasProfiles { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    public partial Profile? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public ObservableCollection<Profile> Profiles { get; } = new();

    /// <summary>
    /// La View (MainWindow.axaml.cs) asigna esta función para mostrar un diálogo de
    /// texto real. Recibe el nombre actual (null si es creación) y devuelve el nombre
    /// ingresado, o null si se canceló.
    /// </summary>
    public Func<string?, Task<string?>>? RequestProfileNameAsync { get; set; }

    /// <summary>
    /// La View asigna esta función para mostrar un diálogo de confirmación real.
    /// Devuelve true si el usuario confirmó.
    /// </summary>
    public Func<string, Task<bool>>? RequestConfirmationAsync { get; set; }

    public string CenterEmptyStateTitle => SelectedProfile is not null
        ? $"Perfil: {SelectedProfile.Name}"
        : HasProfiles
            ? "Seleccioná un perfil"
            : "Todavía no tenés perfiles";

    public string CenterEmptyStateMessage => SelectedProfile is not null
        ? "La administración de páginas y del editor llegará en la próxima etapa."
        : HasProfiles
            ? "Elegí un perfil de la lista en la barra lateral."
            : "Creá tu primer perfil con el botón + de la barra lateral para empezar.";

    [RelayCommand]
    private async Task LoadProfilesAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var profiles = await _profileService.GetAllProfilesAsync(cancellationToken);
            ReplaceProfiles(profiles);
            SelectedProfile = Profiles.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            // Carga cancelada; no se muestra como error.
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudieron cargar los perfiles: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private async Task CreateProfileAsync()
    {
        if (RequestProfileNameAsync is null)
        {
            return;
        }

        var name = await RequestProfileNameAsync(null);

        if (name is null)
        {
            // El usuario canceló el diálogo.
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "El nombre del perfil es obligatorio.";
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            var created = await _profileService.CreateProfileAsync(name, CancellationToken.None);
            var all = await _profileService.GetAllProfilesAsync(CancellationToken.None);
            ReplaceProfiles(all);
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == created.Id);
        }, "No se pudo crear el perfil");
    }

    private bool CanCreateProfile() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedProfile))]
    private async Task RenameProfileAsync()
    {
        if (SelectedProfile is null || RequestProfileNameAsync is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var newName = await RequestProfileNameAsync(SelectedProfile.Name);

        if (newName is null)
        {
            // El usuario canceló el diálogo.
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            ErrorMessage = "El nombre del perfil es obligatorio.";
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            await _profileService.RenameProfileAsync(profileId, newName, CancellationToken.None);
            var all = await _profileService.GetAllProfilesAsync(CancellationToken.None);
            ReplaceProfiles(all);
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profileId);
        }, "No se pudo renombrar el perfil");
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedProfile))]
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var profileToDelete = SelectedProfile;

        if (RequestConfirmationAsync is null)
        {
            ErrorMessage = "No se pudo abrir el diálogo de confirmación. El perfil no fue eliminado.";
            return;
        }

        var confirmed = await RequestConfirmationAsync(
            $"¿Eliminar el perfil \"{profileToDelete.Name}\"? Esta acción no se puede deshacer.");

        if (!confirmed)
        {
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            await _profileService.DeleteProfileAsync(profileToDelete.Id, CancellationToken.None);
            var all = await _profileService.GetAllProfilesAsync(CancellationToken.None);
            ReplaceProfiles(all);
            SelectedProfile = Profiles.FirstOrDefault();
        }, "No se pudo eliminar el perfil");
    }

    private bool CanOperateOnSelectedProfile() => !IsBusy && SelectedProfile is not null;

    private async Task RunExclusiveAsync(Func<Task> operation, string errorPrefix)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // Cancelado; no se muestra como error.
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{errorPrefix}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceProfiles(IReadOnlyList<Profile> profiles)
    {
        Profiles.Clear();

        foreach (var profile in profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Profiles.Add(profile);
        }

        HasProfiles = Profiles.Count > 0;
    }

    partial void OnSelectedProfileChanged(Profile? value)
    {
        OnPropertyChanged(nameof(CenterEmptyStateTitle));
        OnPropertyChanged(nameof(CenterEmptyStateMessage));
    }

    partial void OnHasProfilesChanged(bool value)
    {
        OnPropertyChanged(nameof(CenterEmptyStateTitle));
        OnPropertyChanged(nameof(CenterEmptyStateMessage));
    }
}