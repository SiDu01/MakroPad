#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

// First prototype pinout from docs/HARDWARE.md.
constexpr uint8_t Button1Pin = 4;
constexpr uint8_t Slider1Pin = 1;
constexpr uint8_t OledSdaPin = 8;
constexpr uint8_t OledSclPin = 9;

constexpr uint32_t SerialBaud = 115200;
constexpr uint32_t HelloIntervalMs = 1000;
constexpr uint32_t DebounceMs = 25;
constexpr int SliderDeadband = 16;
constexpr size_t MaxLineLength = 256;

constexpr int ScreenWidth = 128;
constexpr int ScreenHeight = 64;
constexpr int OledResetPin = -1;

Adafruit_SSD1306 display(ScreenWidth, ScreenHeight, &Wire, OledResetPin);

struct ButtonState {
  uint8_t id;
  uint8_t pin;
  bool stablePressed;
  bool lastReadingPressed;
  uint32_t lastChangeMs;
};

struct SliderState {
  uint8_t id;
  uint8_t pin;
  int smoothedValue;
  int lastSentValue;
};

ButtonState button1 = {1, Button1Pin, false, false, 0};
SliderState slider1 = {1, Slider1Pin, -1, -1};

String serialLine;
bool hostAcknowledged = false;
bool displayAvailable = false;
uint32_t lastHelloMs = 0;

String percentDecode(const String& value) {
  String decoded;

  for (size_t i = 0; i < value.length(); i++) {
    if (value[i] == '%' && i + 2 < value.length()) {
      const String hex = value.substring(i + 1, i + 3);
      char* end = nullptr;
      const long code = strtol(hex.c_str(), &end, 16);

      if (end != hex.c_str() && *end == '\0') {
        decoded += static_cast<char>(code);
        i += 2;
        continue;
      }
    }

    decoded += value[i];
  }

  return decoded;
}

String protocolValue(const String& line, const char* key) {
  const String prefix = String(key) + "=";
  int start = 0;

  while (start < static_cast<int>(line.length())) {
    int end = line.indexOf(';', start);
    if (end < 0) {
      end = line.length();
    }

    const String segment = line.substring(start, end);
    if (segment.startsWith(prefix)) {
      return percentDecode(segment.substring(prefix.length()));
    }

    start = end + 1;
  }

  return "";
}

String protocolType(const String& line) {
  const int separator = line.indexOf(';');
  if (separator < 0) {
    return line;
  }

  return line.substring(0, separator);
}

void sendLine(const String& line) {
  Serial.println(line);
}

void sendHello() {
  sendLine("hello;proto=1;fw=0.1.0;device=makropad-s3;buttons=1;sliders=1;display=ssd1306-128x64");
}

void sendReady() {
  sendLine("ready;uptime=" + String(millis()));
}

void sendError(const char* code, const char* message) {
  sendLine("error;code=" + String(code) + ";message=" + String(message));
}

void drawStatusMessage(const String& line1, const String& line2 = "", const String& line3 = "") {
  if (!displayAvailable) {
    return;
  }

  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(0, 0);
  display.println(line1);

  if (line2.length() > 0) {
    display.println(line2);
  }

  if (line3.length() > 0) {
    display.println(line3);
  }

  display.display();
}

void drawMedia(const String& artist, const String& title) {
  if (!displayAvailable) {
    return;
  }

  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(0, 0);
  display.println("Media");
  display.println();
  display.println(artist);
  display.println(title);
  display.display();
}

void handleDisplayCommand(const String& line) {
  const String mode = protocolValue(line, "mode");

  if (mode == "clear") {
    if (displayAvailable) {
      display.clearDisplay();
      display.display();
    }
    return;
  }

  if (mode == "profile") {
    drawStatusMessage("Profile", protocolValue(line, "name"));
    return;
  }

  if (mode == "media") {
    drawMedia(protocolValue(line, "artist"), protocolValue(line, "title"));
    return;
  }

  if (mode == "message") {
    drawStatusMessage(protocolValue(line, "text"));
    return;
  }
}

void handleProtocolLine(String line) {
  line.trim();
  if (line.length() == 0) {
    return;
  }

  const String type = protocolType(line);

  if (type == "hello-ack") {
    if (protocolValue(line, "proto") == "1") {
      hostAcknowledged = true;
      drawStatusMessage("MakroPad", "Connected");
    }
    return;
  }

  if (type == "ping") {
    const String id = protocolValue(line, "id");
    if (id.length() > 0) {
      sendLine("pong;id=" + id);
    } else {
      sendLine("pong");
    }
    return;
  }

  if (type == "display") {
    handleDisplayCommand(line);
    return;
  }

  if (type == "config") {
    const String profile = protocolValue(line, "profile");
    if (profile.length() > 0) {
      drawStatusMessage("Profile", profile);
    }
    return;
  }

  if (type == "action-result") {
    const String status = protocolValue(line, "status");
    const String message = protocolValue(line, "message");
    drawStatusMessage(status == "failed" ? "Action failed" : "Action", message);
    return;
  }
}

void readSerialInput() {
  while (Serial.available() > 0) {
    const char c = static_cast<char>(Serial.read());

    if (c == '\n') {
      handleProtocolLine(serialLine);
      serialLine = "";
      continue;
    }

    if (c == '\r') {
      continue;
    }

    if (serialLine.length() < MaxLineLength) {
      serialLine += c;
    } else {
      serialLine = "";
      sendError("line_too_long", "Max%20line%20length%20exceeded");
    }
  }
}

void pollButton(ButtonState& button) {
  const bool pressed = digitalRead(button.pin) == LOW;
  const uint32_t now = millis();

  if (pressed != button.lastReadingPressed) {
    button.lastReadingPressed = pressed;
    button.lastChangeMs = now;
  }

  if ((now - button.lastChangeMs) < DebounceMs) {
    return;
  }

  if (pressed == button.stablePressed) {
    return;
  }

  button.stablePressed = pressed;
  sendLine("button;id=" + String(button.id) + ";state=" + String(pressed ? "down" : "up"));
}

void pollSlider(SliderState& slider) {
  const int raw = analogRead(slider.pin);

  if (slider.smoothedValue < 0) {
    slider.smoothedValue = raw;
  } else {
    slider.smoothedValue = ((slider.smoothedValue * 7) + raw) / 8;
  }

  if (slider.lastSentValue >= 0 && abs(slider.smoothedValue - slider.lastSentValue) < SliderDeadband) {
    return;
  }

  slider.lastSentValue = slider.smoothedValue;
  sendLine("slider;id=" + String(slider.id) + ";value=" + String(slider.smoothedValue) + ";raw=" + String(raw));
}

void initDisplay() {
  Wire.begin(OledSdaPin, OledSclPin);

  displayAvailable = display.begin(SSD1306_SWITCHCAPVCC, 0x3C);
  if (!displayAvailable) {
    displayAvailable = display.begin(SSD1306_SWITCHCAPVCC, 0x3D);
  }

  if (displayAvailable) {
    drawStatusMessage("MakroPad", "Booting...");
  } else {
    sendError("display_init_failed", "OLED%20not%20found");
  }
}

void setup() {
  Serial.begin(SerialBaud);

  pinMode(button1.pin, INPUT_PULLUP);
  analogReadResolution(12);

  initDisplay();
  sendHello();
  sendReady();
}

void loop() {
  readSerialInput();

  const uint32_t now = millis();
  if (!hostAcknowledged && now - lastHelloMs >= HelloIntervalMs) {
    lastHelloMs = now;
    sendHello();
  }

  pollButton(button1);
  pollSlider(slider1);

  delay(5);
}
