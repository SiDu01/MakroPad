#include "display.h"

#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <Arduino.h>
#include <Wire.h>

#include "config.h"
#include "display_text.h"
#include "hostlink.h"
#include "running_clock.h"

using makropad::ProtocolLine;

namespace display {

namespace {

constexpr int CharWidth = 6;
constexpr int HeaderHeight = 12;

enum class ScreenType { Volume, Action, Media, Profile, Status };

struct State {
  std::string profile;
  std::string artist;
  std::string title;
  makropad::RunningClock clock;
};

struct Overlay {
  bool active = false;
  ScreenType type = ScreenType::Status;
  std::string title;
  std::string text;
  int value = 0;
  Icon icon = Icon::None;
  uint32_t untilMs = 0;  // 0 = until replaced or cleared
};

Adafruit_SSD1306 oled(config::ScreenWidth, config::ScreenHeight, &Wire, -1);
bool available = false;
bool hostConnected = false;
State state;
Overlay overlay;

bool dirty = true;
bool scrolling = false;
uint32_t scrollStep = 0;
uint32_t lastScrollMs = 0;
int lastDrawnMinute = -1;

void contentChanged(uint32_t nowMs) {
  dirty = true;
  scrollStep = 0;
  lastScrollMs = nowMs;
}

// Prints `text` (UTF-8) at x/y; texts wider than `maxChars` scroll.
void drawText(int x, int y, const std::string& text, uint8_t size, int maxChars, bool center = false) {
  const std::string converted = makropad::utf8ToCp437(text);
  const size_t visible = maxChars > 0 ? static_cast<size_t>(maxChars) : 1;
  if (makropad::marqueeCycle(converted, visible) > 0) {
    scrolling = true;
  }
  const std::string shown = makropad::marqueeWindow(converted, visible, scrollStep);
  if (center) {
    const int width = static_cast<int>(shown.size()) * CharWidth * size;
    x = width < config::ScreenWidth ? (config::ScreenWidth - width) / 2 : 0;
  }
  oled.setTextSize(size);
  oled.setCursor(x, y);
  oled.print(shown.c_str());
}

void drawHeader(const std::string& title) {
  oled.fillRect(0, 0, config::ScreenWidth, HeaderHeight, SSD1306_WHITE);
  oled.setTextColor(SSD1306_BLACK);
  drawText(2, 2, title, 1, (config::ScreenWidth - 4) / CharWidth);
  oled.setTextColor(SSD1306_WHITE);
}

// Header with a short right-aligned text (the clock while music plays).
void drawHeader(const std::string& left, const std::string& right) {
  oled.fillRect(0, 0, config::ScreenWidth, HeaderHeight, SSD1306_WHITE);
  oled.setTextColor(SSD1306_BLACK);
  const int rightWidth = static_cast<int>(right.size()) * CharWidth;
  const int rightX = config::ScreenWidth - 2 - rightWidth;
  drawText(2, 2, left, 1, (rightX - 2 - CharWidth) / CharWidth);
  drawText(rightX, 2, right, 1, static_cast<int>(right.size()));
  oled.setTextColor(SSD1306_WHITE);
}

// Track title large, artist below; shared by the idle screen and the media overlay.
void drawTrack(const std::string& title, const std::string& artist) {
  drawText(0, 19, title, 2, config::ScreenWidth / (CharWidth * 2), true);
  drawText(0, 42, artist, 1, config::ScreenWidth / CharWidth, true);
}

void drawIcon(Icon icon, int x, int y) {
  switch (icon) {
    case Icon::Ok:
      oled.drawCircle(x + 9, y + 9, 8, SSD1306_WHITE);
      oled.drawLine(x + 5, y + 9, x + 8, y + 12, SSD1306_WHITE);
      oled.drawLine(x + 8, y + 12, x + 14, y + 5, SSD1306_WHITE);
      break;
    case Icon::Warn:
      oled.drawTriangle(x + 9, y + 1, x + 1, y + 17, x + 17, y + 17, SSD1306_WHITE);
      oled.drawLine(x + 9, y + 6, x + 9, y + 11, SSD1306_WHITE);
      oled.drawPixel(x + 9, y + 14, SSD1306_WHITE);
      break;
    case Icon::Fail:
      oled.drawRect(x + 1, y + 1, 17, 17, SSD1306_WHITE);
      oled.drawLine(x + 5, y + 5, x + 13, y + 13, SSD1306_WHITE);
      oled.drawLine(x + 13, y + 5, x + 5, y + 13, SSD1306_WHITE);
      break;
    case Icon::Sync:
      oled.drawCircle(x + 9, y + 9, 7, SSD1306_WHITE);
      oled.drawLine(x + 13, y + 3, x + 17, y + 3, SSD1306_WHITE);
      oled.drawLine(x + 17, y + 3, x + 15, y + 1, SSD1306_WHITE);
      oled.drawLine(x + 5, y + 15, x + 1, y + 15, SSD1306_WHITE);
      oled.drawLine(x + 1, y + 15, x + 3, y + 17, SSD1306_WHITE);
      break;
    case Icon::None:
      break;
  }
}

void drawProgressBar(int x, int y, int width, int height, int percent) {
  oled.drawRect(x, y, width, height, SSD1306_WHITE);
  const int fill = (width - 2) * constrain(percent, 0, 100) / 100;
  if (fill > 0) {
    oled.fillRect(x + 1, y + 1, fill, height - 2, SSD1306_WHITE);
  }
}

std::string orDefault(const std::string& value, const char* fallback) {
  return value.empty() ? std::string(fallback) : value;
}

void drawIdle(uint32_t nowMs) {
  std::string header;
  if (!hostConnected) {
    header = "Keine App";
  } else {
    header = orDefault(state.profile, "MakroPad");
  }

  char buffer[16];
  if (state.clock.valid()) {
    const makropad::ClockReading now = state.clock.read(nowMs);
    snprintf(buffer, sizeof(buffer), "%02d:%02d", now.hours, now.minutes);
    lastDrawnMinute = now.minutes;
  } else {
    snprintf(buffer, sizeof(buffer), "--:--");
  }

  // While music plays the track takes the space and the clock moves into the header.
  if (!state.title.empty()) {
    drawHeader(header, buffer);
    drawTrack(state.title, state.artist);
    return;
  }

  drawHeader(header);
  drawText(0, 20, buffer, 3, 7, true);

  std::string bottom;
  if (state.clock.valid()) {
    const makropad::ClockReading now = state.clock.read(nowMs);
    snprintf(buffer, sizeof(buffer), "%02d.%02d.%04d", now.day, now.month, now.year);
    bottom = buffer;
  } else if (!hostConnected) {
    bottom = "Warte auf App";
  }
  drawText(0, 52, bottom, 1, config::ScreenWidth / CharWidth, true);
}

void drawOverlay() {
  constexpr int FullWidthChars = config::ScreenWidth / CharWidth;
  switch (overlay.type) {
    case ScreenType::Volume: {
      drawHeader(orDefault(overlay.title, "Lautstärke"));
      drawText(0, 16, overlay.text, 1, FullWidthChars);
      char buffer[8];
      snprintf(buffer, sizeof(buffer), "%d%%", constrain(overlay.value, 0, 100));
      drawText(0, 28, buffer, 2, 5);
      drawProgressBar(0, 50, config::ScreenWidth, 12, overlay.value);
      break;
    }
    case ScreenType::Media:
      drawHeader("Wiedergabe");
      drawTrack(overlay.title, overlay.text);
      break;
    case ScreenType::Profile:
      drawHeader(orDefault(overlay.title, "Profil"));
      drawText(0, 30, overlay.text, 2, config::ScreenWidth / (CharWidth * 2), true);
      break;
    case ScreenType::Action:
    case ScreenType::Status: {
      drawHeader(orDefault(overlay.title, overlay.type == ScreenType::Action ? "Aktion" : "Status"));
      const bool hasIcon = overlay.icon != Icon::None;
      drawIcon(overlay.icon, 0, 24);
      const int x = hasIcon ? 24 : 0;
      drawText(x, 30, overlay.text, 1, (config::ScreenWidth - x) / CharWidth);
      break;
    }
  }
}

void render(uint32_t nowMs) {
  if (!available) {
    return;
  }
  scrolling = false;
  oled.clearDisplay();
  oled.setTextColor(SSD1306_WHITE);
  if (overlay.active) {
    drawOverlay();
  } else {
    drawIdle(nowMs);
  }
  oled.display();
  dirty = false;
}

ScreenType parseType(const std::string& type) {
  if (type == "volume") return ScreenType::Volume;
  if (type == "action") return ScreenType::Action;
  if (type == "media") return ScreenType::Media;
  if (type == "profile") return ScreenType::Profile;
  return ScreenType::Status;
}

Icon parseIcon(const std::string& icon) {
  if (icon == "ok") return Icon::Ok;
  if (icon == "fail") return Icon::Fail;
  if (icon == "warn") return Icon::Warn;
  if (icon == "sync") return Icon::Sync;
  return Icon::None;
}

void startOverlay(uint32_t durationMs, uint32_t nowMs) {
  overlay.active = true;
  overlay.untilMs = durationMs == 0 ? 0 : nowMs + durationMs;
  if (overlay.untilMs == 0 && durationMs != 0) {
    overlay.untilMs = 1;  // keep 0 reserved for "no timeout"
  }
  contentChanged(nowMs);
}

}  // namespace

void setup() {
  Wire.begin(config::OledSdaPin, config::OledSclPin);
  available = oled.begin(SSD1306_SWITCHCAPVCC, config::OledAddress)
              || oled.begin(SSD1306_SWITCHCAPVCC, 0x3D);
  if (!available) {
    hostlink::send("error;code=display_init_failed;message=OLED not found");
    return;
  }
  oled.cp437(true);
  oled.setRotation(config::OledRotation);
  render(millis());
}

void poll(uint32_t nowMs) {
  if (overlay.active && overlay.untilMs != 0 && static_cast<int32_t>(nowMs - overlay.untilMs) >= 0) {
    overlay.active = false;
    contentChanged(nowMs);
  }

  if (scrolling) {
    const uint32_t interval = scrollStep == 0 ? config::ScrollStartPauseMs : config::ScrollStepMs;
    if (nowMs - lastScrollMs >= interval) {
      scrollStep++;
      lastScrollMs = nowMs;
      dirty = true;
    }
  }

  if (!overlay.active && state.clock.valid() && state.clock.read(nowMs).minutes != lastDrawnMinute) {
    dirty = true;
  }

  if (dirty) {
    render(nowMs);
  }
}

void setHostConnected(bool connected, uint32_t nowMs) {
  hostConnected = connected;
  if (!connected) {
    overlay.active = false;
  }
  contentChanged(nowMs);
}

void applyState(const ProtocolLine& line, uint32_t nowMs) {
  if (line.has("profile")) state.profile = line.get("profile");
  if (line.has("artist")) state.artist = line.get("artist");
  if (line.has("title")) state.title = line.get("title");
  if (line.has("time") && line.has("date")) {
    state.clock.set(line.get("time"), line.get("date"), nowMs);
  }
  if (!overlay.active) {
    contentChanged(nowMs);
  }
}

void showScreen(const ProtocolLine& line, uint32_t nowMs) {
  overlay.type = parseType(line.get("type"));
  overlay.title = line.get("title");
  overlay.text = line.get("text");
  overlay.value = line.getInt("value", 0);
  overlay.icon = parseIcon(line.get("icon"));
  const int ms = line.getInt("ms", static_cast<int>(config::DefaultOverlayMs));
  startOverlay(ms < 0 ? config::DefaultOverlayMs : static_cast<uint32_t>(ms), nowMs);
}

void clearOverlay(uint32_t nowMs) {
  overlay.active = false;
  contentChanged(nowMs);
}

void showStatus(const std::string& title, const std::string& text, Icon icon, uint32_t nowMs) {
  overlay.type = ScreenType::Status;
  overlay.title = title;
  overlay.text = text;
  overlay.value = 0;
  overlay.icon = icon;
  startOverlay(config::DefaultOverlayMs, nowMs);
}

}  // namespace display
