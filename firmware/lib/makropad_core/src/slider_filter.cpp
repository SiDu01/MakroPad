#include "slider_filter.h"

#include <stdlib.h>

namespace makropad {

int trimmedMean(int* samples, uint8_t count, uint8_t kept) {
  if (count == 0) {
    return 0;
  }
  if (kept == 0 || kept > count) {
    kept = count;
  }

  for (uint8_t index = 1; index < count; index++) {
    const int current = samples[index];
    int previous = index - 1;
    while (previous >= 0 && samples[previous] > current) {
      samples[previous + 1] = samples[previous];
      previous--;
    }
    samples[previous + 1] = current;
  }

  const uint8_t first = (count - kept) / 2;
  long total = 0;
  for (uint8_t index = 0; index < kept; index++) {
    total += samples[first + index];
  }
  return static_cast<int>(total / kept);
}

SliderFilter::SliderFilter(const SliderFilterConfig& config) : config_(config) {}

bool SliderFilter::update(int raw, uint32_t nowMs) {
  if (smoothed_ < 0) {
    smoothed_ = snap(raw);
  } else {
    const int filtered = (smoothed_ * (config_.smoothingDivisor - 1) + raw) / config_.smoothingDivisor;
    smoothed_ = snap(filtered);
  }

  if (lastSent_ >= 0) {
    // Endpoints always pass so 0 % and 100 % are reachable despite the deadband.
    const bool atEndpoint = (smoothed_ == 0 || smoothed_ == config_.adcMax) && smoothed_ != lastSent_;
    if (!atEndpoint && abs(smoothed_ - lastSent_) < config_.deadband) {
      return false;
    }
    if (nowMs - lastSentMs_ < config_.minSendIntervalMs) {
      return false;
    }
  }

  lastSent_ = smoothed_;
  lastSentMs_ = nowMs;
  return true;
}

int SliderFilter::normalized() const {
  if (lastSent_ < 0) {
    return -1;
  }
  return static_cast<int>((static_cast<long>(lastSent_) * 1000L + config_.adcMax / 2) / config_.adcMax);
}

void SliderFilter::reset() {
  smoothed_ = -1;
  lastSent_ = -1;
  lastSentMs_ = 0;
}

int SliderFilter::snap(int value) const {
  if (value <= config_.snapThreshold) {
    return 0;
  }
  if (value >= config_.adcMax - config_.snapThreshold) {
    return config_.adcMax;
  }
  return value;
}

}  // namespace makropad
