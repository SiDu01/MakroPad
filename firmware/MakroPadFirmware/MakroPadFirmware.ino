#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

// Pinout from docs/HARDWARE.md.
constexpr uint8_t ButtonPins[] = {
  4, 5, 6, 7, 10,
  11, 12, 13, 14, 15,
  16, 17, 18, 21, 39,
  40, 41, 42, 47, 48
};

constexpr uint8_t SliderPins[] = {
  1, 2, 3
};

constexpr uint8_t OledSdaPin = 8;
constexpr uint8_t OledSclPin = 9;

constexpr uint32_t SerialBaud = 115200;
constexpr uint32_t HelloIntervalMs = 1000;
constexpr uint32_t HostTimeoutMs = 6000;
constexpr uint32_t DefaultActionResultTimeoutMs = 3000;
constexpr uint32_t DebounceMs = 25;
constexpr int SliderDeadband = 16;
constexpr size_t MaxLineLength = 512;
constexpr size_t ActiveButtonCount = sizeof(ButtonPins) / sizeof(ButtonPins[0]);
constexpr size_t ActiveSliderCount = 1;

static_assert(ActiveSliderCount <= sizeof(SliderPins) / sizeof(SliderPins[0]), "Too many active sliders configured");

constexpr int ScreenWidth = 128;
constexpr int ScreenHeight = 64;
constexpr int OledResetPin = -1;
constexpr int MaxTextSizeOneCharacters = 21;
constexpr int MaxDisplayLines = 5;
constexpr int DisplayLineHeight = 10;
constexpr int DisplayScrollGapCharacters = 3;
constexpr uint32_t DisplayScrollStepMs = 220;
constexpr uint32_t DisplayScrollPauseMs = 900;

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

ButtonState buttons[ActiveButtonCount];
SliderState sliders[ActiveSliderCount];

String usbSerialLine;
String uartSerialLine;
bool hostAcknowledged = false;
bool displayAvailable = false;
uint32_t lastHelloMs = 0;
uint32_t lastHostSeenMs = 0;
uint32_t actionResultTimeoutMs = DefaultActionResultTimeoutMs;
uint32_t temporaryDisplayUntilMs = 0;
String currentProfileName = "Default";
String preferredDisplayMode = "profile";
String currentMediaArtist;
String currentMediaTitle;
uint8_t defaultScreenRows = 3;
String defaultScreenTitle = "MakroPad";
String defaultScreenLine1 = "{profile}";
String defaultScreenLine2 = "{status}";
String defaultScreenLine3;
String defaultScreenLine4;
String currentTime;
String currentDate;
String currentSystemVolume;
String activeDisplayLines[MaxDisplayLines];
uint8_t activeDisplayLineSizes[MaxDisplayLines];
uint8_t activeDisplayLineCount = 0;
uint16_t activeDisplayScrollStep = 0;
uint32_t lastDisplayScrollMs = 0;
bool activeDisplayScrollNeeded = false;

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
  Serial0.println(line);
}

void sendHello() {
  String line = "hello;proto=1;fw=0.2.0;device=makropad-s3;buttons=";
  line += String(ActiveButtonCount);
  line += ";sliders=";
  line += String(ActiveSliderCount);
  line += ";display=ssd1306-128x64";
  sendLine(line);
}

void sendReady() {
  sendLine("ready;uptime=" + String(millis()));
}

void sendError(const char* code, const char* message) {
  sendLine("error;code=" + String(code) + ";message=" + String(message));
}

String renderDisplayLine(const String& line, uint8_t textSize);
void renderActiveDisplayLines();
void setDisplayLines(const String lines[], uint8_t count);
void setDisplayLines(const String lines[], const uint8_t lineSizes[], uint8_t count);

void drawStatusMessage(
  const String& line1,
  const String& line2 = "",
  const String& line3 = "",
  const String& line4 = "",
  const String& line5 = "") {
  String lines[MaxDisplayLines];
  uint8_t count = 0;

  lines[count++] = line1;
  if (line2.length() > 0) {
    lines[count++] = line2;
  }

  if (line3.length() > 0) {
    lines[count++] = line3;
  }

  if (line4.length() > 0) {
    lines[count++] = line4;
  }

  if (line5.length() > 0) {
    lines[count++] = line5;
  }

  setDisplayLines(lines, count);
}

void drawMedia(const String& artist, const String& title);

uint8_t normalizeDisplayTextSize(int textSize) {
  return textSize >= 2 ? 2 : 1;
}

int visibleCharactersForTextSize(uint8_t textSize) {
  return max(1, MaxTextSizeOneCharacters / normalizeDisplayTextSize(textSize));
}

