#include <Arduino.h>

#include "config.h"

// M0 skeleton: announces the device until the modules (input, protocol,
// display, hid) are ported in M1. See docs/REWRITE_PLAN.md.

namespace {

uint32_t lastHelloMs = 0;

void sendHello() {
  Serial.printf("hello;proto=%u;fw=%s;device=%s;buttons=%u;sliders=%u;display=ssd1306-128x64\n",
                config::ProtocolVersion, config::FirmwareVersion, config::DeviceName,
                static_cast<unsigned>(config::ButtonCount), static_cast<unsigned>(config::SliderCount));
}

}  // namespace

void setup() {
  Serial.begin(config::SerialBaud);
}

void loop() {
  const uint32_t now = millis();
  if (now - lastHelloMs >= config::HelloIntervalMs) {
    lastHelloMs = now;
    sendHello();
  }
}
