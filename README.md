# StreamDeck ESP32

Aplicación de escritorio para administrar y configurar un Stream Deck físico
basado en ESP32. Editor/administrador del dispositivo — no es un Stream Deck
virtual.

## Stack

- C# / .NET 10 LTS
- Avalonia UI 12.1.4 (MVVM, CommunityToolkit.Mvvm 8.4.2)
- Windows y Linux
- Tema oscuro fijo

## Estructura
StreamDeckESP32/
├── StreamDeckESP32.sln
├── Directory.Build.props
├── .gitignore
├── README.md
├── docs/
│ ├── specification.md
│ └── architecture.md
├── src/
│ ├── StreamDeck.App/ # UI Avalonia (MVVM)
│ ├── StreamDeck.Application/ # Casos de uso
│ ├── StreamDeck.Domain/ # Entidades y reglas de negocio
│ ├── StreamDeck.Infrastructure/ # Acceso a SO, futuro JSON
│ └── StreamDeck.Device/ # Futura comunicación con el ESP32
└── tests/


Ver `docs/specification.md` y `docs/architecture.md` para el detalle completo.

## Compilar y ejecutar

```bash
dotnet restore
dotnet build
dotnet run --project src/StreamDeck.App
```

## Estado

Etapa 1 y 1.2 completadas: solución, 5 proyectos, ventana principal mínima
con tema oscuro fijo. Sin perfiles, botones, JSON, USB/Bluetooth todavía.
docs/specification.md
markdown
# Especificación del proyecto — StreamDeck ESP32

## Qué es

Aplicación de escritorio para administrar y configurar un Stream Deck físico
basado en ESP32. Es un editor/administrador del dispositivo, no un Stream
Deck virtual ni una réplica por software del hardware.

## Stack tecnológico

- Lenguaje: C#
- Framework: .NET 10 LTS
- Interfaz gráfica: Avalonia UI 12.1.4
- Patrón de interfaz: MVVM, con CommunityToolkit.Mvvm 8.4.2
- Arquitectura: separación por capas (ver `architecture.md`)
- Sistemas operativos soportados: Windows y Linux
- Apariencia: tema oscuro fijo (no sigue el tema del sistema)
- Almacenamiento futuro: archivos JSON locales
- Conectividad futura: USB y Bluetooth

## Metodología de trabajo

- El manager del proyecto define arquitectura, tecnologías y planificación.
- El programador implementa exactamente lo indicado, etapa por etapa, sin
  rediseñar ni anticipar funcionalidades no solicitadas.
- El desarrollo es incremental: cada etapa requiere aprobación del manager
  antes de avanzar a la siguiente.
- El programador no tiene acceso directo al entorno del usuario: entrega
  código y comandos; el usuario aplica los cambios localmente y comparte
  los resultados reales de compilación/ejecución.

## Estado actual

- **Etapa 1 — Completada**: solución inicial, 5 proyectos creados,
  compilando y ejecutando en Fedora KDE / Wayland.
- **Etapa 1.2 — Completada**: tema oscuro fijo, `Directory.Build.props`
  centralizando `Nullable` e `ImplicitUsings`, limpieza de archivos de
  plantilla sin usar (carpeta `Models/` vacía, clases `Class1` vacías),
  `.gitignore`, documentación inicial autocontenida.

## Qué todavía no existe

- Perfiles, páginas, cuadrícula de botones.
- Botones configurables, acciones, macros.
- Almacenamiento en JSON.
- Bandeja del sistema.
- Comunicación USB o Bluetooth con el ESP32.
- Sistema de actualización, instaladores.
- Python, bases de datos o servicios en la nube: descartados explícitamente.