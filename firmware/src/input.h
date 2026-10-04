#pragma once

#include <stdint.h>

// Buttons (debounced) and sliders (filtered); reports changes to the app and the HID fallback.
namespace input {

void setup();
void poll(uint32_t nowMs);

}  // namespace input