bool displayLinesChanged(const String lines[], const uint8_t lineSizes[], uint8_t count) {
  if (activeDisplayLineCount != count) {
    return true;
  }

  for (uint8_t index = 0; index < count; index++) {
    if (activeDisplayLines[index] != lines[index]
        || activeDisplayLineSizes[index] != normalizeDisplayTextSize(lineSizes[index])) {
      return true;
    }
  }

  return false;
}

bool displayNeedsScroll(const String lines[], const uint8_t lineSizes[], uint8_t count) {
  for (uint8_t index = 0; index < count; index++) {
    if (lines[index].length() > visibleCharactersForTextSize(lineSizes[index])) {
      return true;
    }
  }

  return false;
}

uint16_t displayScrollCycleLength() {
  uint16_t longest = 0;

  for (uint8_t index = 0; index < activeDisplayLineCount; index++) {
    if (activeDisplayLines[index].length() > longest) {
      longest = activeDisplayLines[index].length();
    }
  }

  return longest > MaxTextSizeOneCharacters
    ? longest + DisplayScrollGapCharacters
    : 0;
}

String renderDisplayLine(const String& line, uint8_t textSize) {
  const int visibleCharacters = visibleCharactersForTextSize(textSize);
  if (line.length() <= visibleCharacters) {
    return line;
  }

  const uint16_t cycleLength = line.length() + DisplayScrollGapCharacters;
  const uint16_t start = activeDisplayScrollStep % cycleLength;
  String visible;
  visible.reserve(visibleCharacters);

  for (int offset = 0; offset < visibleCharacters; offset++) {
    const uint16_t sourceIndex = (start + offset) % cycleLength;
    visible += sourceIndex < line.length()
      ? line[sourceIndex]
      : ' ';
  }

  return visible;
}

void renderActiveDisplayLines() {
  if (!displayAvailable) {
    return;
  }

  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);

  int y = 0;
  for (uint8_t index = 0; index < activeDisplayLineCount && index < MaxDisplayLines; index++) {
    const uint8_t textSize = normalizeDisplayTextSize(activeDisplayLineSizes[index]);
    if (y >= ScreenHeight) {
      break;
    }

    display.setTextSize(textSize);
    display.setCursor(0, y);
    display.println(renderDisplayLine(activeDisplayLines[index], textSize));
    y += DisplayLineHeight * textSize;
  }

  display.display();
}

void setDisplayLines(const String lines[], uint8_t count) {
  uint8_t lineSizes[MaxDisplayLines];
  for (uint8_t index = 0; index < MaxDisplayLines; index++) {
    lineSizes[index] = 1;
  }

  setDisplayLines(lines, lineSizes, count);
}

void setDisplayLines(const String lines[], const uint8_t lineSizes[], uint8_t count) {
  const uint8_t cappedCount = min(count, static_cast<uint8_t>(MaxDisplayLines));
  const bool changed = displayLinesChanged(lines, lineSizes, cappedCount);

  for (uint8_t index = 0; index < cappedCount; index++) {
    activeDisplayLines[index] = lines[index];
    activeDisplayLineSizes[index] = normalizeDisplayTextSize(lineSizes[index]);
  }

  for (uint8_t index = cappedCount; index < MaxDisplayLines; index++) {
    activeDisplayLines[index] = "";
    activeDisplayLineSizes[index] = 1;
  }

  activeDisplayLineCount = cappedCount;
  activeDisplayScrollNeeded = displayNeedsScroll(
    activeDisplayLines,
    activeDisplayLineSizes,
    activeDisplayLineCount);

  if (changed) {
    activeDisplayScrollStep = 0;
    lastDisplayScrollMs = millis();
  }

  renderActiveDisplayLines();
}

String formatUptime() {
  const uint32_t totalSeconds = millis() / 1000;
  const uint32_t hours = totalSeconds / 3600;
  const uint32_t minutes = (totalSeconds % 3600) / 60;
  const uint32_t seconds = totalSeconds % 60;

  char buffer[16];
  snprintf(buffer, sizeof(buffer), "%02lu:%02lu:%02lu",
    static_cast<unsigned long>(hours),
    static_cast<unsigned long>(minutes),
    static_cast<unsigned long>(seconds));
  return String(buffer);
}

String renderDefaultScreenLine(String line, const String& status) {
  const String mediaTrack = currentMediaArtist.length() > 0 && currentMediaTitle.length() > 0
    ? currentMediaArtist + " - " + currentMediaTitle
    : currentMediaTitle;

  line.replace("{profile}", currentProfileName);
  line.replace("{status}", status);
  line.replace("{time}", currentTime);
  line.replace("{date}", currentDate);
  line.replace("{media.artist}", currentMediaArtist);
  line.replace("{media.title}", currentMediaTitle);
  line.replace("{media.track}", mediaTrack);
  line.replace("{volume.system}", currentSystemVolume);
  line.replace("{device.uptime}", formatUptime());
  return line;
}

