#pragma once

#include <stdint.h>

namespace makropad {

// A reading must stay unchanged for `debounceMs` before it becomes the stable state.
class Debouncer {
 public:
  explicit Debouncer(uint32_t debounceMs = 25) : debounceMs_(debounceMs) {}

  // Returns true when the stable state changed with this reading.
  bool update(bool rawPressed, uint32_t nowMs) {
    if (rawPressed != lastReading_) {
      lastReading_ = rawPressed;
      lastChangeMs_ = nowMs;
    }
    if (nowMs - lastChangeMs_ < debounceMs_ || rawPressed == stable_) {
      return false;
    }
    stable_ = rawPressed;
    return true;
  }

  bool pressed() const { return stable_; }

 private:
  uint32_t debounceMs_;
  bool stable_ = false;
  bool lastReading_ = false;
  uint32_t lastChangeMs_ = 0;
};

}  // namespace makropad
