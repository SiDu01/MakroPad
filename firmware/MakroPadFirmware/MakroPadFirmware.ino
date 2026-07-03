#include <Arduino.h>
#include <USB.h>
#include <USBHIDConsumerControl.h>
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
constexpr int SliderAdcMax = 4095;
constexpr int SliderDeadband = 32;
constexpr int SliderSnapThreshold = 24;
constexpr uint8_t SliderSampleCount = 9;
constexpr uint8_t SliderKeptSampleCount = 5;
constexpr uint8_t SliderSmoothingDivisor = 16;
constexpr uint32_t SliderMinSendIntervalMs = 35;
constexpr uint32_t HidFallbackStartupGraceMs = 2500;
constexpr uint32_t HidFallbackPressMs = 30;
constexpr uint32_t HidFallbackVolumeIntervalMs = 120;
constexpr uint8_t HidFallbackVolumeNudgeThresholdPercent = 4;
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
constexpr int CharacterWidth = 6;
constexpr int DisplayScrollGapCharacters = 3;
constexpr uint32_t DisplayScrollStepMs = 220;
constexpr uint32_t DisplayScrollPauseMs = 900;

Adafruit_SSD1306 display(ScreenWidth, ScreenHeight, &Wire, OledResetPin);
USBHIDConsumerControl hidConsumerControl;

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
  uint32_t lastSentMs;
};

ButtonState buttons[ActiveButtonCount];
SliderState sliders[ActiveSliderCount];
int hidFallbackSliderPercents[ActiveSliderCount];
uint32_t hidFallbackSliderLastSendMs[ActiveSliderCount];

String usbSerialLine;
String uartSerialLine;
bool hostAcknowledged = false;
bool hidFallbackWasActive = false;
bool displayAvailable = false;
uint32_t lastHelloMs = 0;
uint32_t lastHostSeenMs = 0;
uint32_t actionResultTimeoutMs = DefaultActionResultTimeoutMs;
uint32_t temporaryDisplayUntilMs = 0;
String currentProfileName = "Default";
String preferredDisplayMode = "profile";
String currentMediaArtist;
String currentMediaTitle;
uint8_t defaultScreenRows = 5;
String defaultScreenTitle = "{status}";
uint8_t defaultScreenTitleSize = 1;
String defaultScreenLine1 = "";
uint8_t defaultScreenLine1Size = 1;
String defaultScreenLine2 = "{date}";
uint8_t defaultScreenLine2Size = 1;
String defaultScreenLine3 = "{time}";
uint8_t defaultScreenLine3Size = 2;
String defaultScreenLine4 = "";
uint8_t defaultScreenLine4Size = 1;
String defaultScreenTitleAlign = "center";
String defaultScreenLine1Align = "left";
String defaultScreenLine2Align = "left";
String defaultScreenLine3Align = "center";
String defaultScreenLine4Align = "left";
String currentTime;
String currentDate;

// Running clock so {time}/{date} keep advancing while the host app is
// disconnected (the board has no RTC; time/date arrive via the config
// message). We remember the last host-provided time and roll it forward
// with millis().
bool clockBaseValid = false;
uint32_t clockBaseMillis = 0;
long clockBaseSecondsOfDay = 0;
int clockBaseDay = 1;
int clockBaseMonth = 1;
int clockBaseYear = 2000;
uint32_t lastClockTickMs = 0;
String currentSystemVolume;
String activeDisplayLines[MaxDisplayLines];
uint8_t activeDisplayLineSizes[MaxDisplayLines];
String activeDisplayLineAligns[MaxDisplayLines];
uint8_t activeDisplayLineCount = 0;
uint16_t activeDisplayScrollStep = 0;
uint32_t lastDisplayScrollMs = 0;
bool activeDisplayScrollNeeded = false;
bool activeLayoutScreen = false;
String activeLayoutMode;
String activeLayoutIcon;
int activeLayoutValue = 0;

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
void setDisplayLines(const String lines[], const uint8_t lineSizes[], const String lineAligns[], uint8_t count);
void drawLayoutScreen(
  const String& layout,
  const String& title,
  const String& line1,
  const String& line2,
  int value,
  const String& icon);
void renderActiveLayoutScreen();

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

