#pragma once

#include <stdint.h>

// USB HID consumer-control fallback while no app is connected.
namespace hid {

void setup();
void poll(uint32_t nowMs);
void setHostConnected(bool connected);
void onButton(uint8_t id, bool pressed, uint32_t nowMs);
void onSlider(uint8_t id, int value, uint32_t nowMs);

}  // namespace hid
