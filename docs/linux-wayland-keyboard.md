# Autorización de teclado en Linux Wayland

## Alcance de esta etapa

Esta etapa integra StreamDeckESP32 con el portal estándar `org.freedesktop.portal.RemoteDesktop` para solicitar autorización de teclado en una sesión Linux Wayland.

La aplicación solicita exclusivamente el dispositivo `KEYBOARD` (`types = 1`). No solicita `POINTER` (mouse) ni `TOUCHSCREEN`. La etapa 10.2 gestiona la autorización y el ciclo de vida de la sesión. La etapa 10.3 utiliza esa sesión para enviar combinaciones de teclas mediante los métodos D-Bus Notify del portal. La etapa 10.4 incorpora la configuración de esas combinaciones desde el panel de propiedades de cada botón.

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

## Configurar un atajo desde la aplicación

En una sesión Linux Wayland, seleccioná un botón de la cuadrícula. Si no tiene acción, elegí **Atajo de teclado** en el panel de propiedades; si ya tiene una acción, elegí **Cambiar atajo de teclado**.

El diálogo permite seleccionar uno o varios modificadores (Ctrl, Alt, Shift y Super), además de una tecla principal. `Shift` corresponde a la tecla **Mayús**. Por ejemplo, se puede configurar `Alt + Shift + F4` además de combinaciones como `Ctrl + C` o `Super + D`. Se admiten letras, números, F1–F12 y teclas comunes de navegación y edición. También se puede configurar una tecla sin modificadores. Al guardar, el atajo queda persistido dentro de la acción del botón.

Para ejecutar el atajo, el teclado debe estar autorizado previamente desde el menú del icono de la bandeja. Los atajos se prueban con el botón físico del ESP32 mientras la aplicación de destino está en primer plano; el botón **Probar acción** del panel se oculta para los atajos porque al pulsarlo la propia ventana StreamDeckESP32 tendría el foco.

La configuración de atajos se muestra únicamente cuando la aplicación se inicia dentro de una sesión Linux Wayland. En esta etapa todavía no se ofrece ejecución de atajos en Windows ni en Linux X11.


## Etapa 10.3: envío de combinaciones

Una vez autorizada la sesión de teclado, `WaylandKeyboardInputSession.SendShortcutAsync` utiliza `org.freedesktop.portal.RemoteDesktop.NotifyKeyboardKeysym`.

Para cada combinación:

1. Envía los modificadores en orden estable (Ctrl, Alt, Shift, Super).
2. Presiona la tecla principal.
3. Libera la tecla principal y luego los modificadores en orden inverso.
4. Si una llamada falla o se cancela el envío, intenta liberar todas las teclas cuyo evento de pulsación se inició.

Los estados del portal son `1` para pulsado y `0` para liberado. La aplicación usa KeySyms X11 estándar: letras y dígitos ASCII, y KeySyms definidos para teclas especiales y modificadores.

La sesión debe haber sido autorizada previamente desde el menú de la bandeja. La ejecución de un atajo no abre automáticamente el diálogo de permisos. La interfaz para asignar y editar atajos está disponible en la etapa 10.4.

La implementación usa exclusivamente los métodos D-Bus `Notify*` del portal; no llama a `ConnectToEIS` en la misma sesión.
