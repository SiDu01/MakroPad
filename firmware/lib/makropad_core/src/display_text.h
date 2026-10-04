#pragma once

#include <stddef.h>

#include <string>

namespace makropad {

// Converts UTF-8 to the Adafruit GFX built-in font (code page 437, enabled via
// display.cp437(true)). Covers German umlauts and common Latin-1 characters;
// anything else becomes '?'.
std::string utf8ToCp437(const std::string& utf8);

// Visible window of a horizontally scrolling text. Texts that fit are returned
// unchanged; longer texts loop with `gap` spaces between repetitions.
std::string marqueeWindow(const std::string& text, size_t visible, size_t step, size_t gap = 3);

// Number of scroll steps until a marquee of this text repeats (0 = no scrolling).
size_t marqueeCycle(const std::string& text, size_t visible, size_t gap = 3);

}  // namespace makropad
