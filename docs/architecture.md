# Arquitectura — StreamDeck ESP32

## Proyectos y responsabilidades

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `StreamDeck.Domain` | Perfiles, páginas, posiciones de botones, acciones (aplicaciones, URLs y modelo de atajos), colores y nombres personalizados. Contiene las reglas y validaciones del dominio. | Ninguno interno |
| `StreamDeck.Application` | Casos de uso para perfiles y páginas, configuración de acciones y colores, ejecución de acciones e interfaces para los servicios externos. | Domain |
| `StreamDeck.Infrastructure` | Persistencia JSON local, adaptadores para ejecutar aplicaciones y abrir URLs, y sesión de autorización de teclado mediante XDG RemoteDesktop en Linux Wayland. | Application, Domain |
| `StreamDeck.Device` | Enumeración, conexión y comunicación serie con el firmware ESP32. | Application, Domain |
| `StreamDeck.App` | Interfaz Avalonia, ViewModels, diálogos, composición de dependencias y presentación de estados. | Application, Domain, Infrastructure, Device |

No hay dependencias circulares. `Domain` es la única capa sin dependencias internas.

## Puntos principales

- `Domain/Entities/Profile.cs`: perfil y administración de páginas.
- `Domain/Entities/Page.cs`: página con nueve posiciones fijas.
- `Domain/Entities/ButtonSlot.cs`: posición, acción opcional, color visual opcional (`#RRGGBB`) y nombre personalizado opcional.
- `Domain/Entities/ButtonAction.cs`: tipo base de acción.
- `Domain/Entities/LaunchApplicationAction.cs`: ruta de una aplicación.
- `Domain/Entities/OpenUrlAction.cs`: URL absoluta HTTP/HTTPS.
- `Domain/Entities/KeyboardShortcutAction.cs`: combinación persistible de modificadores y tecla principal.
- `Application/Execution/IKeyboardInputSession.cs`: contrato de autorización de una sesión de teclado y envío de combinaciones, sin dependencias de D-Bus.
- `Infrastructure/Execution/WaylandKeyboardInputSession.cs`: ciclo de vida de autorización y envío de KeySyms con XDG RemoteDesktop.
- `Infrastructure/Execution/KeyboardKeySymMapper.cs`: traducción de los identificadores del dominio a KeySyms X11.
- `Infrastructure/Execution/KeyboardShortcutSender.cs`: orden de pulsación y liberación de modificadores y tecla principal.
- `Application/Profiles/ProfileService.cs`: operaciones y persistencia de perfiles, acciones, colores y nombres de botones.
- `Application/Execution/ButtonActionExecutionService.cs`: resuelve y ejecuta la acción asignada a una posición.
- `Infrastructure/Persistence/Json/JsonProfileRepository.cs`: lectura y escritura atómica de archivos JSON.
- `Infrastructure/Execution/`: apertura de aplicaciones y URLs en el sistema operativo.
- `Device/Serial/SerialDeviceConnection.cs`: conexión serie, handshake y recepción de pulsaciones.
- `App/ViewModels/MainViewModel.cs`: coordinación de la interfaz, la selección activa y la cola de pulsaciones físicas.

## Persistencia JSON

Cada perfil se guarda en un archivo independiente. El repositorio escribe actualmente `schemaVersion: 7`.

| Versión | Contenido agregado |
|---:|---|
| 1 | Perfil y páginas. |
| 2 | Las nueve posiciones de botones por página. |
| 3 | Acciones `launchApplication`. |
| 4 | Acciones `openUrl` además de aplicaciones. |
| 5 | Colores visuales opcionales para cada posición. |
| 6 | Nombres personalizados opcionales para cada botón. |
| 7 | Acciones `keyboardShortcut`, con tecla principal y modificadores.

El repositorio mantiene lectura compatible con v1 a v6. Leer un archivo antiguo no lo modifica; al guardar ese perfil se escribe el esquema v7.

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
- Integración D-Bus: `Tmds.DBus.Protocol` y `Tmds.DBus.Generator` 0.95.1 para los proxies del portal XDG en Linux.
- Firmware: PlatformIO, plataforma `espressif32`, framework Arduino.

Todos los proyectos C# apuntan a `net10.0`. `Directory.Build.props` centraliza `Nullable` e `ImplicitUsings`.

## Pruebas

- `StreamDeck.Domain.Tests`: reglas de perfiles, páginas, botones, acciones, colores y nombres personalizados.
- `StreamDeck.Application.Tests`: casos de uso con repositorio en memoria y lanzadores simulados, incluyendo configuración de nombres personalizados.
- `StreamDeck.Infrastructure.Tests`: persistencia JSON, migraciones y validaciones de esquemas.


### Personalización visual de botones

Cada `ButtonSlot` puede conservar un color hexadecimal y un nombre personalizado opcional. Los nombres se guardan en `slotNames` dentro del esquema JSON v6; los archivos v1–v5 siguen leyéndose y sus botones comienzan sin nombre personalizado. Esta personalización es exclusivamente visual y no cambia la posición física ni la acción del botón.

### Autorización de teclado Wayland

`WaylandKeyboardInputSession` administra la conexión al bus D-Bus de sesión y el ciclo `CreateSession` → `SelectDevices` → `Start` de `org.freedesktop.portal.RemoteDesktop`. Solicita únicamente `KEYBOARD` (`types = 1`) y `persist_mode = 1`, que mantiene el permiso mientras la aplicación está ejecutándose. La implementación observa el cierre de la sesión y la pérdida de la conexión D-Bus.

La etapa 10.2 incorpora la autorización y el ciclo de vida de la sesión. La etapa 10.3 implementa el envío de combinaciones mediante `NotifyKeyboardKeysym`; no utiliza `ConnectToEIS`, por lo que mantiene el flujo de entrada D-Bus del portal.
