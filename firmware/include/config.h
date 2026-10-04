#pragma once

#include <stdint.h>
#include <stddef.h>

// All hardware and timing constants live here. Pinout: docs/HARDWARE.md.

namespace config {

constexpr const char* FirmwareVersion = "0.3.0";
constexpr const char* DeviceName = "makropad";
constexpr uint8_t ProtocolVersion = 2;

// Buttons, numbered row by row from the top left (OLED occupies the top right 2x2 block):
//  1  2  3  .  .
//  4  5  6  .  .
//  7  8  9 10 11
// 12 13 14 15 16
constexpr uint8_t ButtonPins[] = {
  4, 5, 6,
  7, 10, 11,
  12, 13, 14, 15, 16,
  17, 18, 21, 39, 40
};
constexpr size_t ButtonCount = sizeof(ButtonPins) / sizeof(ButtonPins[0]);
static_assert(ButtonCount == 16, "MakroPad has 16 buttons");

constexpr uint8_t SliderPins[] = { 1, 2, 3 };
constexpr size_t SliderCount = sizeof(SliderPins) / sizeof(SliderPins[0]);

constexpr uint8_t OledSdaPin = 8;
constexpr uint8_t OledSclPin = 9;
constexpr uint8_t OledAddress = 0x3C;
constexpr int ScreenWidth = 128;
constexpr int ScreenHeight = 64;

constexpr uint32_t SerialBaud = 115200;
constexpr uint32_t HelloIntervalMs = 1000;
constexpr uint32_t HostTimeoutMs = 6000;
constexpr uint32_t DebounceMs = 25;

}  // namespace config
