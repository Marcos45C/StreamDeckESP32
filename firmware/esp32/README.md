#  StreamDeck ESP32 — Firmware

Firmware para la placa física (ESP32 DevKit V1 / ESP-WROOM-32) que implementa el
protocolo serie descrito en [`docs/serial-protocol-v1.md`](../../docs/serial-protocol-v1.md).

## Requisitos

- [PlatformIO Core](https://platformio.org/install/cli) instalado (`pip install platformio`,
  o la extensión de PlatformIO en VS Code).
- Cable Micro-USB conectado a la placa.
- En Fedora, el usuario debe pertenecer al grupo `dialout`:

  ```bash
  sudo usermod -aG dialout $USER
  # cerrar sesión y volver a iniciarla para que el cambio de grupo tenga efecto
  ```

## Compilar

Desde `firmware/esp32/`:

```bash
pio run
```

## Cargar el firmware

Con la placa conectada por USB:

```bash
pio run --target upload
```

PlatformIO detecta el puerto automáticamente (en Fedora, normalmente `/dev/ttyUSB0`). Para
forzarlo explícitamente:

```bash
pio run --target upload --upload-port /dev/ttyUSB0
```

## Abrir el monitor serie

```bash
pio device monitor --baud 115200
```

O apuntando a un puerto específico:

```bash
pio device monitor --port /dev/ttyUSB0 --baud 115200
```

Para salir: `Ctrl+C`.

## Comprobar el handshake manualmente

Con el monitor serie abierto:

1. Escribir `HELLO 1` y Enter. Debe responder:

READY 1 ESP32-WROOM-32 9

2. Escribir `HELLO 2`. Debe responder:

ERROR UNSUPPORTED_VERSION

3. Escribir `HELLO 1` de nuevo. Debe volver a responder `READY 1 ESP32-WROOM-32 9`.
4. Escribir un comando desconocido (por ejemplo `PING`) o Enter sin texto: el firmware no
   debe bloquearse ni responder nada.
5. Solo después de completar el handshake (paso 1 o 3), tocar brevemente con un cable
   puente el pin de un botón (por ejemplo GPIO 13, posición 1) contra GND. Debe enviarse
   `BUTTON 1`. Antes del handshake, no debe enviarse nada al tocarlo.

> El monitor serie de PlatformIO suele enviar CRLF al presionar Enter; el firmware lo
> acepta sin problema según el protocolo.

## Notas

- `platformio.ini` no fija puerto de carga ni de monitor, para funcionar igual en Linux y
  Windows.
- Sin dependencias de terceros: solo el framework Arduino incluido con `espressif32`.