String normalizeDisplayAlign(String align) {
  align.toLowerCase();
  if (align == "center" || align == "right") {
    return align;
  }
  return "left";
}

int displayAlignOffsetX(const String& rendered, uint8_t textSize, const String& align) {
  if (align != "center" && align != "right") {
    return 0;
  }

  const int width = rendered.length() * CharacterWidth * normalizeDisplayTextSize(textSize);
  if (width >= ScreenWidth) {
    return 0;
  }

  return align == "center" ? (ScreenWidth - width) / 2 : (ScreenWidth - width);
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
  uint16_t longestCycle = 0;

  for (uint8_t index = 0; index < activeDisplayLineCount; index++) {
    const uint16_t lineLength = activeDisplayLines[index].length();
    if (lineLength > visibleCharactersForTextSize(activeDisplayLineSizes[index])) {
      const uint16_t cycleLength = lineLength + DisplayScrollGapCharacters;
      if (cycleLength > longestCycle) {
        longestCycle = cycleLength;
      }
    }
  }

  return longestCycle;
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

    const String rendered = renderDisplayLine(activeDisplayLines[index], textSize);
    const int x = displayAlignOffsetX(rendered, textSize, activeDisplayLineAligns[index]);
    display.setTextSize(textSize);
    display.setCursor(x, y);
    display.println(rendered);
    y += DisplayLineHeight * textSize;
  }

  display.display();
}

void clearActiveDisplayLineState() {
  activeLayoutScreen = false;
  activeLayoutMode = "";
  activeLayoutIcon = "";
  activeLayoutValue = 0;
  activeDisplayLineCount = 0;
  activeDisplayScrollStep = 0;
  activeDisplayScrollNeeded = false;
  for (uint8_t index = 0; index < MaxDisplayLines; index++) {
    activeDisplayLines[index] = "";
    activeDisplayLineSizes[index] = 1;
    activeDisplayLineAligns[index] = "left";
  }
}

String clippedText(const String& text, uint8_t textSize) {
  const int maxCharacters = visibleCharactersForTextSize(textSize);
  return text.length() <= maxCharacters
    ? text
    : text.substring(0, maxCharacters);
}

void drawHeader(const String& title) {
  display.fillRect(0, 0, ScreenWidth, 13, SSD1306_WHITE);
  display.setTextSize(1);
  display.setTextColor(SSD1306_BLACK);
  display.setCursor(2, 2);
  display.print(title);
  display.setTextColor(SSD1306_WHITE);
}

void drawTextAt(int x, int y, const String& text, uint8_t textSize = 1, bool clip = true) {
  display.setTextSize(textSize);
  display.setCursor(x, y);
  display.print(clip ? clippedText(text, textSize) : text);
}

void drawIcon(const String& icon, int x, int y) {
  if (icon == "check") {
    display.drawCircle(x + 9, y + 9, 8, SSD1306_WHITE);
    display.drawLine(x + 5, y + 9, x + 8, y + 12, SSD1306_WHITE);
    display.drawLine(x + 8, y + 12, x + 14, y + 5, SSD1306_WHITE);
  } else if (icon == "warn") {
    display.drawTriangle(x + 9, y + 1, x + 1, y + 17, x + 17, y + 17, SSD1306_WHITE);
    display.drawLine(x + 9, y + 6, x + 9, y + 11, SSD1306_WHITE);
    display.drawPixel(x + 9, y + 14, SSD1306_WHITE);
  } else if (icon == "x") {
    display.drawRect(x + 1, y + 1, 17, 17, SSD1306_WHITE);
    display.drawLine(x + 5, y + 5, x + 13, y + 13, SSD1306_WHITE);
    display.drawLine(x + 13, y + 5, x + 5, y + 13, SSD1306_WHITE);
  } else if (icon == "sync") {
    display.drawCircle(x + 9, y + 9, 7, SSD1306_WHITE);
    display.drawLine(x + 13, y + 3, x + 17, y + 3, SSD1306_WHITE);
    display.drawLine(x + 17, y + 3, x + 15, y + 1, SSD1306_WHITE);
    display.drawLine(x + 5, y + 15, x + 1, y + 15, SSD1306_WHITE);
    display.drawLine(x + 1, y + 15, x + 3, y + 17, SSD1306_WHITE);
  }
}

