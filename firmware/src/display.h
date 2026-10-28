#pragma once

#include <stdint.h>

#include <string>

#include "protocol.h"

// SSD1306 output: a fixed idle screen built from `state` plus temporary
// `screen` overlays (docs/PROTOCOL.md).
namespace display {

enum class Icon { None, Ok, Fail, Warn, Sync };

void setup();
void poll(uint32_t nowMs);

void setHostConnected(bool connected, uint32_t nowMs);
void applyState(const makropad::ProtocolLine& line, uint32_t nowMs);
void showScreen(const makropad::ProtocolLine& line, uint32_t nowMs);
void clearOverlay(uint32_t nowMs);
// Panel on/off; switched back on when the app disconnects.
void setEnabled(bool enabled);

// Local overlay (e.g. HID fallback feedback) using the same rendering as `screen`.
void showStatus(const std::string& title, const std::string& text, Icon icon, uint32_t nowMs);

}  // namespace display
