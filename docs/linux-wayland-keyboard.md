# Autorización de teclado en Linux Wayland

## Alcance de esta etapa

Esta etapa integra StreamDeckESP32 con el portal estándar `org.freedesktop.portal.RemoteDesktop` para solicitar autorización de teclado en una sesión Linux Wayland.

La aplicación solicita exclusivamente el dispositivo `KEYBOARD` (`types = 1`). No solicita `POINTER` (mouse) ni `TOUCHSCREEN`. Esta etapa solamente gestiona la autorización y el ciclo de vida de la sesión: todavía no envía combinaciones de teclas.

El portal devuelve `session_handle` dentro de `Request.Response` como un valor D-Bus `string` (`s`), aunque su contenido representa una ruta de objeto. La implementación valida esa ruta antes de utilizarla en `SelectDevices` y `Start`.

## Requisitos

- Sistema Linux con sesión Wayland.
- D-Bus de sesión del usuario disponible.
- Servicio `org.freedesktop.portal.Desktop` disponible.
- Interfaz `org.freedesktop.portal.RemoteDesktop` versión 2 o superior.
- La propiedad `AvailableDeviceTypes` debe incluir el bit `1` (teclado).

Comprobá el tipo de sesión:

```bash
echo "$XDG_SESSION_TYPE"
```

Comprobá la versión y capacidades del portal:

```bash
busctl --user get-property \
  org.freedesktop.portal.Desktop \
  /org/freedesktop/portal/desktop \
  org.freedesktop.portal.RemoteDesktop version

busctl --user get-property \
  org.freedesktop.portal.Desktop \
  /org/freedesktop/portal/desktop \
  org.freedesktop.portal.RemoteDesktop AvailableDeviceTypes
```

Un resultado `u 2` para `version` y `u 7` para `AvailableDeviceTypes`, como en el entorno de desarrollo utilizado para este proyecto, indica que el portal ofrece versión 2 y publica las capacidades de teclado, mouse y pantalla táctil. StreamDeckESP32 solicita solamente teclado.

## Autorizar el teclado

1. Iniciá StreamDeckESP32 en una sesión Wayland.
2. Abrí el menú contextual del icono de StreamDeckESP32 en la bandeja del sistema.
3. Seleccioná **Autorizar atajos de teclado (Linux Wayland)**.
4. La aplicación restaura la ventana principal y solicita autorización al portal.
5. Revisá el diálogo que muestra KDE y aceptá el acceso al teclado.
6. La aplicación debe mostrar el estado **Teclado autorizado**.

El portal presenta el diálogo durante la operación `Start`. La respuesta satisfactoria debe incluir el bit de dispositivo teclado. La aplicación rechaza una respuesta que no incluya teclado o que informe dispositivos adicionales no solicitados.

## Persistencia y cierre

La aplicación solicita `persist_mode = 1`: si el escritorio concede esa modalidad, el permiso se mantiene mientras StreamDeckESP32 esté en ejecución. Minimizar la ventana a la bandeja no cierra la sesión.

Al elegir **Salir** o cerrar la ventana, StreamDeckESP32 completa primero la desconexión del ESP32 y luego llama a `Close` sobre la sesión RemoteDesktop y libera la conexión D-Bus.

Si KDE cierra la sesión o la conexión D-Bus se interrumpe, la aplicación marca la autorización como perdida. Se puede volver a solicitar desde el menú de la bandeja.

## Si se cancela o deniega

Si el usuario cancela o deniega la solicitud, la aplicación no marca el teclado como autorizado y muestra un mensaje. Podés abrir nuevamente el menú y repetir **Autorizar atajos de teclado (Linux Wayland)**.

Si el portal no está disponible, no informa versión suficiente o no ofrece teclado, la aplicación informa el motivo y no continúa con la autorización.

## Próxima etapa

Esta etapa no envía teclas. La implementación de eventos mediante `NotifyKeyboardKeysym` o `NotifyKeyboardKeycode`, junto con la asignación de atajos a los botones físicos, queda para etapas posteriores.
