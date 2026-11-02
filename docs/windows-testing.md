# Probar StreamDeck ESP32 en Windows

Esta guía cubre dos escenarios: ejecutar el proyecto desde el código fuente y preparar una carpeta portable para probarlo en otra computadora.

**Sistema recomendado: Windows 11 x64.** Para .NET 10, Microsoft limita el soporte de Windows 10 a determinadas versiones y ediciones LTSC/Enterprise; Windows 10 Home/Pro no figura como plataforma admitida. Revisá la [matriz oficial de compatibilidad de .NET 10](https://learn.microsoft.com/es-es/dotnet/core/install/windows) antes de probar en un equipo con Windows 10.

## 1. Ejecutar desde el código fuente

### Requisitos

- Windows 11 x64 recomendado; Windows 10 solamente en las ediciones/versiones LTSC o Enterprise que Microsoft incluye en su matriz de compatibilidad.
- SDK de .NET 10.
- Git, si se va a clonar el repositorio; no es necesario si se recibe el ZIP del proyecto.

Se puede instalar el SDK desde PowerShell o Windows Terminal con WinGet (el SDK incluye el runtime necesario para ejecutar y compilar proyectos .NET):

```powershell
winget install Microsoft.DotNet.SDK.10
```

También se puede descargar el instalador oficial desde [Instalar .NET en Windows](https://learn.microsoft.com/es-es/dotnet/core/install/windows). Para desarrollar y compilar se necesita el **SDK**, no solamente un runtime.

Comprobá la instalación:

```powershell
dotnet --version
dotnet --info
```

### Compilar y ejecutar

Extraé el ZIP del proyecto, abrí PowerShell en la carpeta `StreamDeckESP32` (la carpeta que contiene `StreamDeckESP32.sln`) y ejecutá:

```powershell
dotnet restore .\StreamDeckESP32.sln
dotnet build .\StreamDeckESP32.sln
dotnet test .\StreamDeckESP32.sln
dotnet run --project .\src\StreamDeck.App\StreamDeck.App.csproj
```

La primera restauración descarga los paquetes NuGet y necesita conexión a Internet.

## 2. Crear una versión portable para otra PC

Para preparar una publicación Windows x64 autocontenida, ejecutá desde la raíz del repositorio:

```powershell
dotnet publish .\src\StreamDeck.App\StreamDeck.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\win-x64
```

Al terminar, copiá la carpeta completa `artifacts\publish\win-x64` a la otra computadora, por ejemplo mediante un ZIP o una memoria USB. Extraela y ejecutá:

```text
StreamDeck.App.exe
```

No copies únicamente el `.exe`: la carpeta contiene bibliotecas y archivos necesarios. Esta publicación es autocontenida y no requiere que la computadora de destino tenga instalado .NET. Actualmente el proyecto no genera un instalador MSI/EXE ni registra accesos directos automáticamente. Para esta etapa, la carpeta portable es el método de distribución previsto para las pruebas.

Opcionalmente, podés empaquetar esa carpeta para transferirla:

```powershell
Compress-Archive -Path .\artifacts\publish\win-x64\* -DestinationPath .\StreamDeckESP32-win-x64.zip -Force
```

## 3. Conectar el ESP32

El firmware ya cargado en la placa permanece en ella; no hay que volver a flashearlo para probar la aplicación en otra computadora.

1. Conectá la placa ESP32 DevKit V1 mediante un cable Micro-USB que permita datos.
2. Windows puede reconocer el conversor CP2102 automáticamente. Si no aparece un puerto serie, instalá el controlador oficial [Silicon Labs CP210x USB to UART VCP](https://www.silabs.com/software-and-tools/usb-to-uart-bridge-vcp-drivers).
3. Abrí el **Administrador de dispositivos** y desplegá **Puertos (COM y LPT)**. Buscá una entrada similar a `Silicon Labs CP210x USB to UART Bridge (COM3)`; el número COM puede ser diferente.
4. Abrí StreamDeck ESP32, presioná actualizar puertos si hace falta, elegí el puerto `COMx` y presioná **Conectar**.
5. Una vez que el estado cambie a conectado, probá los nueve pulsadores.

Si el CP2102 aparece como dispositivo desconocido o no se crea ningún puerto COM, instalá el controlador y desconectá/conectá nuevamente la placa. Si otro programa (por ejemplo, PlatformIO Serial Monitor o Arduino Serial Monitor) tiene abierto el puerto, cerralo antes de conectar desde StreamDeck ESP32.

## 4. Probar las acciones

- **Abrir URL:** configurá una dirección `https://` y probá la acción; Windows la abrirá con el navegador predeterminado.
- **Ejecutar aplicación:** seleccioná un ejecutable de Windows (`.exe`). No reutilices rutas de Fedora como `/usr/bin/firefox`.
- **Bandeja del sistema:** minimizá la ventana y verificá que desaparezca de la barra de tareas. El icono de StreamDeck ESP32 debe quedar en el área de notificaciones o dentro del panel de iconos ocultos. Desde su menú, elegí **Mostrar StreamDeck ESP32** para restaurar la ventana o **Salir** para cerrar la aplicación.
- **Colores:** asigná un color desde la paleta de propiedades. El color se guarda en el perfil y es visual solamente; no necesita soporte adicional del ESP32.

Los perfiles de Windows se almacenan bajo:

```text
%APPDATA%\StreamDeckESP32\profiles
```

La primera ejecución en otro usuario o computadora puede mostrar la lista de perfiles vacía porque los archivos JSON son locales. Para migrar configuraciones, copiá los archivos JSON a esa carpeta con la aplicación cerrada. Revisá las rutas de ejecutables después de migrar entre Windows y Linux.