void drawIdleStatus() {
  const String status = hostAcknowledged ? "Connected" : "Waiting for app";
  if (defaultScreenRows == 5) {
    drawStatusMessage(
      renderDefaultScreenLine(defaultScreenTitle, status),
      renderDefaultScreenLine(defaultScreenLine1, status),
      renderDefaultScreenLine(defaultScreenLine2, status),
      renderDefaultScreenLine(defaultScreenLine3, status),
      renderDefaultScreenLine(defaultScreenLine4, status));
    return;
  }

  drawStatusMessage(
    renderDefaultScreenLine(defaultScreenTitle, status),
    renderDefaultScreenLine(defaultScreenLine1, status),
    renderDefaultScreenLine(defaultScreenLine2, status));
}

void drawPreferredDisplay() {
  if (preferredDisplayMode == "media" && currentMediaTitle.length() > 0) {
    drawMedia(currentMediaArtist, currentMediaTitle);
    return;
  }

  drawIdleStatus();
}

void markHostSeen() {
  const bool wasAcknowledged = hostAcknowledged;
  hostAcknowledged = true;
  lastHostSeenMs = millis();

  if (!wasAcknowledged) {
    temporaryDisplayUntilMs = 0;
    drawPreferredDisplay();
  }
}

void drawMedia(const String& artist, const String& title) {
  String lines[MaxDisplayLines] = {
    "Media",
    "",
    artist,
    title,
    ""
  };
  setDisplayLines(lines, 4);
}

void handleDisplayCommand(const String& line) {
  const String mode = protocolValue(line, "mode");
  temporaryDisplayUntilMs = 0;

  if (mode == "clear") {
    activeDisplayLineCount = 0;
    activeDisplayScrollStep = 0;
    activeDisplayScrollNeeded = false;
    for (uint8_t index = 0; index < MaxDisplayLines; index++) {
      activeDisplayLines[index] = "";
      activeDisplayLineSizes[index] = 1;
    }

    if (displayAvailable) {
      display.clearDisplay();
      display.display();
    }
    return;
  }

  if (mode == "profile") {
    const String profile = protocolValue(line, "name");
    if (profile.length() > 0) {
      currentProfileName = profile;
    }
    preferredDisplayMode = "profile";
    drawPreferredDisplay();
    return;
  }

  if (mode == "media") {
    currentMediaArtist = protocolValue(line, "artist");
    currentMediaTitle = protocolValue(line, "title");
    if (protocolValue(line, "preferred") != "false") {
      preferredDisplayMode = "media";
    }
    drawPreferredDisplay();
    return;
  }

  if (mode == "message") {
    drawStatusMessage(protocolValue(line, "text"));
    return;
  }

  if (mode == "template") {
    String lines[MaxDisplayLines];
    uint8_t lineSizes[MaxDisplayLines];
    uint8_t count = 0;

    for (uint8_t index = 0; index < MaxDisplayLines; index++) {
      const String key = "line" + String(index + 1);
      const String value = protocolValue(line, key.c_str());
      if (index == 0 || value.length() > 0) {
        lines[count] = value;
        lineSizes[count] = normalizeDisplayTextSize(
          protocolValue(line, (key + "Size").c_str()).toInt());
        count++;
      }
    }

    setDisplayLines(lines, lineSizes, count);

    const String duration = protocolValue(line, "durationMs");
    temporaryDisplayUntilMs = duration.length() > 0 && duration.toInt() > 0
      ? millis() + static_cast<uint32_t>(duration.toInt())
      : 0;
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
      markHostSeen();
    }
    return;
  }

  if (type == "ping") {
    markHostSeen();
    const String id = protocolValue(line, "id");
    if (id.length() > 0) {
      sendLine("pong;id=" + id);
    } else {
      sendLine("pong");
    }
    return;
  }

  if (type == "display") {
    markHostSeen();
    handleDisplayCommand(line);
    return;
  }

  if (type == "config") {
    markHostSeen();
    const String profile = protocolValue(line, "profile");
    if (profile.length() > 0) {
      currentProfileName = profile;
    }

    const String timeout = protocolValue(line, "actionResultTimeoutMs");
    if (timeout.length() > 0) {
      actionResultTimeoutMs = static_cast<uint32_t>(max(0L, timeout.toInt()));
    }

    const String displayMode = protocolValue(line, "preferredDisplay");
    if (displayMode == "media" || displayMode == "profile") {
      preferredDisplayMode = displayMode;
    }

    const String configuredDefaultRows = protocolValue(line, "defaultRows");
    if (configuredDefaultRows == "5") {
      defaultScreenRows = 5;
    } else if (configuredDefaultRows == "3") {
      defaultScreenRows = 3;
    }

    const String configuredDefaultTitle = protocolValue(line, "defaultTitle");
    if (configuredDefaultTitle.length() > 0) {
      defaultScreenTitle = configuredDefaultTitle;
    }

    const String configuredDefaultLine1 = protocolValue(line, "defaultLine1");
    if (configuredDefaultLine1.length() > 0) {
      defaultScreenLine1 = configuredDefaultLine1;
    }

    const String configuredDefaultLine2 = protocolValue(line, "defaultLine2");
    if (configuredDefaultLine2.length() > 0) {
      defaultScreenLine2 = configuredDefaultLine2;
    }

    defaultScreenLine3 = protocolValue(line, "defaultLine3");
    defaultScreenLine4 = protocolValue(line, "defaultLine4");

    currentTime = protocolValue(line, "time");
    currentDate = protocolValue(line, "date");
    currentSystemVolume = protocolValue(line, "volumeSystem");

    if (temporaryDisplayUntilMs == 0) {
      drawPreferredDisplay();
    }
    return;
  }

  if (type == "action-result") {
    markHostSeen();
    const String status = protocolValue(line, "status");
    const String message = protocolValue(line, "message");
    drawStatusMessage(status == "failed" ? "Action failed" : "Action", message);
    temporaryDisplayUntilMs = actionResultTimeoutMs > 0
      ? millis() + actionResultTimeoutMs
      : 0;
    return;
  }
}

