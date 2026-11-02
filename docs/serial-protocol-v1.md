# Protocolo serie StreamDeck ESP32 — versión 1

## 1. Objetivo

Definir el protocolo de comunicación entre la aplicación de escritorio StreamDeck ESP32
(C# / .NET 10 / Avalonia) y el firmware del dispositivo físico ESP32, de forma que la
aplicación pueda:

1. Detectar que el ESP32 está conectado.
2. Establecer comunicación mediante un handshake simple.
3. Confirmar que el dispositivo está listo.
4. Recibir pulsaciones de los nueve botones físicos.
5. Diferenciar cada botón por su posición (1 a 9).

La computadora es la única responsable de interpretar las pulsaciones y ejecutar las
acciones configuradas en el perfil y la página activos. El ESP32 no almacena perfiles,
páginas ni rutas de aplicaciones — es un dispositivo de entrada "tonto" desde el punto de
vista de los datos.

## 2. Hardware compatible

- **Placa:** ESP32 DevKit V1 (módulo ESP-WROOM-32).
- **Conversor USB-serie:** Silicon Labs CP2102.
- **Conector:** Micro-USB.

### Entorno de referencia (Fedora Linux 44 KDE)

- Puerto detectado: `/dev/ttyUSB0`.
- Identificador persistente recomendado:
  `/dev/serial/by-id/usb-Silicon_Labs_CP2102_USB_to_UART_Bridge_Controller_0001-if00-port0`.
- El usuario debe pertenecer al grupo `dialout` para tener permisos de lectura/escritura
  sin privilegios elevados.

### Windows

Se utilizará el puerto `COMx` correspondiente, asignado por el driver del CP2102. El
protocolo en sí es idéntico; solo cambia la forma de nombrar el puerto a nivel de sistema
operativo.

## 3. Configuración del puerto serie

| Parámetro | Valor |
|---|---|
| Velocidad | 115200 baudios |
| Bits de datos | 8 |
| Paridad | Ninguna |
| Bits de parada | 1 (8N1) |
| Control de flujo por hardware | Ninguno |
| Codificación | ASCII |
| Formato | Un mensaje por línea |
| Terminador al enviar | LF (`\n`) |
| Terminador aceptado al recibir | LF (`\n`) o CRLF (`\r\n`) |
|Tamaño máximo de mensaje | 64 bytes (contenido del mensaje; no incluye el terminador LF ni CRLF) |

Esta primera versión del protocolo usa texto plano ASCII, sin JSON ni formatos binarios,
para que sea trivial de implementar tanto en C++ (ESP32, con `Serial.print`/`Serial.read`)
como en C# (.NET, con `System.IO.Ports.SerialPort` o equivalente).

## 4. Formato de mensajes — versión 1

### 4.1 Computadora → ESP32

**Inicialización del handshake:**

HELLO 1

El número es la versión de protocolo que habla la aplicación. Se envía cada vez que la
aplicación abre correctamente el puerto serie, y puede reenviarse en cualquier momento si
la aplicación necesita repetir el handshake (por ejemplo, tras una reconexión).

### 4.2 ESP32 → Computadora

**Respuesta de inicialización:**
READY 1 ESP32-WROOM-32 9


Campos, separados por un espacio:

| # | Campo | Significado |
|---|---|---|
| 1 | `READY` | El dispositivo está preparado para operar |
| 2 | `1` | Versión de protocolo que habla el firmware |
| 3 | `ESP32-WROOM-32` | Identificador del modelo del dispositivo |
| 4 | `9` | Cantidad de botones físicos disponibles |

La aplicación **solo** debe considerar el dispositivo conectado y listo después de recibir
y validar esta respuesta — abrir el puerto serie con éxito no es, por sí solo, prueba de
que hay un ESP32 con este firmware al otro lado.
Ver sección 5.2 para el detalle exacto de qué campos se validan y qué ocurre si alguno es
incorrecto o falta.

**Versión incompatible:**

ERROR UNSUPPORTED_VERSION


El ESP32 responde esto si la versión recibida en `HELLO` no es una que sepa hablar.

**Pulsación de botón:**

BUTTON <posición>


Donde `<posición>` es un entero de 1 a 9 que identifica la posición física del botón
pulsado (la misma numeración 1-9 de la grilla 3×3 ya usada en la aplicación y en el
dominio — `ButtonSlot.Position`).

Reglas:

- Se envía **un único mensaje por pulsación válida** detectada (transición estable de no
  pulsado a pulsado).
- No se reenvía nada mientras el botón se mantiene presionado.
- No es necesario (ni se envía) ningún mensaje al soltar el botón.

### 4.3 Ejemplos

```text
# La computadora abre el puerto y saluda
> HELLO 1

# El ESP32 responde que está listo
< READY 1 ESP32-WROOM-32 9

# El usuario presiona el botón físico en la posición 5
< BUTTON 5

# El usuario presiona el botón físico en la posición 1
< BUTTON 1

# La computadora repite el handshake
> HELLO 1
< READY 1 ESP32-WROOM-32 9

# Caso de versión incompatible
> HELLO 2
< ERROR UNSUPPORTED_VERSION
```

## 5. Secuencia de conexión

1. El usuario conecta el ESP32 por USB.
2. La aplicación detecta y abre el puerto serie correspondiente.
3. La aplicación envía `HELLO 1`.
4. El ESP32 recibe el mensaje.
5. El ESP32 valida que la versión (`1`) es compatible con su firmware.
6. Si es compatible, el ESP32 responde `READY 1 ESP32-WROOM-32 9`.
   Si no lo es, responde `ERROR UNSUPPORTED_VERSION` y no pasa a estado "listo".
7. La aplicación valida la respuesta `READY` (formato y versión) y recién entonces marca
   el dispositivo como conectado en la interfaz.
8. A partir de ese momento, el ESP32 comienza a comunicar pulsaciones de forma
   espontánea (no solicitadas), según ocurran.
9. Cuando el usuario presiona un botón físico, el ESP32 envía `BUTTON N`.
10. La aplicación interpreta `N` como una posición (1-9), la resuelve contra el perfil y
    la página actualmente activos, y ejecuta la acción configurada en ese `ButtonSlot`
    (si existe alguna).

El dispositivo debe poder responder nuevamente a un `HELLO` en cualquier momento,
incluso ya estando en estado "listo" — repetir el handshake no debe dejarlo en un estado
inconsistente.

### 5.1 Reintentos del handshake

Abrir el puerto serie de un CP2102 puede provocar un **reinicio del ESP32** por el
comportamiento de las señales DTR/RTS del conversor USB-UART — esto depende del circuito
específico de la placa (auto-reset) y del driver del sistema operativo, y no es algo que
la aplicación pueda controlar ni desactivar de forma confiable. El handshake debe
diseñarse asumiendo que esto puede ocurrir en cualquier apertura de puerto.

Secuencia que debe seguir la aplicación al abrir el puerto:

1. Abrir el puerto serie.
2. Esperar aproximadamente **200 ms** antes de enviar el primer `HELLO 1` (margen para
   que, si el ESP32 se reinició al abrir el puerto, el firmware llegue a arrancar y
   quede escuchando).
3. Enviar `HELLO 1`.
4. Si no llega una respuesta `READY` válida (ver sección 5.2), reenviar `HELLO 1` cada
   **500 ms**.
5. Mantener una ventana máxima de **5 segundos**, contados desde el primer `HELLO`, para
   completar el handshake.
6. Si se agota ese tiempo sin recibir una respuesta `READY` válida, la aplicación debe
   informar que el dispositivo no respondió y permitir que el usuario intente de nuevo
   (repitiendo esta misma secuencia desde el paso 1 o el paso 3, según corresponda).

El firmware, del lado del ESP32, debe responder `READY 1 ESP32-WROOM-32 9` cada vez que
reciba un `HELLO` compatible — **incluso si ya estaba en estado "listo"** y ya le había
respondido antes a un `HELLO` anterior. No debe ignorar un `HELLO` repetido asumiendo que
la aplicación ya lo sabe.

Estos reintentos son exclusivos del handshake inicial. Los eventos `BUTTON N` no tienen
ningún mecanismo de reintento o confirmación (ver sección 8 y sección 10).

### 5.2 Validación de la respuesta READY

La aplicación solo debe aceptar una respuesta `READY` y marcar el dispositivo como
conectado después de validar explícitamente **los cuatro campos**:

1. La palabra `READY` (primer campo, literal).
2. La versión de protocolo: debe ser `1`.
3. El identificador de dispositivo: debe ser `ESP32-WROOM-32`.
4. La cantidad de botones: debe ser `9`.

Un mensaje incompleto (con menos de cuatro campos), con campos en un orden distinto, o
con cualquiera de estos cuatro valores incorrecto, **no debe establecer la conexión** —
se trata igual que si no hubiera llegado ninguna respuesta válida, y el handshake sigue
reintentando según la secuencia de la sección 5.1 hasta agotar la ventana de 5 segundos.

## 6. Mapa de GPIO (lógico/físico)

Asignación inicial de pines para los nueve pulsadores. Documentado únicamente como
referencia para el cableado futuro — **todavía no implementado ni cableado**.

| Posición | GPIO |
|---:|---:|
| 1 | 13 |
| 2 | 14 |
| 3 | 16 |
| 4 | 17 |
| 5 | 18 |
| 6 | 19 |
| 7 | 21 |
| 8 | 22 |
| 9 | 23 |

> **Nota:** los GPIO 16 y 17 corresponden a los pines serigrafiados como `RX2`/`TX2` en la
> placa (el segundo UART de hardware del ESP32, `UART2`). Como esta versión del firmware
> no usa `UART2` para nada, quedan libres para uso como entradas digitales genéricas sin
> conflicto. Si una versión futura necesitara `UART2`, estas dos posiciones deberían
> reasignarse a otros GPIO libres.

## 7. Detección de pulsaciones (comportamiento previsto del firmware)

Documentado para una implementación futura — **no implementado todavía**:

- Cada pulsador conecta su GPIO a GND al presionarse.
- Cada pin se configura como `INPUT_PULLUP`.
- Estado normal (no pulsado): `HIGH`.
- Estado pulsado: `LOW`.
- Antirrebote (debounce) por software: ventana de aproximadamente **30 ms** de estabilidad
  antes de considerar válido un cambio de estado.
- Se emite **un solo evento** (`BUTTON N`) al detectar una transición estable de
  no-pulsado → pulsado.
- No se repiten eventos mientras el botón permanece presionado (sin "auto-repeat").
- El pulsador vuelve a estar habilitado para generar un nuevo evento recién después de
  detectar una liberación estable (transición pulsado → no-pulsado, también con su propia
  ventana de antirrebote).

## 8. Tratamiento de mensajes y manejo de errores

Reglas que debe seguir el lado que recibe (aplicación de escritorio leyendo del ESP32, y
ESP32 leyendo de la aplicación), documentadas para ambas implementaciones futuras:

- **No asumir alineación con los límites de una lectura USB**: una lectura física del
  puerto puede traer un mensaje incompleto, un mensaje completo, varios mensajes juntos,
  o un mensaje partido entre dos lecturas. El receptor debe acumular bytes en un buffer
  hasta encontrar el terminador de línea (`\n`, aceptando también `\r\n`) antes de
  procesar un mensaje.
- **Mensajes vacíos o inválidos** (que no matchean ningún comando reconocido, o con
  campos faltantes) se descartan sin generar una respuesta de error explícita salvo en el
  caso ya definido de `ERROR UNSUPPORTED_VERSION`.
- **Posiciones fuera de rango** (`BUTTON 0`, `BUTTON 10`, etc.) se rechazan — la
  aplicación las ignora en vez de intentar interpretarlas.
- **Comandos desconocidos** se ignoran sin bloquear ni reiniciar el dispositivo ni la
  conexión — el protocolo debe seguir funcionando con normalidad ante un mensaje que no
  reconoce.
 - **Mensajes que superan el tamaño máximo** (64 bytes) se descartan por completo: el
- **Mensajes que superan el tamaño máximo** (64 bytes de contenido, sin contar el
  terminador) se descartan por completo: el
   receptor sigue consumiendo bytes sin procesarlos hasta encontrar el próximo `\n`, y
   recién ahí retoma el procesamiento normal.
- **Una desconexión USB se trata como pérdida de conexión**, nunca como una pulsación o
  un mensaje válido. La aplicación debe poder detectar esta condición (cierre/error del
  puerto) y marcar el dispositivo como desconectado en la interfaz.
- **Reconexión:** tras una desconexión (USB reinsertado, puerto reabierto), la aplicación
  debe poder repetir el handshake (`HELLO 1` → `READY 1 ...`) desde cero, sin necesitar
  reiniciar el ESP32.

Esta primera versión deliberadamente **no** incluye retransmisión, heartbeat periódico ni
confirmación individual de cada pulsación — se prioriza simplicidad. Si en el futuro se
detecta pérdida real de eventos `BUTTON N` en uso normal, seria candidato para una v2 del
protocolo (ver sección 10).

## 9. Responsabilidades de cada lado

### ESP32 (firmware)

- Detectar el estado de los 9 pulsadores físicos.
- Aplicar antirrebote por software (~30 ms).
- Responder correctamente al handshake (`HELLO` → `READY` o `ERROR UNSUPPORTED_VERSION`).
- Enviar `BUTTON N` con la posición física correspondiente, una vez por pulsación válida.
- No tomar ninguna decisión sobre qué acción ejecutar — solo reporta posiciones.

### Aplicación de escritorio (StreamDeck ESP32 / C#)

- Abrir y cerrar el puerto serie.
- Detectar conexión y desconexión del dispositivo.
- Realizar el handshake y validar la respuesta `READY` antes de marcar el dispositivo
  como conectado.
- Validar y descartar mensajes malformados, fuera de rango o desconocidos.
- Interpretar cada `BUTTON N` recibido como una pulsación en la posición `N`.
- Consultar cuál es el perfil y la página actualmente activos.
- Resolver la posición contra el `ButtonSlot` correspondiente de esa página y, si tiene
  una acción asignada, ejecutarla (reutilizando el mismo servicio de ejecución ya
  implementado para "Probar acción" en la Etapa 7.7).

La computadora es la única fuente de verdad para perfiles, páginas y acciones — el ESP32
nunca almacena ni necesita conocer esos datos.

## 10. Consideraciones para futuras versiones del protocolo

Ideas explícitamente fuera de alcance de la versión 1, que podrían abordarse en una v2 si
resultan necesarias en uso real:

- **Heartbeat periódico** para detectar más rápido una desconexión silenciosa (cable
  flojo, ESP32 colgado) sin depender únicamente de errores del driver USB.
- **Confirmación (ACK) de pulsaciones**, si se observa pérdida de eventos en escenarios
  de pulsaciones muy rápidas o seguidas.
- **Indicadores de estado hacia el ESP32** (por ejemplo, LED de "conectado"/"listo"),
  que requeriría mensajes nuevos computadora → ESP32.
- **Negociación de capacidades** más rica que el simple número de versión actual, si el
  hardware futuro soporta más o menos de 9 botones, o botones con LED individual.
- **Cifrado o autenticación** del enlace serie — no considerado necesario mientras la
  conexión sea exclusivamente por cable USB físico.

Cualquiera de estos cambios debe mantener compatibilidad hacia atrás donde sea posible,
siguiendo el mismo patrón ya usado para la evolución del esquema JSON de persistencia
(versión declarada explícitamente en el mensaje, rechazo claro de versiones no
soportadas).
