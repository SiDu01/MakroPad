#include "input.h"

#include <Arduino.h>

#include "config.h"
#include "debouncer.h"
#include "hid.h"
#include "hostlink.h"
#include "protocol.h"
#include "slider_filter.h"

using makropad::LineBuilder;

namespace input {

namespace {

makropad::Debouncer buttons[config::ButtonCount];
makropad::SliderFilter sliders[config::ActiveSliderCount];
uint32_t lastSliderPollMs = 0;

int readSliderRaw(uint8_t pin) {
  int samples[config::SliderSampleCount];
  for (uint8_t index = 0; index < config::SliderSampleCount; index++) {
    samples[index] = analogRead(pin);
    delayMicroseconds(150);
  }
  return makropad::trimmedMean(samples, config::SliderSampleCount, 5);
}

}  // namespace

void setup() {
  for (size_t index = 0; index < config::ButtonCount; index++) {
    buttons[index] = makropad::Debouncer(config::DebounceMs);
    pinMode(config::ButtonPins[index], INPUT_PULLUP);
  }
  analogReadResolution(12);
}

void poll(uint32_t nowMs) {
  for (size_t index = 0; index < config::ButtonCount; index++) {
    const bool pressed = digitalRead(config::ButtonPins[index]) == LOW;
    if (!buttons[index].update(pressed, nowMs)) {
      continue;
    }
    const uint8_t id = static_cast<uint8_t>(index + 1);
    hostlink::send(LineBuilder("button").add("id", static_cast<long>(id)).add("state", pressed ? "down" : "up").str());
    hid::onButton(id, pressed, nowMs);
  }

  if (nowMs - lastSliderPollMs < config::SliderPollIntervalMs) {
    return;
  }
  lastSliderPollMs = nowMs;

  for (size_t index = 0; index < config::ActiveSliderCount; index++) {
    if (!sliders[index].update(readSliderRaw(config::SliderPins[index]), nowMs)) {
      continue;
    }
    const uint8_t id = static_cast<uint8_t>(index + 1);
    const int value = sliders[index].normalized();
    hostlink::send(LineBuilder("slider").add("id", static_cast<long>(id)).add("value", static_cast<long>(value)).str());
    hid::onSlider(id, value, nowMs);
  }
}

}  // namespace input
