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
    [NotifyCanExecuteChangedFor(nameof(CreatePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenamePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageLeftCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageRightCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasProfiles { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreatePageCommand))]
    public partial Profile? SelectedProfile { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenamePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageLeftCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageRightCommand))]
    public partial Page? SelectedPage { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public ObservableCollection<Profile> Profiles { get; } = new();

    public ObservableCollection<Page> Pages { get; } = new();

    public Func<string?, Task<string?>>? RequestProfileNameAsync { get; set; }

    public Func<string?, Task<string?>>? RequestPageNameAsync { get; set; }

    public Func<string, Task<bool>>? RequestConfirmationAsync { get; set; }

    public bool HasSelectedProfile => SelectedProfile is not null;

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



    // ---------- Perfiles (sin cambios respecto a la Etapa 6.1 corregida) ----------

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

    // ---------- Páginas (nuevo en esta etapa) ----------

    [RelayCommand(CanExecute = nameof(CanCreatePage))]
    private async Task CreatePageAsync()
    {
        if (SelectedProfile is null || RequestPageNameAsync is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var name = await RequestPageNameAsync(null);

        if (name is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "El nombre de la página es obligatorio.";
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            var newPage = await _profileService.AddPageAsync(profileId, name, CancellationToken.None);
            await RefreshProfilesAsync(profileId);
            SelectedPage = Pages.FirstOrDefault(p => p.Id == newPage.Id);
        }, "No se pudo crear la página");
    }

    private bool CanCreatePage() => !IsBusy && SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedPage))]
    private async Task RenamePageAsync()
    {
        if (SelectedProfile is null || SelectedPage is null || RequestPageNameAsync is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageId = SelectedPage.Id;
        var newName = await RequestPageNameAsync(SelectedPage.Name);

        if (newName is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            ErrorMessage = "El nombre de la página es obligatorio.";
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            await _profileService.RenamePageAsync(profileId, pageId, newName, CancellationToken.None);
            await RefreshProfilesAsync(profileId);
            SelectedPage = Pages.FirstOrDefault(p => p.Id == pageId);
        }, "No se pudo renombrar la página");
    }

    private bool CanOperateOnSelectedPage() => !IsBusy && SelectedPage is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedPage))]
    private async Task RemovePageAsync()
    {
        if (SelectedProfile is null || SelectedPage is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageToRemove = SelectedPage;
        var indexBeforeRemoval = Pages.IndexOf(pageToRemove);

        if (RequestConfirmationAsync is null)
        {
            ErrorMessage = "No se pudo abrir el diálogo de confirmación. La página no fue eliminada.";
            return;
        }

        var confirmed = await RequestConfirmationAsync(
            $"¿Eliminar la página \"{pageToRemove.Name}\"? Esta acción no se puede deshacer.");

        if (!confirmed)
        {
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            await _profileService.RemovePageAsync(profileId, pageToRemove.Id, CancellationToken.None);
            await RefreshProfilesAsync(profileId);

            var neighborIndex = Math.Min(indexBeforeRemoval, Pages.Count - 1);
            SelectedPage = neighborIndex >= 0 ? Pages[neighborIndex] : null;
        }, "No se pudo eliminar la página");
    }

    private bool CanRemoveSelectedPage() => !IsBusy && SelectedPage is not null && Pages.Count > 1;

    [RelayCommand(CanExecute = nameof(CanMoveSelectedPageLeft))]
    private async Task MovePageLeftAsync()
    {
        if (SelectedProfile is null || SelectedPage is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageId = SelectedPage.Id;
        var currentIndex = Pages.IndexOf(SelectedPage);

        if (currentIndex <= 0)
        {
            return;
        }

        var newIndex = currentIndex - 1;

        await RunExclusiveAsync(async () =>
        {
            await _profileService.ReorderPageAsync(profileId, pageId, newIndex, CancellationToken.None);
            await RefreshProfilesAsync(profileId);
            SelectedPage = Pages.FirstOrDefault(p => p.Id == pageId);
        }, "No se pudo mover la página");
    }

    private bool CanMoveSelectedPageLeft() =>
        !IsBusy && SelectedPage is not null && Pages.IndexOf(SelectedPage) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveSelectedPageRight))]
    private async Task MovePageRightAsync()
    {
        if (SelectedProfile is null || SelectedPage is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageId = SelectedPage.Id;
        var currentIndex = Pages.IndexOf(SelectedPage);

        if (currentIndex < 0 || currentIndex >= Pages.Count - 1)
        {
            return;
        }

        var newIndex = currentIndex + 1;

        await RunExclusiveAsync(async () =>
        {
            await _profileService.ReorderPageAsync(profileId, pageId, newIndex, CancellationToken.None);
            await RefreshProfilesAsync(profileId);
            SelectedPage = Pages.FirstOrDefault(p => p.Id == pageId);
        }, "No se pudo mover la página");
    }

    private bool CanMoveSelectedPageRight() =>
        !IsBusy && SelectedPage is not null && Pages.IndexOf(SelectedPage) < Pages.Count - 1;

    // ---------- Helpers compartidos ----------

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

    /// <summary>
    /// Recarga todos los perfiles y vuelve a seleccionar el indicado por id, garantizando
    /// que SelectedProfile sea la misma instancia que la lista Profiles (necesario para que
    /// la selección visual del ListBox de perfiles se mantenga correcta). Usado exclusivamente
    /// por las operaciones de páginas.
    /// </summary>
    private async Task RefreshProfilesAsync(Guid profileIdToReselect)
    {
        var all = await _profileService.GetAllProfilesAsync(CancellationToken.None);
        ReplaceProfiles(all);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profileIdToReselect);
    }

    private void SyncPagesFromSelectedProfile()
    {
        Pages.Clear();

        if (SelectedProfile is not null)
        {
            foreach (var page in SelectedProfile.Pages)
            {
                Pages.Add(page);
            }
        }

        SelectedPage = Pages.FirstOrDefault();
    }
    [ObservableProperty]
    public partial ButtonSlot? SelectedButtonSlot { get; set; }

    partial void OnSelectedProfileChanged(Profile? value)
    {
        OnPropertyChanged(nameof(CenterEmptyStateTitle));
        OnPropertyChanged(nameof(CenterEmptyStateMessage));
        OnPropertyChanged(nameof(HasSelectedProfile));
        SyncPagesFromSelectedProfile();
    }

    partial void OnHasProfilesChanged(bool value)
    {
        OnPropertyChanged(nameof(CenterEmptyStateTitle));
        OnPropertyChanged(nameof(CenterEmptyStateMessage));
    }
    public IReadOnlyList<ButtonSlot> ButtonSlots => SelectedPage?.Slots ?? Array.Empty<ButtonSlot>();

    public bool HasSelectedButtonSlot => SelectedButtonSlot is not null;

    public string SelectedButtonSlotTitle => SelectedButtonSlot is not null
        ? $"Botón {SelectedButtonSlot.Position}"
        : string.Empty;

    public string SelectedButtonSlotPositionText => SelectedButtonSlot is not null
        ? $"Posición: {SelectedButtonSlot.Position}"
        : string.Empty;

    public string SelectedButtonSlotRowText => SelectedButtonSlot is not null
        ? $"Fila: {SelectedButtonSlot.Row}"
        : string.Empty;

    public string SelectedButtonSlotColumnText => SelectedButtonSlot is not null
        ? $"Columna: {SelectedButtonSlot.Column}"
        : string.Empty;

    public string SelectedButtonSlotStatusText => "Estado: Sin acción asignada";

    // Ahora:
    partial void OnSelectedPageChanged(Page? value)
    {
        OnPropertyChanged(nameof(ButtonSlots));
        SelectedButtonSlot = value?.Slots.FirstOrDefault();
    }
    partial void OnSelectedButtonSlotChanged(ButtonSlot? value)
    {
        OnPropertyChanged(nameof(HasSelectedButtonSlot));
        OnPropertyChanged(nameof(SelectedButtonSlotTitle));
        OnPropertyChanged(nameof(SelectedButtonSlotPositionText));
        OnPropertyChanged(nameof(SelectedButtonSlotRowText));
        OnPropertyChanged(nameof(SelectedButtonSlotColumnText));
    }
}