void drawProgressBar(int x, int y, int width, int height, int value) {
  const int clampedValue = constrain(value, 0, 100);
  display.drawRect(x, y, width, height, SSD1306_WHITE);
  const int fillWidth = max(0, (width - 2) * clampedValue / 100);
  if (fillWidth > 0) {
    display.fillRect(x + 1, y + 1, fillWidth, height - 2, SSD1306_WHITE);
  }
}

void drawVolumeLayout(const String& title, const String& line1, int value) {
  drawHeader(renderDisplayLine(title.length() > 0 ? title : "Volume", 1));
  drawTextAt(0, 18, renderDisplayLine(line1, 1), 1, false);
  display.setTextSize(2);
  display.setCursor(0, 31);
  display.print(constrain(value, 0, 100));
  display.print("%");
  drawProgressBar(0, 54, ScreenWidth, 10, value);
}

void drawMediaLayout(const String& title, const String& line1, const String& line2) {
  drawHeader(renderDisplayLine(title.length() > 0 ? title : "Now Playing", 1));
  drawTextAt(0, 18, renderDisplayLine(line1, 1), 1, false);
  drawTextAt(0, 32, renderDisplayLine(line2, 1), 1, false);
}

void drawStatusLayout(
  const String& title,
  const String& line1,
  const String& line2,
  const String& icon) {
  drawHeader(renderDisplayLine(title.length() > 0 ? title : "Status", 1));
  drawIcon(icon, 0, 21);
  drawTextAt(icon.length() > 0 ? 24 : 0, 20, renderDisplayLine(line1, 1), 1, false);
  drawTextAt(icon.length() > 0 ? 24 : 0, 34, renderDisplayLine(line2, 1), 1, false);
}

void renderActiveLayoutScreen() {
  if (!displayAvailable) {
    return;
  }

  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);

  const String title = activeDisplayLineCount > 0 ? activeDisplayLines[0] : "";
  const String line1 = activeDisplayLineCount > 1 ? activeDisplayLines[1] : "";
  const String line2 = activeDisplayLineCount > 2 ? activeDisplayLines[2] : "";

  if (activeLayoutMode == "volume") {
    drawVolumeLayout(title, line1, activeLayoutValue);
  } else if (activeLayoutMode == "media") {
    drawMediaLayout(title, line1, line2);
  } else {
    drawStatusLayout(title, line1, line2, activeLayoutIcon);
  }

  display.display();
}

void drawLayoutScreen(
  const String& layout,
  const String& title,
  const String& line1,
  const String& line2,
  int value,
  const String& icon) {
  clearActiveDisplayLineState();
  activeLayoutScreen = true;
  activeLayoutMode = layout;
  activeLayoutIcon = icon;
  activeLayoutValue = constrain(value, 0, 100);
  activeDisplayLines[0] = title;
  activeDisplayLines[1] = line1;
  activeDisplayLines[2] = line2;
  activeDisplayLineSizes[0] = 1;
  activeDisplayLineSizes[1] = 1;
  activeDisplayLineSizes[2] = 1;
  activeDisplayLineCount = 3;
  activeDisplayScrollNeeded = displayNeedsScroll(
    activeDisplayLines,
    activeDisplayLineSizes,
    activeDisplayLineCount);
  activeDisplayScrollStep = 0;
  lastDisplayScrollMs = millis();
  renderActiveLayoutScreen();
}

void setDisplayLines(const String lines[], uint8_t count) {
  uint8_t lineSizes[MaxDisplayLines];
  for (uint8_t index = 0; index < MaxDisplayLines; index++) {
    lineSizes[index] = 1;
  }

  setDisplayLines(lines, lineSizes, count);
}

void setDisplayLines(const String lines[], const uint8_t lineSizes[], uint8_t count) {
  String lineAligns[MaxDisplayLines];
  for (uint8_t index = 0; index < MaxDisplayLines; index++) {
    lineAligns[index] = "left";
  }
  setDisplayLines(lines, lineSizes, lineAligns, count);
}

