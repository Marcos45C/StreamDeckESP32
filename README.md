# StreamDeck ESP32

Aplicación de escritorio para configurar y administrar un **Stream Deck físico de 9 botones basado en ESP32**.

La computadora guarda los perfiles, páginas y acciones. El ESP32 funciona como un dispositivo de entrada: detecta las pulsaciones y envía la posición del botón por USB/serie. No es un Stream Deck virtual y el firmware no almacena las configuraciones.

> Estado del proyecto: la conexión serie, las nueve pulsaciones, las acciones de aplicaciones/URLs, los colores, los nombres personalizados, la bandeja del sistema y los atajos de teclado fueron probados en Fedora KDE Wayland con una placa ESP32 DevKit V1 / ESP-WROOM-32 con CP2102. La bandeja del sistema también fue probada en Windows. La ejecución de atajos está disponible actualmente solo en Linux Wayland.

## Funcionalidades implementadas

- Perfiles con varias páginas.
- Cuadrícula de 3 × 3: nueve botones por página.
- Asignación, modificación y eliminación de acciones por botón.
- Acción **Ejecutar aplicación**, usando la ruta del ejecutable correspondiente al sistema operativo.
- Acción **Abrir URL**, limitada a direcciones HTTP y HTTPS.
- Acción **Atajo de teclado** para Linux KDE Wayland: permite seleccionar uno o varios modificadores (Ctrl, Alt, Shift/Mayús o Super) junto con una tecla principal. Por ejemplo, `Alt + Shift + F4` es una combinación válida. La ejecución utiliza la sesión de teclado autorizada mediante XDG RemoteDesktop.
- Ejecución de combinaciones de teclas en Linux KDE Wayland mediante la sesión autorizada de XDG RemoteDesktop.
- Ocho colores configurables por botón, independientes de la acción asignada.
- Nombre personalizado por botón: doble clic sobre un botón para editarlo o restablecer el nombre predeterminado.
- Minimización a la bandeja del sistema: la ventana desaparece de la barra de tareas y permanece ejecutándose en segundo plano.
- Icono propio en la bandeja con opciones para restaurar la ventana o salir de la aplicación.
- Persistencia local de perfiles en archivos JSON.
- Modelo, persistencia y configuración visual de acciones de atajo de teclado (`keyboardShortcut`) en el esquema JSON v7. Los atajos se envían mediante el portal RemoteDesktop después de autorizar el teclado.
- Compatibilidad de lectura con los esquemas JSON anteriores (v1 a v6); los guardados actuales utilizan el esquema v7.
- Detección del puerto serie, conexión, handshake y recepción de las nueve pulsaciones físicas.
- Ejecución de la acción correspondiente al perfil y página seleccionados cuando se pulsa un botón físico.
- Interfaz de escritorio multiplataforma con tema oscuro.

### Bandeja del sistema y ejecución en segundo plano

Al minimizar la ventana, StreamDeck ESP32 oculta su entrada de la barra de tareas y mantiene la aplicación ejecutándose. Para volver a abrirla, hacé clic en el icono de StreamDeck ESP32 de la bandeja del sistema o utilizá la opción **Mostrar StreamDeck ESP32** del menú del icono. La opción **Salir** cierra la aplicación; si el ESP32 está conectado, se realiza primero la desconexión ordenada.


### Atajos de teclado en Fedora KDE Wayland

Los atajos de teclado están implementados para sesiones Linux Wayland. Para utilizarlos:

1. Iniciá StreamDeckESP32 en una sesión Wayland (`echo $XDG_SESSION_TYPE` debe devolver `wayland`).
2. Abrí el menú del icono de StreamDeckESP32 en la bandeja y seleccioná **Autorizar atajos de teclado (Linux Wayland)**.
3. Aceptá en KDE la solicitud de acceso al teclado. La aplicación solicita exclusivamente el dispositivo teclado; no pide control del mouse ni de la pantalla táctil.
4. Seleccioná un botón de la cuadrícula y elegí **Atajo de teclado** o **Cambiar atajo de teclado** en el panel de propiedades.
5. Elegí uno o varios modificadores y una tecla principal. `Shift` es la tecla **Mayús**. Por ejemplo, podés configurar `Alt + Shift + F4`, `Ctrl + C` o `Super + D`.
6. Guardá el atajo y probalo con el botón físico mientras la aplicación de destino esté en primer plano.

