#include "hid.h"

#include <Arduino.h>
#include <USB.h>
#include <USBHIDConsumerControl.h>

#include "config.h"
#include "display.h"
#include "volume_nudger.h"

namespace hid {

namespace {

struct FallbackKey {
  uint16_t usage;
  const char* label;
};

// Buttons 1-7 in order; see docs/PROTOCOL.md.
constexpr FallbackKey ButtonKeys[] = {
  { CONSUMER_CONTROL_PLAY_PAUSE, "Play/Pause" },
  { CONSUMER_CONTROL_SCAN_NEXT, "Nächster Titel" },
  { CONSUMER_CONTROL_SCAN_PREVIOUS, "Vorheriger Titel" },
  { CONSUMER_CONTROL_STOP, "Stopp" },
  { CONSUMER_CONTROL_MUTE, "Stumm" },
  { CONSUMER_CONTROL_VOLUME_DECREMENT, "Leiser" },
  { CONSUMER_CONTROL_VOLUME_INCREMENT, "Lauter" },
};
constexpr size_t ButtonKeyCount = sizeof(ButtonKeys) / sizeof(ButtonKeys[0]);

USBHIDConsumerControl consumerControl;
makropad::VolumeNudger volumeNudger(config::HidVolumeThresholdPermille, config::HidVolumeIntervalMs);
bool hostConnected = false;

bool active(uint32_t nowMs) {
  return !hostConnected && nowMs >= config::HidStartupGraceMs;
}

void press(uint16_t usage) {
  consumerControl.press(usage);
  delay(config::HidPressMs);
  consumerControl.release();
}

}  // namespace

void setup() {
  consumerControl.begin();
  USB.begin();
}

void poll(uint32_t) {}

void setHostConnected(bool connected) {
  hostConnected = connected;
  volumeNudger.reset();
}

void onButton(uint8_t id, bool pressed, uint32_t nowMs) {
  // Fire on release, like a media key that was tapped.
  if (pressed || !active(nowMs) || id == 0 || id > ButtonKeyCount) {
    return;
  }
  const FallbackKey& key = ButtonKeys[id - 1];
  press(key.usage);
  display::showStatus("Ohne App", key.label, display::Icon::Ok, nowMs);
}

void onSlider(uint8_t id, int value, uint32_t nowMs) {
  if (id != 1 || !active(nowMs)) {
    return;
  }
  const int step = volumeNudger.update(value, nowMs);
  if (step == 0) {
    return;
  }
  press(step > 0 ? CONSUMER_CONTROL_VOLUME_INCREMENT : CONSUMER_CONTROL_VOLUME_DECREMENT);
  display::showStatus("Ohne App", step > 0 ? "Lauter" : "Leiser", display::Icon::None, nowMs);
}

}  // namespace hid