void readSerialInputFrom(Stream& port, String& lineBuffer) {
  while (port.available() > 0) {
    const char c = static_cast<char>(port.read());

    if (c == '\n') {
      handleProtocolLine(lineBuffer);
      lineBuffer = "";
      continue;
    }

    if (c == '\r') {
      continue;
    }

    if (lineBuffer.length() < MaxLineLength) {
      lineBuffer += c;
    } else {
      lineBuffer = "";
      sendError("line_too_long", "Max%20line%20length%20exceeded");
    }
  }
}

void readSerialInput() {
  readSerialInputFrom(Serial, usbSerialLine);
  readSerialInputFrom(Serial0, uartSerialLine);
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

void initButtons() {
  for (size_t index = 0; index < ActiveButtonCount; index++) {
    buttons[index] = {
      static_cast<uint8_t>(index + 1),
      ButtonPins[index],
      false,
      false,
      0
    };

    pinMode(buttons[index].pin, INPUT_PULLUP);
  }
}

void initSliders() {
  for (size_t index = 0; index < ActiveSliderCount; index++) {
    sliders[index] = {
      static_cast<uint8_t>(index + 1),
      SliderPins[index],
      -1,
      -1
    };
  }
}

void updateDisplayTimeout() {
  if (temporaryDisplayUntilMs == 0) {
    return;
  }

  if (static_cast<int32_t>(millis() - temporaryDisplayUntilMs) >= 0) {
    temporaryDisplayUntilMs = 0;
    drawPreferredDisplay();
  }
}

void updateDisplayScroll() {
  if (!activeDisplayScrollNeeded) {
    return;
  }

  const uint32_t now = millis();
  const uint32_t interval = activeDisplayScrollStep == 0
    ? DisplayScrollPauseMs
    : DisplayScrollStepMs;

  if (static_cast<uint32_t>(now - lastDisplayScrollMs) < interval) {
    return;
  }

  const uint16_t cycleLength = displayScrollCycleLength();
  if (cycleLength == 0) {
    activeDisplayScrollNeeded = false;
    return;
  }

  activeDisplayScrollStep++;
  if (activeDisplayScrollStep >= cycleLength) {
    activeDisplayScrollStep = 0;
  }

  lastDisplayScrollMs = now;
  renderActiveDisplayLines();
}

void updateHostTimeout() {
  if (!hostAcknowledged) {
    return;
  }

  if (static_cast<uint32_t>(millis() - lastHostSeenMs) >= HostTimeoutMs) {
    hostAcknowledged = false;
    temporaryDisplayUntilMs = 0;
    drawPreferredDisplay();
  }
}

void setup() {
  Serial.begin(SerialBaud);
  Serial0.begin(SerialBaud);

  initButtons();
  initSliders();
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

  updateHostTimeout();
  updateDisplayTimeout();
  updateDisplayScroll();

  for (size_t index = 0; index < ActiveButtonCount; index++) {
    pollButton(buttons[index]);
  }

  for (size_t index = 0; index < ActiveSliderCount; index++) {
    pollSlider(sliders[index]);
  }

  delay(5);
}