La autorización se mantiene durante la ejecución de StreamDeckESP32 si el portal concede el modo de persistencia solicitado. Minimizar la ventana a la bandeja no cierra la sesión. Al salir, la aplicación cierra la sesión RemoteDesktop. Si KDE revoca o cierra la sesión, hay que volver a solicitar la autorización.

El botón **Probar acción** del panel no se muestra para atajos, porque al utilizarlo la propia ventana de StreamDeckESP32 tendría el foco. Los atajos se envían a la aplicación que esté en primer plano cuando se presiona el botón físico.

La ejecución de atajos todavía no está disponible en Windows ni en Linux X11. La configuración se conserva en los perfiles mediante el esquema JSON v7, pero su ejecución está implementada actualmente solo para Linux Wayland.

Guía técnica y solución de problemas: [`docs/linux-wayland-keyboard.md`](docs/linux-wayland-keyboard.md).

## Hardware actual

| Componente | Modelo |
|---|---|
| Placa | ESP32 DevKit V1 con módulo ESP-WROOM-32 |
| Conversor USB-serie | Silicon Labs CP2102 |
| Conexión | Micro-USB |
| Entradas | 9 pulsadores momentáneos |

Cada pulsador conecta un GPIO con GND. El firmware configura las entradas con `INPUT_PULLUP`: el estado normal es HIGH y al presionar el botón pasa a LOW. **No conectar los GPIO a 5 V.**

### Mapa de botones y GPIO

| Botón en la aplicación | GPIO del ESP32 | Rotulado habitual en la placa |
|---:|---:|---|
| 1 | 13 | D13 |
| 2 | 14 | D14 |
| 3 | 16 | RX2 |
| 4 | 17 | TX2 |
| 5 | 18 | D18 |
| 6 | 19 | D19 |
| 7 | 21 | D21 |
| 8 | 22 | D22 |
| 9 | 23 | D23 |

El antirrebote del firmware es de aproximadamente 30 ms y genera un evento por cada pulsación estable.

## Tecnologías

- C# y .NET 10.
- Avalonia UI 12.1.4.
- CommunityToolkit.Mvvm 8.4.2.
- `System.IO.Ports` para la comunicación serie.
- `System.Text.Json` para la persistencia local.
- `Tmds.DBus.Protocol` y `Tmds.DBus.Generator` para la integración con XDG Desktop Portal en Linux Wayland.
- PlatformIO + framework Arduino para el firmware del ESP32.

## Arquitectura del repositorio

```text
StreamDeckESP32/
├── StreamDeckESP32.sln
├── Directory.Build.props
├── README.md
├── docs/
│   ├── architecture.md
│   ├── linux-wayland-keyboard.md
│   ├── serial-protocol-v1.md
│   └── windows-testing.md
├── firmware/
│   └── esp32/
│       ├── platformio.ini
│       └── src/main.cpp
├── src/
│   ├── StreamDeck.App/           # Interfaz Avalonia y ViewModels
│   ├── StreamDeck.Application/   # Casos de uso e interfaces
│   ├── StreamDeck.Domain/        # Perfiles, páginas y botones
│   ├── StreamDeck.Infrastructure/# JSON y ejecución de acciones
│   └── StreamDeck.Device/        # Comunicación serie con ESP32
└── tests/
    ├── StreamDeck.Domain.Tests/
    ├── StreamDeck.Application.Tests/
    └── StreamDeck.Infrastructure.Tests/
```

## Requisitos para desarrollar

