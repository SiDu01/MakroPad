#pragma once

#include <stdint.h>

// Hardware-independent slider filtering, ported from the legacy firmware.
// Chain: trimmed mean of a sample burst -> low-pass -> endpoint snap ->
// deadband -> minimum send interval. Output is normalized to 0..1000.

namespace makropad {

struct SliderFilterConfig {
  int adcMax = 4095;
  uint8_t keptSampleCount = 5;
  int smoothingDivisor = 16;
  int snapThreshold = 24;
  int deadband = 32;
  uint32_t minSendIntervalMs = 35;
};

// Sorts `samples` in place and returns the mean of the middle `kept` values.
int trimmedMean(int* samples, uint8_t count, uint8_t kept);

class SliderFilter {
 public:
  explicit SliderFilter(const SliderFilterConfig& config = SliderFilterConfig());

  // Feeds one (already trimmed) reading. Returns true when the value changed
  // enough to be reported.
  bool update(int raw, uint32_t nowMs);

  int smoothed() const { return smoothed_; }
  // Last reported value, normalized to 0..1000 (-1 before the first report).
  int normalized() const;
  void reset();

 private:
  int snap(int value) const;

  SliderFilterConfig config_;
  int smoothed_ = -1;
  int lastSent_ = -1;
  uint32_t lastSentMs_ = 0;
};

}  // namespace makropad
