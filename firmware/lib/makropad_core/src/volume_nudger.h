#pragma once

#include <stdint.h>
#include <stdlib.h>

namespace makropad {

// HID fallback for a slider: HID can only press "volume up/down", so slider
// movement is translated into throttled relative steps.
class VolumeNudger {
 public:
  VolumeNudger(int thresholdPermille = 40, uint32_t intervalMs = 120)
      : threshold_(thresholdPermille), intervalMs_(intervalMs) {}

  // value: slider position 0..1000. Returns +1 (louder), -1 (quieter) or 0.
  int update(int value, uint32_t nowMs) {
    if (last_ < 0) {
      last_ = value;
      return 0;
    }
    const int delta = value - last_;
    if (abs(delta) < threshold_) {
      return 0;
    }
    if (hasSent_ && nowMs - lastSendMs_ < intervalMs_) {
      return 0;
    }
    last_ = value;
    lastSendMs_ = nowMs;
    hasSent_ = true;
    return delta > 0 ? 1 : -1;
  }

  void reset() {
    last_ = -1;
    hasSent_ = false;
  }

 private:
  int threshold_;
  uint32_t intervalMs_;
  int last_ = -1;
  uint32_t lastSendMs_ = 0;
  bool hasSent_ = false;
};

}  // namespace makropad