- SDK de .NET 10.
- Acceso a Internet durante la primera restauración de paquetes NuGet.
- PlatformIO solamente para compilar o cargar el firmware del ESP32.

### Fedora Linux

Desde la carpeta raíz del repositorio:

```bash
dotnet restore StreamDeckESP32.sln
dotnet build StreamDeckESP32.sln
dotnet test StreamDeckESP32.sln
dotnet run --project src/StreamDeck.App/StreamDeck.App.csproj
```

En Fedora, el usuario debe tener permisos para acceder al puerto serie. Si todavía no pertenece al grupo `dialout`:

```bash
sudo usermod -aG dialout "$USER"
```

Después hay que cerrar la sesión y volver a iniciarla. El dispositivo CP2102 normalmente aparece como `/dev/ttyUSB0`; la aplicación enumera los puertos disponibles.

### Windows

Para probar y compilar esta versión con .NET 10, se recomienda **Windows 11 x64**. Microsoft limita la compatibilidad de .NET 10 en Windows 10 a ediciones LTSC/Enterprise y versiones concretas; Windows 10 Home/Pro no figura como plataforma admitida. Consultá la [matriz oficial de sistemas compatibles](https://learn.microsoft.com/es-es/dotnet/core/install/windows).

La aplicación puede probarse compilando el código fuente o generando una carpeta portable autocontenida. La guía completa está en [`docs/windows-testing.md`](docs/windows-testing.md).

Para compilar y ejecutar desde PowerShell, con el SDK .NET 10 instalado:

```powershell
dotnet restore .\StreamDeckESP32.sln
dotnet build .\StreamDeckESP32.sln
dotnet test .\StreamDeckESP32.sln
dotnet run --project .\src\StreamDeck.App\StreamDeck.App.csproj
```

Para crear una publicación portable Windows x64, también desde PowerShell:

```powershell
dotnet publish .\src\StreamDeck.App\StreamDeck.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\win-x64
```

Se debe copiar **la carpeta completa** `artifacts\publish\win-x64` al otro equipo y ejecutar `StreamDeck.App.exe`. Esta publicación incluye el runtime .NET, por lo que la computadora de destino no necesita instalar el SDK ni el runtime de .NET. Actualmente es una distribución portable en carpeta, no un instalador MSI.

## Firmware ESP32

El firmware se encuentra en `firmware/esp32`. Se carga una sola vez en la placa, salvo que se actualice el firmware. La aplicación de escritorio no necesita PlatformIO para ejecutarse.

Con PlatformIO CLI instalado:

```bash
cd firmware/esp32
pio run
pio run --target upload
```

El monitor serie puede abrirse con:

```bash
pio device monitor --baud 115200
```

## Protocolo serie

La comunicación utiliza texto ASCII a 115200 baudios, 8N1. La aplicación envía `HELLO 1`; el ESP32 responde `READY 1 ESP32-WROOM-32 9`. Cada pulsación válida se comunica como `BUTTON N`, donde `N` va de 1 a 9.

La especificación completa está en [`docs/serial-protocol-v1.md`](docs/serial-protocol-v1.md).

## Persistencia y datos

Los perfiles se guardan localmente en archivos JSON, separados por perfil. Los datos no se guardan en la placa ESP32 ni se sincronizan con la nube.

En Windows, el directorio habitual es:

```text
%APPDATA%\StreamDeckESP32\profiles
```

En Linux, la ubicación se determina mediante `Environment.SpecialFolder.ApplicationData` y normalmente queda bajo la carpeta de configuración del usuario.

Los perfiles pueden respaldarse copiando esa carpeta. Tené presente que las rutas de ejecutables son específicas de cada sistema operativo: una ruta configurada en Fedora no necesariamente existe en Windows. Las URLs, los colores y los nombres personalizados no dependen de esas rutas.

## Alcance actual y próximos pasos

No se implementaron todavía instaladores MSI/EXE, actualización automática, sincronización en la nube, macros, soporte de atajos para Windows ni imágenes personalizadas para los botones.
