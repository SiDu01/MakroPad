#include "running_clock.h"

#include <stdio.h>

namespace makropad {

int daysInMonth(int month, int year) {
  static const int lengths[] = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
  if (month == 2 && ((year % 4 == 0 && year % 100 != 0) || year % 400 == 0)) {
    return 29;
  }
  return lengths[(month - 1) % 12];
}

bool RunningClock::set(const std::string& time, const std::string& date, uint32_t nowMs) {
  int hours = -1;
  int minutes = -1;
  int seconds = 0;
  int day = 0;
  int month = 0;
  int year = 0;

  const int timeFields = sscanf(time.c_str(), "%d:%d:%d", &hours, &minutes, &seconds);
  if (timeFields < 2) {
    return false;
  }
  if (timeFields == 2) {
    seconds = 0;
  }
  if (sscanf(date.c_str(), "%d.%d.%d", &day, &month, &year) != 3) {
    return false;
  }
  if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59
      || month < 1 || month > 12 || year < 2000 || day < 1 || day > daysInMonth(month, year)) {
    return false;
  }

  baseSecondsOfDay_ = hours * 3600L + minutes * 60L + seconds;
  baseDay_ = day;
  baseMonth_ = month;
  baseYear_ = year;
  baseMillis_ = nowMs;
  valid_ = true;
  return true;
}

ClockReading RunningClock::read(uint32_t nowMs) const {
  const long elapsed = static_cast<long>((nowMs - baseMillis_) / 1000UL);
  const long total = baseSecondsOfDay_ + elapsed;
  long extraDays = total / 86400L;
  const long secondsOfDay = total % 86400L;

  ClockReading reading;
  reading.hours = static_cast<int>(secondsOfDay / 3600L);
  reading.minutes = static_cast<int>((secondsOfDay % 3600L) / 60L);
  reading.seconds = static_cast<int>(secondsOfDay % 60L);
  reading.day = baseDay_;
  reading.month = baseMonth_;
  reading.year = baseYear_;

  while (extraDays-- > 0) {
    if (++reading.day > daysInMonth(reading.month, reading.year)) {
      reading.day = 1;
      if (++reading.month > 12) {
        reading.month = 1;
        reading.year++;
      }
    }
  }
  return reading;
}

}  // namespace makropad
