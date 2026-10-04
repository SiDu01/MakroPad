#pragma once

#include <stdint.h>

#include <string>

namespace makropad {

struct ClockReading {
  int hours;
  int minutes;
  int seconds;
  int day;
  int month;
  int year;
};

// The board has no RTC. The host sends its local time; the clock rolls it
// forward with millis() so the idle screen keeps ticking without the app.
class RunningClock {
 public:
  // time: "HH:mm" or "HH:mm:ss", date: "dd.MM.yyyy". Returns false (and keeps
  // the previous baseline) when either value is malformed.
  bool set(const std::string& time, const std::string& date, uint32_t nowMs);
  bool valid() const { return valid_; }
  ClockReading read(uint32_t nowMs) const;

 private:
  bool valid_ = false;
  uint32_t baseMillis_ = 0;
  long baseSecondsOfDay_ = 0;
  int baseDay_ = 1;
  int baseMonth_ = 1;
  int baseYear_ = 2000;
};

int daysInMonth(int month, int year);

}  // namespace makropad
