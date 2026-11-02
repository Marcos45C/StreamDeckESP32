# Arquitectura — StreamDeck ESP32

## Proyectos y responsabilidades

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `StreamDeck.Domain` | Perfiles, páginas, posiciones de botones, acciones y colores configurables. Contiene las reglas y validaciones del dominio. | Ninguno interno |
| `StreamDeck.Application` | Casos de uso para perfiles y páginas, configuración de acciones y colores, ejecución de acciones e interfaces para los servicios externos. | Domain |
| `StreamDeck.Infrastructure` | Persistencia JSON local y adaptadores del sistema operativo para ejecutar aplicaciones y abrir URLs. | Application, Domain |
| `StreamDeck.Device` | Enumeración, conexión y comunicación serie con el firmware ESP32. | Application, Domain |
| `StreamDeck.App` | Interfaz Avalonia, ViewModels, diálogos, composición de dependencias y presentación de estados. | Application, Domain, Infrastructure, Device |

No hay dependencias circulares. `Domain` es la única capa sin dependencias internas.

## Puntos principales

- `Domain/Entities/Profile.cs`: perfil y administración de páginas.
- `Domain/Entities/Page.cs`: página con nueve posiciones fijas.
- `Domain/Entities/ButtonSlot.cs`: posición, acción opcional y color visual opcional (`#RRGGBB`).
- `Domain/Entities/ButtonAction.cs`: tipo base de acción.
- `Domain/Entities/LaunchApplicationAction.cs`: ruta de una aplicación.
- `Domain/Entities/OpenUrlAction.cs`: URL absoluta HTTP/HTTPS.
- `Application/Profiles/ProfileService.cs`: operaciones y persistencia de perfiles, acciones y colores.
- `Application/Execution/ButtonActionExecutionService.cs`: resuelve y ejecuta la acción asignada a una posición.
- `Infrastructure/Persistence/Json/JsonProfileRepository.cs`: lectura y escritura atómica de archivos JSON.
- `Infrastructure/Execution/`: apertura de aplicaciones y URLs en el sistema operativo.
- `Device/Serial/SerialDeviceConnection.cs`: conexión serie, handshake y recepción de pulsaciones.
- `App/ViewModels/MainViewModel.cs`: coordinación de la interfaz, la selección activa y la cola de pulsaciones físicas.

## Persistencia JSON

Cada perfil se guarda en un archivo independiente. El repositorio escribe actualmente `schemaVersion: 5`.

| Versión | Contenido agregado |
|---:|---|
| 1 | Perfil y páginas. |
| 2 | Las nueve posiciones de botones por página. |
| 3 | Acciones `launchApplication`. |
| 4 | Acciones `openUrl` además de aplicaciones. |
| 5 | Colores visuales opcionales para cada posición. |

El repositorio mantiene lectura compatible con v1 a v4. Leer un archivo antiguo no lo modifica; al guardar ese perfil se escribe el esquema v5.

## MVVM y composición

- `ViewModels/ViewModelBase.cs` es la clase base MVVM.
- `ViewModels/MainViewModel.cs` contiene propiedades observables y comandos mediante CommunityToolkit.Mvvm.
- `Views/MainWindow.axaml` define la interfaz principal.
- `Views/MainWindow.axaml.cs` conecta los delegados de los diálogos y gestiona el cierre seguro cuando el dispositivo está conectado.
- `App.axaml.cs` crea las implementaciones concretas y las inyecta en el ViewModel principal.

## Dependencias principales

- `StreamDeck.App`: Avalonia 12.1.4, CommunityToolkit.Mvvm 8.4.2 y paquetes de interfaz asociados.
- `StreamDeck.Device`: `System.IO.Ports` 10.0.12.
- Persistencia JSON: `System.Text.Json` incluido en .NET.
- Firmware: PlatformIO, plataforma `espressif32`, framework Arduino.

Todos los proyectos C# apuntan a `net10.0`. `Directory.Build.props` centraliza `Nullable` e `ImplicitUsings`.

## Pruebas

- `StreamDeck.Domain.Tests`: reglas de perfiles, páginas, botones, acciones y colores.
- `StreamDeck.Application.Tests`: casos de uso con repositorio en memoria y lanzadores simulados.
- `StreamDeck.Infrastructure.Tests`: persistencia JSON, migraciones y validaciones de esquemas.
