using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamDeck.Application.Device;
using StreamDeck.Application.Execution;
using StreamDeck.Application.Profiles;
using StreamDeck.Domain.Entities;
using System.Threading;

namespace StreamDeck.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ProfileService _profileService;
    private readonly ButtonActionExecutionService _buttonActionExecutionService;
    private readonly IDeviceConnection _deviceConnection;

    private readonly ConcurrentQueue<ButtonPressEvent> _buttonPressQueue = new();
    private readonly SemaphoreSlim _buttonPressSignal = new(0);
    private readonly CancellationTokenSource _buttonProcessingCancellation = new();

    private Task? _buttonProcessingTask;
    private bool _isShuttingDown;

    private ActiveSelectionSnapshot _activeSelectionSnapshot = new(null, null);

    private readonly record struct ButtonPressEvent(
        Guid? ProfileId,
        Guid? PageId,
        int Position);

    private sealed record ActiveSelectionSnapshot(
        Guid? ProfileId,
        Guid? PageId);

    public MainViewModel(
    ProfileService profileService,
    ButtonActionExecutionService buttonActionExecutionService,
    IDeviceConnection deviceConnection)
    {
        ArgumentNullException.ThrowIfNull(profileService);
        ArgumentNullException.ThrowIfNull(buttonActionExecutionService);
        ArgumentNullException.ThrowIfNull(deviceConnection);

        _profileService = profileService;
        _buttonActionExecutionService = buttonActionExecutionService;
        _deviceConnection = deviceConnection;

        _deviceConnection.StateChanged += HandleDeviceConnectionStateChanged;
        _deviceConnection.ButtonPressed += HandleDeviceButtonPressed;

        _buttonProcessingTask =
            ProcessButtonPressQueueAsync(_buttonProcessingCancellation.Token);

        RefreshPortsCommand.Execute(null);
        LoadProfilesCommand.Execute(null);
    }

    [ObservableProperty]
    public partial string AppName { get; set; } = "StreamDeck ESP32";

    [ObservableProperty]
    public partial string CurrentSectionName { get; set; } = "Perfiles";

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
    [NotifyCanExecuteChangedFor(nameof(AssignApplicationCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignUrlCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearButtonActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSelectedButtonActionCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasProfiles { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreatePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignApplicationCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignUrlCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSelectedButtonActionCommand))]
    public partial Profile? SelectedProfile { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenamePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageLeftCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageRightCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignApplicationCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignUrlCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSelectedButtonActionCommand))]
    public partial Page? SelectedPage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AssignApplicationCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignUrlCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearButtonActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSelectedButtonActionCommand))]
    public partial ButtonSlot? SelectedButtonSlot { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDeviceCommand))]
    public partial bool IsDeviceBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDeviceCommand))]
    public partial DeviceConnectionState DeviceState { get; set; } = DeviceConnectionState.Disconnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDeviceCommand))]
    public partial string? SelectedPort { get; set; }

    [ObservableProperty]
    public partial int? LastButtonPressedPosition { get; set; }

    [ObservableProperty]
    public partial string? LastButtonActivityText { get; set; }

    public ObservableCollection<Profile> Profiles { get; } = new();

    public ObservableCollection<Page> Pages { get; } = new();

    public ObservableCollection<string> AvailablePorts { get; } = new();

    public Func<string?, Task<string?>>? RequestProfileNameAsync { get; set; }

    public Func<string?, Task<string?>>? RequestPageNameAsync { get; set; }

    public Func<string, Task<bool>>? RequestConfirmationAsync { get; set; }

    public Func<string?, Task<string?>>? RequestApplicationPathAsync { get; set; }

    public Func<string?, Task<string?>>? RequestUrlAsync { get; set; }

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

    public string SelectedButtonSlotStatusText => SelectedButtonSlot?.Action switch
    {
        LaunchApplicationAction => "Tipo: Aplicación",
        OpenUrlAction => "Tipo: Abrir URL",
        _ => "Estado: Sin acción asignada",
    };

    public bool HasButtonAction => SelectedButtonSlot?.Action is not null;

    public string ConfiguredActionDetailsText => SelectedButtonSlot?.Action switch
    {
        LaunchApplicationAction launch => $"Ejecutable: {launch.ApplicationPath}",
        OpenUrlAction openUrl => $"URL: {openUrl.Url}",
        _ => string.Empty,
    };

    public string DeviceStatusText => DeviceState switch
    {
        DeviceConnectionState.Disconnected => "Desconectado",
        DeviceConnectionState.Connecting => "Conectando…",
        DeviceConnectionState.Connected => "Conectado",
        DeviceConnectionState.Error => "Error de conexión",
        _ => "Desconectado",
    };

    public bool IsDeviceConnected => DeviceState == DeviceConnectionState.Connected;

    public string LastButtonPressedText => LastButtonPressedPosition is int position
        ? $"Último botón recibido: {position}"
        : "Esperando pulsaciones...";

    // ---------- Perfiles ----------

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

    // ---------- Páginas ----------

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

    // ---------- Acciones de botones (configuración / prueba manual) ----------

    [RelayCommand(CanExecute = nameof(CanAssignOrChangeApplication))]
    private async Task AssignApplicationAsync()
    {
        if (SelectedProfile is null || SelectedPage is null || SelectedButtonSlot is null || RequestApplicationPathAsync is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageId = SelectedPage.Id;
        var position = SelectedButtonSlot.Position;
        var currentPath = (SelectedButtonSlot.Action as LaunchApplicationAction)?.ApplicationPath;

        var newPath = await RequestApplicationPathAsync(currentPath);

        if (newPath is null)
        {
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            await _profileService.AssignApplicationActionAsync(profileId, pageId, position, newPath, CancellationToken.None);
            await RefreshAfterButtonActionChangeAsync(profileId, pageId, position);
        }, "No se pudo asignar la aplicación");
    }

    private bool CanAssignOrChangeApplication() =>
        !IsBusy && SelectedProfile is not null && SelectedPage is not null && SelectedButtonSlot is not null;

    [RelayCommand(CanExecute = nameof(CanAssignOrChangeApplication))]
    private async Task AssignUrlAsync()
    {
        if (SelectedProfile is null || SelectedPage is null || SelectedButtonSlot is null || RequestUrlAsync is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageId = SelectedPage.Id;
        var position = SelectedButtonSlot.Position;
        var currentUrl = (SelectedButtonSlot.Action as OpenUrlAction)?.Url;

        var newUrl = await RequestUrlAsync(currentUrl);

        if (newUrl is null)
        {
            return;
        }

        await RunExclusiveAsync(async () =>
        {
            await _profileService.AssignOpenUrlActionAsync(
                profileId, pageId, position, newUrl, CancellationToken.None);
            await RefreshAfterButtonActionChangeAsync(profileId, pageId, position);
        }, "No se pudo asignar la URL");
    }

    [RelayCommand(CanExecute = nameof(CanClearButtonAction))]
    private async Task ClearButtonActionAsync()
    {
        if (SelectedProfile is null || SelectedPage is null || SelectedButtonSlot is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageId = SelectedPage.Id;
        var position = SelectedButtonSlot.Position;

        await RunExclusiveAsync(async () =>
        {
            await _profileService.ClearButtonActionAsync(profileId, pageId, position, CancellationToken.None);
            await RefreshAfterButtonActionChangeAsync(profileId, pageId, position);
        }, "No se pudo quitar la acción");
    }

    private bool CanClearButtonAction() =>
        !IsBusy && SelectedProfile is not null && SelectedPage is not null && SelectedButtonSlot is not null && HasButtonAction;

    [RelayCommand(CanExecute = nameof(CanExecuteSelectedButtonAction))]
    private async Task ExecuteSelectedButtonActionAsync()
    {
        if (SelectedProfile is null || SelectedPage is null || SelectedButtonSlot is null)
        {
            return;
        }

        if (SelectedButtonSlot.Action is null)
        {
            return;
        }

        var profileId = SelectedProfile.Id;
        var pageId = SelectedPage.Id;
        var position = SelectedButtonSlot.Position;

        await RunExclusiveAsync(async () =>
        {
            await _buttonActionExecutionService.ExecuteAsync(profileId, pageId, position, CancellationToken.None);
        }, "No se pudo ejecutar la acción");
    }

    private bool CanExecuteSelectedButtonAction() =>
        !IsBusy && SelectedProfile is not null && SelectedPage is not null
        && SelectedButtonSlot?.Action is not null;

    // ---------- Conexión con el dispositivo ----------

    [RelayCommand]
    private void RefreshPorts()
    {
        var ports = _deviceConnection.GetAvailablePortNames();
        var previousSelection = SelectedPort;

        AvailablePorts.Clear();
        foreach (var port in ports)
        {
            AvailablePorts.Add(port);
        }

        SelectedPort = previousSelection is not null && AvailablePorts.Contains(previousSelection)
            ? previousSelection
            : AvailablePorts.FirstOrDefault();
    }

    private bool CanConnectDevice() =>
        !IsDeviceBusy && !string.IsNullOrWhiteSpace(SelectedPort) &&
        (DeviceState == DeviceConnectionState.Disconnected || DeviceState == DeviceConnectionState.Error);

    [RelayCommand(CanExecute = nameof(CanConnectDevice))]
    private async Task ConnectDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            return;
        }

        IsDeviceBusy = true;
        ErrorMessage = null;

        try
        {
            await _deviceConnection.ConnectAsync(SelectedPort, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo conectar con el dispositivo: {ex.Message}";
        }
        finally
        {
            IsDeviceBusy = false;
        }
    }

    private bool CanDisconnectDevice() =>
        !IsDeviceBusy && (DeviceState == DeviceConnectionState.Connected || DeviceState == DeviceConnectionState.Connecting);

    [RelayCommand(CanExecute = nameof(CanDisconnectDevice))]
    private async Task DisconnectDeviceAsync()
    {
        IsDeviceBusy = true;

        try
        {
            await _deviceConnection.DisconnectAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo desconectar correctamente: {ex.Message}";
        }
        finally
        {
            IsDeviceBusy = false;
        }
    }

    public async Task DisconnectDeviceForShutdownAsync()
    {
        Volatile.Write(ref _isShuttingDown, true);

        try
        {
            await _deviceConnection.DisconnectAsync();
        }
        catch
        {
            // Mejor esfuerzo durante el cierre.
        }

        _buttonProcessingCancellation.Cancel();

        if (_buttonProcessingTask is not null)
        {
            try
            {
                await _buttonProcessingTask;
            }
            catch
            {
                // El consumidor maneja sus propias excepciones.
            }
        }

        _buttonPressSignal.Dispose();
        _buttonProcessingCancellation.Dispose();
    }

    private void HandleDeviceConnectionStateChanged(object? sender, DeviceConnectionStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            DeviceState = e.State;

            if (e.ErrorMessage is not null)
            {
                ErrorMessage = e.ErrorMessage;
            }
        });
    }

        private void HandleDeviceButtonPressed(
        object? sender,
        DeviceButtonPressedEventArgs e)
    {
        if (Volatile.Read(ref _isShuttingDown))
        {
            return;
        }

        var snapshot = Volatile.Read(ref _activeSelectionSnapshot);

        var pressEvent = new ButtonPressEvent(
            snapshot.ProfileId,
            snapshot.PageId,
            e.Position);

        _buttonPressQueue.Enqueue(pressEvent);
        _buttonPressSignal.Release();

        Dispatcher.UIThread.Post(() =>
            LastButtonPressedPosition = e.Position);
    }
        private void UpdateActiveSelectionSnapshot()
    {
        var snapshot = new ActiveSelectionSnapshot(
            SelectedProfile?.Id,
            SelectedPage?.Id);

        Volatile.Write(ref _activeSelectionSnapshot, snapshot);
    }

    private async Task ProcessButtonPressQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _buttonPressSignal.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (_buttonPressQueue.TryDequeue(out var pressEvent))
                {
                    await ProcessSingleButtonPressAsync(pressEvent, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cierre de la aplicación: se deja de procesar la cola.
        }
    }

    private async Task ProcessSingleButtonPressAsync(
    ButtonPressEvent pressEvent,
    CancellationToken cancellationToken)
    {
        if (pressEvent.ProfileId is not Guid profileId ||
            pressEvent.PageId is not Guid pageId)
        {
            Dispatcher.UIThread.Post(() =>
                LastButtonActivityText =
                    $"Botón {pressEvent.Position}: no hay perfil o página activos.");

            return;
        }

        try
        {
            await _buttonActionExecutionService.ExecuteAsync(
                profileId,
                pageId,
                pressEvent.Position,
                cancellationToken).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
                LastButtonActivityText =
                    $"Botón {pressEvent.Position}: acción ejecutada.");
        }
        catch (ButtonActionNotConfiguredException)
        {
            Dispatcher.UIThread.Post(() =>
                LastButtonActivityText =
                    $"Botón {pressEvent.Position}: sin acción asignada.");
        }
        catch (KeyNotFoundException)
        {
            Dispatcher.UIThread.Post(() =>
                LastButtonActivityText =
                    $"Botón {pressEvent.Position}: no hay perfil o página activos.");
        }
        catch (OperationCanceledException)
        {
            // Cierre de la aplicación en curso.
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LastButtonActivityText =
                    $"Botón {pressEvent.Position}: error al ejecutar la acción.";

                ErrorMessage =
                    $"No se pudo ejecutar la acción del botón {pressEvent.Position}: {ex.Message}";
            });
        }
    }

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

    private async Task RefreshProfilesAsync(Guid profileIdToReselect)
    {
        var all = await _profileService.GetAllProfilesAsync(CancellationToken.None);
        ReplaceProfiles(all);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profileIdToReselect);
    }

    private async Task RefreshAfterButtonActionChangeAsync(Guid profileId, Guid pageId, int position)
    {
        var all = await _profileService.GetAllProfilesAsync(CancellationToken.None);
        ReplaceProfiles(all);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profileId);
        SelectedPage = Pages.FirstOrDefault(p => p.Id == pageId);
        SelectedButtonSlot = SelectedPage?.Slots.FirstOrDefault(s => s.Position == position);
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

    partial void OnSelectedProfileChanged(Profile? value)
    {
        OnPropertyChanged(nameof(CenterEmptyStateTitle));
        OnPropertyChanged(nameof(CenterEmptyStateMessage));
        OnPropertyChanged(nameof(HasSelectedProfile));

        SyncPagesFromSelectedProfile();

        UpdateActiveSelectionSnapshot();
    }

    partial void OnHasProfilesChanged(bool value)
    {
        OnPropertyChanged(nameof(CenterEmptyStateTitle));
        OnPropertyChanged(nameof(CenterEmptyStateMessage));
    }

        partial void OnSelectedPageChanged(Page? value)
    {
        OnPropertyChanged(nameof(ButtonSlots));

        SelectedButtonSlot = value?.Slots.FirstOrDefault();

        UpdateActiveSelectionSnapshot();
    }
    partial void OnSelectedButtonSlotChanged(ButtonSlot? value)
    {
        OnPropertyChanged(nameof(HasSelectedButtonSlot));
        OnPropertyChanged(nameof(SelectedButtonSlotTitle));
        OnPropertyChanged(nameof(SelectedButtonSlotPositionText));
        OnPropertyChanged(nameof(SelectedButtonSlotRowText));
        OnPropertyChanged(nameof(SelectedButtonSlotColumnText));
        OnPropertyChanged(nameof(SelectedButtonSlotStatusText));
        OnPropertyChanged(nameof(HasButtonAction));
        OnPropertyChanged(nameof(ConfiguredActionDetailsText));
    }

    partial void OnDeviceStateChanged(DeviceConnectionState value)
    {
        OnPropertyChanged(nameof(DeviceStatusText));
        OnPropertyChanged(nameof(IsDeviceConnected));
    }

    partial void OnLastButtonPressedPositionChanged(int? value)
    {
        OnPropertyChanged(nameof(LastButtonPressedText));
    }
}