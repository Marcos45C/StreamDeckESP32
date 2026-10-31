# Arquitectura — StreamDeck ESP32

## Proyectos y responsabilidades

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `StreamDeck.Domain` | Entidades, modelos de negocio, enumeraciones, objetos de valor, reglas de dominio. | (ninguno interno) |
| `StreamDeck.Application` | Casos de uso, interfaces de servicios, coordinación de funcionalidades. | Domain |
| `StreamDeck.Infrastructure` | Futuro almacenamiento JSON, acceso al sistema operativo, ejecución de acciones. | Application, Domain |
| `StreamDeck.Device` | Futura administración del ESP32: estado de conexión, USB, Bluetooth. | Application, Domain |
| `StreamDeck.App` | Interfaz Avalonia (MVVM): ventanas, vistas, ViewModels, recursos visuales. | Application, Infrastructure, Device |

Sin dependencias circulares. `Domain` es la única capa sin dependencias internas.

## Configuración compartida

`Directory.Build.props` (raíz del repo) centraliza, para todos los proyectos:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

Todos los proyectos apuntan a `net10.0`.

## MVVM en StreamDeck.App

- `ViewModels/ViewModelBase.cs`: clase base abstracta sobre `ObservableObject` (CommunityToolkit.Mvvm).
- `ViewModels/MainViewModel.cs`: ViewModel de la ventana principal. Usa propiedades parciales con `[ObservableProperty]` (C# 13, incluido por defecto al apuntar a `net10.0` — sin `LangVersion=preview`).
- `Views/MainWindow.axaml` + `.axaml.cs`: vista de la ventana principal. El `DataContext` se asigna explícitamente en `App.axaml.cs`.
- `ViewLocator.cs`: resuelve automáticamente una vista `XxxView` para un ViewModel `XxxViewModel`, pensado para vistas que se agreguen en etapas futuras. Registrado en `App.axaml` vía `Application.DataTemplates`.
- Tema: `RequestedThemeVariant="Dark"` fijo en `App.axaml` (no sigue el tema del sistema).

## Paquetes NuGet utilizados

Solo en `StreamDeck.App`:

- `Avalonia` 12.1.4
- `Avalonia.Desktop` 12.1.4
- `Avalonia.Themes.Fluent` 12.1.4
- `Avalonia.Fonts.Inter` 12.1.4
- `AvaloniaUI.DiagnosticsSupport` 2.2.3 (solo en Debug)
- `CommunityToolkit.Mvvm` 8.4.2

Ningún otro proyecto tiene paquetes NuGet adicionales.

## Convenciones

- Namespace por proyecto: `StreamDeck.<Capa>` (ej. `StreamDeck.App.ViewModels`).
- Nombre de archivo = nombre de clase.
- Vistas en `Views/`, ViewModels en `ViewModels/`, modelos de dominio en `StreamDeck.Domain` (no dentro de `StreamDeck.App`).