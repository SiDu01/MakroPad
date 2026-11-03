#pragma once

#include <stdint.h>
#include <stddef.h>

// All hardware and timing constants live here. Pinout: docs/HARDWARE.md.

namespace config {

constexpr const char* FirmwareVersion = "0.4.1";
constexpr const char* DeviceName = "makropad";
constexpr uint8_t ProtocolVersion = 2;

// Buttons, numbered row by row from the top left (OLED occupies the top right 2x2 block):
//  1  2  3  .  .
//  4  5  6  .  .
//  7  8  9 10 11
// 12 13 14 15 16
constexpr uint8_t ButtonPins[] = {
  4, 5, 6,
  18, 10, 11,
  12, 13, 14, 17, 16,
  15, 7, 21, 39, 40
};
constexpr size_t ButtonCount = sizeof(ButtonPins) / sizeof(ButtonPins[0]);
static_assert(ButtonCount == 16, "MakroPad has 16 buttons");

constexpr uint8_t SliderPins[] = { 1, 2, 3 };
constexpr size_t SliderCount = sizeof(SliderPins) / sizeof(SliderPins[0]);
// Unwired ADC pins float and spam events; lower this if a slider is not connected.
constexpr size_t ActiveSliderCount = 3;
static_assert(ActiveSliderCount <= SliderCount, "Too many active sliders");
constexpr uint8_t SliderSampleCount = 9;
constexpr uint32_t SliderPollIntervalMs = 5;

constexpr uint8_t OledSdaPin = 8;
constexpr uint8_t OledSclPin = 9;
constexpr uint8_t OledAddress = 0x3C;
constexpr int ScreenWidth = 128;
constexpr int ScreenHeight = 64;
// The case mounts the OLED upside down (pins at the bottom), so rotate by 180 degrees.
constexpr uint8_t OledRotation = 2;

constexpr uint32_t SerialBaud = 115200;
constexpr uint32_t HelloIntervalMs = 1000;
constexpr uint32_t HostTimeoutMs = 6000;
constexpr uint32_t DebounceMs = 25;
constexpr size_t MaxLineLength = 256;

// Bluetooth LE (Nordic UART Service, docs/PROTOCOL.md)
constexpr const char* BleDeviceName = "MakroPad";
constexpr uint16_t BleMtu = 247;
constexpr size_t BleRxQueueBytes = 1024;
constexpr uint32_t BlePasskeyShowMs = 30000;

// Display
constexpr uint32_t DefaultOverlayMs = 1500;
constexpr uint32_t ScrollStepMs = 220;
constexpr uint32_t ScrollStartPauseMs = 900;

// HID fallback (active while no app has acknowledged the device)
constexpr uint32_t HidStartupGraceMs = 2500;
constexpr uint32_t HidPressMs = 30;
constexpr int HidVolumeThresholdPermille = 40;
constexpr uint32_t HidVolumeIntervalMs = 120;

}  // namespace config
