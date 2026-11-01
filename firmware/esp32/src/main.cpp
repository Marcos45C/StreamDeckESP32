#include <Arduino.h>
#include <cstring>
#include <cstdlib>
#include <cctype> // isdigit()

// ---------- Protocolo (ver docs/serial-protocol-v1.md) ----------

constexpr int PROTOCOL_VERSION = 1;
constexpr const char *DEVICE_MODEL = "ESP32-WROOM-32";
constexpr uint8_t BUTTON_COUNT = 9;
constexpr uint8_t MAX_MESSAGE_LENGTH = 64; // Contenido del mensaje, sin contar el terminador.
constexpr uint8_t BUFFER_CAPACITY = MAX_MESSAGE_LENGTH + 1;
constexpr unsigned long DEBOUNCE_MS = 30;

bool deviceReady = false;
bool isValidVersionToken(const char *token);
// ---------- Botones ----------

struct ButtonConfig {
  uint8_t position;
  uint8_t pin;
};

const ButtonConfig BUTTONS[BUTTON_COUNT] = {
  {1, 13},
  {2, 14},
  {3, 16},
  {4, 17},
  {5, 18},
  {6, 19},
  {7, 21},
  {8, 22},
  {9, 23},
};

struct ButtonState {
  int lastRawReading;
  int stableState;
  unsigned long lastChangeTime;
};

ButtonState buttonStates[BUTTON_COUNT];

// ---------- Receptor serie ----------

char messageBuffer[BUFFER_CAPACITY + 1]; 
uint8_t messageLength = 0;
bool overflowing = false; // true mientras se descarta un mensaje que superó el máximo.

// ---------- Declaraciones ----------

void initializeButtons();
void updateButtons();
void onButtonPressed(uint8_t position);

void pollSerial();
void processMessage(char *message, uint8_t length);
void handleHello(char *versionToken);

void sendReady();
void sendError();
void sendButton(uint8_t position);

// ---------- Setup / loop ----------

void setup() {
  Serial.begin(115200);
  initializeButtons();
  // No se envía READY automáticamente al arrancar: se responde solo ante un HELLO.
}

void loop() {
  pollSerial();
  updateButtons();
  // Sin delay(): ambas funciones son no bloqueantes.
}

bool isValidVersionToken(const char *token) {
  if (token == nullptr || token[0] == '\0') {
    return false;
  }

  for (const char *p = token; *p != '\0'; p++) {
    if (!isdigit(static_cast<unsigned char>(*p))) {
      return false;
    }
  }

  return true;
}

void handleHello(char *versionToken) {
  if (!isValidVersionToken(versionToken)) {
    return; // Versión ausente o no íntegramente numérica: mensaje malformado, se ignora sin alterar el estado.
  }

  char *extraToken = strtok(nullptr, " ");
  if (extraToken != nullptr) {
    return; // Campos adicionales después de la versión: mensaje malformado, se ignora sin alterar el estado.
  }

  int version = atoi(versionToken); // Seguro: versionToken ya fue validado como íntegramente numérico.

  if (version == PROTOCOL_VERSION) {
    deviceReady = true;
    sendReady();
  } else {
    deviceReady = false;
    sendError();
  }
}
// ---------- Botones: inicialización y lectura ----------

void initializeButtons() {
  unsigned long now = millis();

  for (uint8_t i = 0; i < BUTTON_COUNT; i++) {
    pinMode(BUTTONS[i].pin, INPUT_PULLUP);

    // Se toma el estado real del pin como punto de partida: si el botón ya
    // está pulsado al arrancar, stableState queda en LOW desde el inicio y
    // no se dispara ninguna pulsación artificial. Hace falta soltarlo
    // (LOW -> HIGH) antes de que una nueva pulsación (HIGH -> LOW) cuente.
    int initialReading = digitalRead(BUTTONS[i].pin);
    buttonStates[i].lastRawReading = initialReading;
    buttonStates[i].stableState = initialReading;
    buttonStates[i].lastChangeTime = now;
  }
}

void updateButtons() {
  unsigned long now = millis();

  for (uint8_t i = 0; i < BUTTON_COUNT; i++) {
    ButtonState &state = buttonStates[i];
    int reading = digitalRead(BUTTONS[i].pin);

    if (reading != state.lastRawReading) {
      state.lastRawReading = reading;
      state.lastChangeTime = now;
    }

    if ((now - state.lastChangeTime) >= DEBOUNCE_MS && reading != state.stableState) {
      int previousStable = state.stableState;
      state.stableState = reading;

      if (previousStable == HIGH && state.stableState == LOW) {
        onButtonPressed(BUTTONS[i].position);
      }
      // LOW -> HIGH: liberación estable. No emite evento; solo habilita la próxima pulsación.
    }
  }
}

void onButtonPressed(uint8_t position) {
  if (deviceReady) {
    sendButton(position);
  }
  // Si el handshake todavía no se completó, la pulsación simplemente no se reporta.
}

// ---------- Receptor serie: acumulación y framing ----------

void pollSerial() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();

    if (c == '\n') {
      if (!overflowing) {
        // Si el mensaje terminaba en CRLF, el '\r' quedó como último byte
        // acumulado: se descuenta antes de procesar (no forma parte del
        // contenido). Cualquier otro '\r' que no esté inmediatamente antes
        // de este '\n' ya quedó almacenado como contenido normal.
        uint8_t contentLength = messageLength;

        if (contentLength > 0 && messageBuffer[contentLength - 1] == '\r') {
          contentLength--;
        }

        if (contentLength <= MAX_MESSAGE_LENGTH) {
          messageBuffer[contentLength] = '\0';
          processMessage(messageBuffer, contentLength);
        }
        // Si el contenido real (ya sin el CR final, si lo había) sigue
        // superando el máximo, se descarta la línea sin procesarla.
      }

      messageLength = 0;
      overflowing = false;
      continue;
    }

    if (overflowing) {
      continue; // Se sigue descartando hasta encontrar el próximo LF.
    }

    if (messageLength >= BUFFER_CAPACITY) {
      overflowing = true;
      continue;
    }

    messageBuffer[messageLength++] = c;
  }
}

// ---------- Procesamiento de comandos ----------

void processMessage(char *message, uint8_t length) {
  if (length == 0) {
    return; // Mensaje vacío: se ignora.
  }

  char *command = strtok(message, " ");

  if (command == nullptr) {
    return;
  }

  if (strcmp(command, "HELLO") == 0) {
    char *versionToken = strtok(nullptr, " ");
    handleHello(versionToken);
    return;
  }

  // Comando desconocido: se ignora sin bloquear el firmware.
}

// ---------- Envío de mensajes ----------
//
// Se arma cada línea con Serial.print() y se cierra con Serial.write('\n')
// en vez de Serial.println(), porque println() en Arduino termina las
// líneas con CR+LF y el protocolo exige que el emisor use únicamente LF.

void sendReady() {
  Serial.print("READY ");
  Serial.print(PROTOCOL_VERSION);
  Serial.print(' ');
  Serial.print(DEVICE_MODEL);
  Serial.print(' ');
  Serial.print(BUTTON_COUNT);
  Serial.write('\n');
}

void sendError() {
  Serial.print("ERROR UNSUPPORTED_VERSION");
  Serial.write('\n');
}

void sendButton(uint8_t position) {
  Serial.print("BUTTON ");
  Serial.print(position);
  Serial.write('\n');
}