void setDisplayLines(const String lines[], const uint8_t lineSizes[], const String lineAligns[], uint8_t count) {
  activeLayoutScreen = false;
  activeLayoutMode = "";
  activeLayoutIcon = "";
  activeLayoutValue = 0;
  const uint8_t cappedCount = min(count, static_cast<uint8_t>(MaxDisplayLines));
  const bool changed = displayLinesChanged(lines, lineSizes, cappedCount);

  for (uint8_t index = 0; index < cappedCount; index++) {
    activeDisplayLines[index] = lines[index];
    activeDisplayLineSizes[index] = normalizeDisplayTextSize(lineSizes[index]);
    activeDisplayLineAligns[index] = normalizeDisplayAlign(lineAligns[index]);
  }

  for (uint8_t index = cappedCount; index < MaxDisplayLines; index++) {
    activeDisplayLines[index] = "";
    activeDisplayLineSizes[index] = 1;
    activeDisplayLineAligns[index] = "left";
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

int daysInClockMonth(int month, int year) {
  static const int lengths[] = {31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};
  if (month == 2 && ((year % 4 == 0 && year % 100 != 0) || year % 400 == 0)) {
    return 29;
  }
  return lengths[(month - 1) % 12];
}

// Remembers the last host-provided time/date ("HH:mm" and "dd.MM.yyyy") as the
// baseline the running clock rolls forward from.
void setClockBaseline(const String& time, const String& date) {
  const int colon = time.indexOf(':');
  const int dot1 = date.indexOf('.');
  const int dot2 = dot1 >= 0 ? date.indexOf('.', dot1 + 1) : -1;
  if (colon <= 0 || dot1 <= 0 || dot2 <= dot1) {
    clockBaseValid = false;
    return;
  }

  const int hours = time.substring(0, colon).toInt();
  const int minutes = time.substring(colon + 1).toInt();
  const int day = date.substring(0, dot1).toInt();
  const int month = date.substring(dot1 + 1, dot2).toInt();
  const int year = date.substring(dot2 + 1).toInt();
  if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59
      || day < 1 || day > 31 || month < 1 || month > 12 || year < 2000) {
    clockBaseValid = false;
    return;
  }

  clockBaseSecondsOfDay = static_cast<long>(hours) * 3600L + static_cast<long>(minutes) * 60L;
  clockBaseDay = day;
  clockBaseMonth = month;
  clockBaseYear = year;
  clockBaseMillis = millis();
  clockBaseValid = true;
}

// Rolls the baseline forward by the elapsed millis() and refreshes
// currentTime/currentDate so {time}/{date} keep advancing without the host.
void refreshRunningClock() {
  if (!clockBaseValid) {
    return;
  }

  const long elapsed = static_cast<long>((millis() - clockBaseMillis) / 1000UL);
  const long total = clockBaseSecondsOfDay + elapsed;
  long extraDays = total / 86400L;
  const long secondsOfDay = total % 86400L;

  const int hours = static_cast<int>(secondsOfDay / 3600L);
  const int minutes = static_cast<int>((secondsOfDay % 3600L) / 60L);
  char timeBuffer[6];
  snprintf(timeBuffer, sizeof(timeBuffer), "%02d:%02d", hours, minutes);
  currentTime = timeBuffer;

  int day = clockBaseDay;
  int month = clockBaseMonth;
  int year = clockBaseYear;
  while (extraDays-- > 0) {
    day++;
    if (day > daysInClockMonth(month, year)) {
      day = 1;
      if (++month > 12) {
        month = 1;
        year++;
      }
    }
  }
  char dateBuffer[11];
  snprintf(dateBuffer, sizeof(dateBuffer), "%02d.%02d.%04d", day, month, year);
  currentDate = dateBuffer;
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
  const String status = hostAcknowledged ? "Connected" : "Connection Lost";
  if (defaultScreenRows == 5) {
    String lines[MaxDisplayLines] = {
      renderDefaultScreenLine(defaultScreenTitle, status),
      renderDefaultScreenLine(defaultScreenLine1, status),
      renderDefaultScreenLine(defaultScreenLine2, status),
      renderDefaultScreenLine(defaultScreenLine3, status),
      renderDefaultScreenLine(defaultScreenLine4, status)
    };
    uint8_t lineSizes[MaxDisplayLines] = {
      defaultScreenTitleSize,
      defaultScreenLine1Size,
      defaultScreenLine2Size,
      defaultScreenLine3Size,
      defaultScreenLine4Size
    };
    String lineAligns[MaxDisplayLines] = {
      defaultScreenTitleAlign,
      defaultScreenLine1Align,
      defaultScreenLine2Align,
      defaultScreenLine3Align,
      defaultScreenLine4Align
    };
    setDisplayLines(lines, lineSizes, lineAligns, 5);
    return;
  }

  String lines[MaxDisplayLines] = {
    renderDefaultScreenLine(defaultScreenTitle, status),
    renderDefaultScreenLine(defaultScreenLine1, status),
    renderDefaultScreenLine(defaultScreenLine2, status),
    "",
    ""
  };
  uint8_t lineSizes[MaxDisplayLines] = {
    defaultScreenTitleSize,
    defaultScreenLine1Size,
    defaultScreenLine2Size,
    1,
    1
  };
  String lineAligns[MaxDisplayLines] = {
    defaultScreenTitleAlign,
    defaultScreenLine1Align,
    defaultScreenLine2Align,
    "left",
    "left"
  };
  setDisplayLines(lines, lineSizes, lineAligns, 3);
}

void drawPreferredDisplay() {
  if (preferredDisplayMode == "media" && currentMediaTitle.length() > 0) {
    drawMedia(currentMediaArtist, currentMediaTitle);
    return;
  }

  drawIdleStatus();
}

int mapSliderValueToPercent(int value) {
  const int clamped = constrain(value, 0, SliderAdcMax);
  return static_cast<int>((static_cast<long>(clamped) * 100L + (SliderAdcMax / 2)) / SliderAdcMax);
}

bool isHidFallbackActive() {
  return !hostAcknowledged && millis() >= HidFallbackStartupGraceMs;
}

void resetHidFallbackSliderState() {
  for (size_t index = 0; index < ActiveSliderCount; index++) {
    hidFallbackSliderPercents[index] = sliders[index].smoothedValue >= 0
      ? mapSliderValueToPercent(sliders[index].smoothedValue)
      : -1;
    hidFallbackSliderLastSendMs[index] = 0;
  }
}

void sendHidConsumerControl(uint16_t usage) {
  hidConsumerControl.press(usage);
  delay(HidFallbackPressMs);
  hidConsumerControl.release();
  delay(2);
}

uint16_t hidFallbackButtonUsage(uint8_t buttonId) {
  switch (buttonId) {
    case 1:
      return CONSUMER_CONTROL_PLAY_PAUSE;
    case 2:
      return CONSUMER_CONTROL_SCAN_NEXT;
    case 3:
      return CONSUMER_CONTROL_SCAN_PREVIOUS;
    case 4:
      return CONSUMER_CONTROL_STOP;
    case 5:
      return CONSUMER_CONTROL_MUTE;
    case 6:
      return CONSUMER_CONTROL_VOLUME_DECREMENT;
    case 7:
      return CONSUMER_CONTROL_VOLUME_INCREMENT;
    default:
      return 0;
  }
}

void handleHidFallbackButton(uint8_t buttonId, bool pressed) {
  if (!isHidFallbackActive() || pressed) {
    return;
  }

  const uint16_t usage = hidFallbackButtonUsage(buttonId);
  if (usage == 0) {
    return;
  }

  sendHidConsumerControl(usage);
  sendLine("hid-fallback;input=button:" + String(buttonId) + ";usage=" + String(usage));
  drawStatusMessage("HID Button " + String(buttonId), "Usage " + String(usage));
}

void handleHidFallbackSlider(const SliderState& slider) {
  if (!isHidFallbackActive() || slider.id != 1 || slider.id > ActiveSliderCount) {
    return;
  }

  const int percent = mapSliderValueToPercent(slider.smoothedValue);
  int& lastPercent = hidFallbackSliderPercents[slider.id - 1];
  if (lastPercent < 0) {
    lastPercent = percent;
    return;
  }

  const int delta = percent - lastPercent;
  if (abs(delta) < HidFallbackVolumeNudgeThresholdPercent) {
    return;
  }

  const uint32_t now = millis();
  uint32_t& lastSendMs = hidFallbackSliderLastSendMs[slider.id - 1];
  if (lastSendMs > 0 && now - lastSendMs < HidFallbackVolumeIntervalMs) {
    return;
  }

  const uint16_t usage = delta > 0
    ? CONSUMER_CONTROL_VOLUME_INCREMENT
    : CONSUMER_CONTROL_VOLUME_DECREMENT;

  sendHidConsumerControl(usage);
  lastPercent = percent;
  lastSendMs = now;
  sendLine("hid-fallback;input=slider:" + String(slider.id) + ";percent=" + String(percent) + ";steps=1");
  drawStatusMessage("HID Volume", String(percent) + "%");
}

void updateHidFallbackState() {
  const bool active = isHidFallbackActive();
  if (active == hidFallbackWasActive) {
    return;
  }

  hidFallbackWasActive = active;
  resetHidFallbackSliderState();

  if (active) {
    temporaryDisplayUntilMs = 0;
    drawStatusMessage("HID Fallback", "System volume");
  } else {
    drawPreferredDisplay();
  }
}

void markHostSeen() {
  const bool wasAcknowledged = hostAcknowledged;
  hostAcknowledged = true;
  lastHostSeenMs = millis();
  hidFallbackWasActive = false;
  resetHidFallbackSliderState();

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
    clearActiveDisplayLineState();

    if (displayAvailable) {
      display.clearDisplay();
      display.display();
    }
    return;
  }

  if (mode == "layout") {
    drawLayoutScreen(
      protocolValue(line, "layout"),
      protocolValue(line, "title"),
      protocolValue(line, "line1"),
      protocolValue(line, "line2"),
      protocolValue(line, "value").toInt(),
      protocolValue(line, "icon"));

    const String duration = protocolValue(line, "durationMs");
    temporaryDisplayUntilMs = duration.length() > 0 && duration.toInt() > 0
      ? millis() + static_cast<uint32_t>(duration.toInt())
      : 0;
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
    String lineAligns[MaxDisplayLines];
    uint8_t lastFilled = 0;

    for (uint8_t index = 0; index < MaxDisplayLines; index++) {
      const String key = "line" + String(index + 1);
      lines[index] = protocolValue(line, key.c_str());
      lineSizes[index] = normalizeDisplayTextSize(
        protocolValue(line, (key + "Size").c_str()).toInt());
      lineAligns[index] = normalizeDisplayAlign(protocolValue(line, (key + "Align").c_str()));
      if (lines[index].length() > 0) {
        lastFilled = index;
      }
    }

    // Keep intermediate blank lines; trim only trailing empty ones.
    setDisplayLines(lines, lineSizes, lineAligns, static_cast<uint8_t>(lastFilled + 1));

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

    // The app always sends the full idle screen in every config message, so empty fields are
    // intentional blank lines and must overwrite the previous value (matches line3/line4 below).
    defaultScreenTitle = protocolValue(line, "defaultTitle");
    defaultScreenTitleSize = normalizeDisplayTextSize(protocolValue(line, "defaultTitleSize").toInt());
    defaultScreenTitleAlign = normalizeDisplayAlign(protocolValue(line, "defaultTitleAlign"));

    defaultScreenLine1 = protocolValue(line, "defaultLine1");
    defaultScreenLine1Size = normalizeDisplayTextSize(protocolValue(line, "defaultLine1Size").toInt());
    defaultScreenLine1Align = normalizeDisplayAlign(protocolValue(line, "defaultLine1Align"));

    defaultScreenLine2 = protocolValue(line, "defaultLine2");
    defaultScreenLine2Size = normalizeDisplayTextSize(protocolValue(line, "defaultLine2Size").toInt());
    defaultScreenLine2Align = normalizeDisplayAlign(protocolValue(line, "defaultLine2Align"));

    defaultScreenLine3 = protocolValue(line, "defaultLine3");
    defaultScreenLine3Size = normalizeDisplayTextSize(protocolValue(line, "defaultLine3Size").toInt());
    defaultScreenLine3Align = normalizeDisplayAlign(protocolValue(line, "defaultLine3Align"));
    defaultScreenLine4 = protocolValue(line, "defaultLine4");
    defaultScreenLine4Size = normalizeDisplayTextSize(protocolValue(line, "defaultLine4Size").toInt());
    defaultScreenLine4Align = normalizeDisplayAlign(protocolValue(line, "defaultLine4Align"));

    currentTime = protocolValue(line, "time");
    currentDate = protocolValue(line, "date");
    currentSystemVolume = protocolValue(line, "volumeSystem");
    setClockBaseline(currentTime, currentDate);

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
  handleHidFallbackButton(button.id, pressed);
}

int snapSliderValue(int value) {
  if (value <= SliderSnapThreshold) {
    return 0;
  }

  if (value >= SliderAdcMax - SliderSnapThreshold) {
    return SliderAdcMax;
  }

  return constrain(value, 0, SliderAdcMax);
}

void sortSliderSamples(int samples[], uint8_t count) {
  for (uint8_t index = 1; index < count; index++) {
    const int current = samples[index];
    int previous = index - 1;

    while (previous >= 0 && samples[previous] > current) {
      samples[previous + 1] = samples[previous];
      previous--;
    }

    samples[previous + 1] = current;
  }
}

int readSliderRaw(uint8_t pin) {
  int samples[SliderSampleCount];
  for (uint8_t index = 0; index < SliderSampleCount; index++) {
    samples[index] = analogRead(pin);
    delayMicroseconds(150);
  }

  sortSliderSamples(samples, SliderSampleCount);

  const uint8_t firstKeptIndex = (SliderSampleCount - SliderKeptSampleCount) / 2;
  long total = 0;
  for (uint8_t index = 0; index < SliderKeptSampleCount; index++) {
    total += samples[firstKeptIndex + index];
  }

  return static_cast<int>(total / SliderKeptSampleCount);
}

void pollSlider(SliderState& slider) {
  const uint32_t now = millis();
  const int raw = readSliderRaw(slider.pin);

  if (slider.smoothedValue < 0) {
    slider.smoothedValue = snapSliderValue(raw);
  } else {
    const int filtered = ((slider.smoothedValue * (SliderSmoothingDivisor - 1)) + raw) / SliderSmoothingDivisor;
    slider.smoothedValue = snapSliderValue(filtered);
  }

  if (slider.lastSentValue >= 0 && abs(slider.smoothedValue - slider.lastSentValue) < SliderDeadband) {
    return;
  }

  if (slider.lastSentValue >= 0 && now - slider.lastSentMs < SliderMinSendIntervalMs) {
    return;
  }

  slider.lastSentValue = slider.smoothedValue;
  slider.lastSentMs = now;
  sendLine("slider;id=" + String(slider.id) + ";value=" + String(slider.smoothedValue) + ";raw=" + String(raw));
  handleHidFallbackSlider(slider);
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
      -1,
      0
    };
    hidFallbackSliderPercents[index] = -1;
    hidFallbackSliderLastSendMs[index] = 0;
  }
}

void initHidFallback() {
  hidConsumerControl.begin();
  USB.begin();
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
  if (activeLayoutScreen) {
    renderActiveLayoutScreen();
  } else {
    renderActiveDisplayLines();
  }
}

void updateIdleClock() {
  if (!clockBaseValid) {
    return;
  }

  const uint32_t now = millis();
  if (static_cast<uint32_t>(now - lastClockTickMs) < 1000) {
    return;
  }
  lastClockTickMs = now;

  const String previousTime = currentTime;
  refreshRunningClock();
  if (currentTime == previousTime) {
    return;  // same minute -> nothing to redraw
  }

  // Only redraw when the idle/default screen is actually on screen.
  if (temporaryDisplayUntilMs != 0) {
    return;
  }
  if (preferredDisplayMode == "media" && currentMediaTitle.length() > 0) {
    return;
  }
  drawIdleStatus();
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

  initHidFallback();
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
  updateHidFallbackState();
  updateDisplayTimeout();
  updateDisplayScroll();
  updateIdleClock();

  for (size_t index = 0; index < ActiveButtonCount; index++) {
    pollButton(buttons[index]);
  }

  for (size_t index = 0; index < ActiveSliderCount; index++) {
    pollSlider(sliders[index]);
  }

  delay(5);
}
