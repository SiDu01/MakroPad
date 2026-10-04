#include <unity.h>

#include "debouncer.h"
#include "display_text.h"
#include "running_clock.h"
#include "volume_nudger.h"

using namespace makropad;

void setUp() {}
void tearDown() {}

void test_debouncer_ignores_bounce() {
  Debouncer debouncer(25);
  TEST_ASSERT_FALSE(debouncer.update(true, 0));
  TEST_ASSERT_FALSE(debouncer.update(false, 5));
  TEST_ASSERT_FALSE(debouncer.update(true, 10));
  TEST_ASSERT_FALSE(debouncer.update(true, 30));
  TEST_ASSERT_TRUE(debouncer.update(true, 35));
  TEST_ASSERT_TRUE(debouncer.pressed());
  TEST_ASSERT_FALSE(debouncer.update(true, 100));
}

void test_clock_rolls_forward_over_midnight_and_month() {
  RunningClock clock;
  TEST_ASSERT_TRUE(clock.set("23:59:30", "31.12.2026", 1000));
  const ClockReading reading = clock.read(1000 + 45000);
  TEST_ASSERT_EQUAL_INT(0, reading.hours);
  TEST_ASSERT_EQUAL_INT(0, reading.minutes);
  TEST_ASSERT_EQUAL_INT(15, reading.seconds);
  TEST_ASSERT_EQUAL_INT(1, reading.day);
  TEST_ASSERT_EQUAL_INT(1, reading.month);
  TEST_ASSERT_EQUAL_INT(2027, reading.year);
}

void test_clock_accepts_short_time_and_rejects_garbage() {
  RunningClock clock;
  TEST_ASSERT_TRUE(clock.set("08:05", "29.02.2028", 0));
  TEST_ASSERT_EQUAL_INT(8, clock.read(0).hours);
  TEST_ASSERT_FALSE(clock.set("25:00", "01.01.2026", 0));
  TEST_ASSERT_FALSE(clock.set("10:00", "29.02.2027", 0));
  TEST_ASSERT_TRUE(clock.valid());
  TEST_ASSERT_EQUAL_INT(29, clock.read(0).day);
}

void test_utf8_umlauts_map_to_cp437() {
  const std::string converted = utf8ToCp437("Lautst\xC3\xA4rke \xC3\x9F \xE2\x82\xAC");
  TEST_ASSERT_EQUAL_STRING("Lautst\x84rke \xE1 ?", converted.c_str());
}

void test_marquee_window() {
  TEST_ASSERT_EQUAL_STRING("short", marqueeWindow("short", 10, 7).c_str());
  TEST_ASSERT_EQUAL_STRING("abcd", marqueeWindow("abcdefgh", 4, 0, 2).c_str());
  TEST_ASSERT_EQUAL_STRING("gh  ", marqueeWindow("abcdefgh", 4, 6, 2).c_str());
  TEST_ASSERT_EQUAL_STRING("h  a", marqueeWindow("abcdefgh", 4, 7, 2).c_str());
  TEST_ASSERT_EQUAL_UINT(10, marqueeCycle("abcdefgh", 4, 2));
}

void test_volume_nudger_steps_and_throttles() {
  VolumeNudger nudger(40, 120);
  TEST_ASSERT_EQUAL_INT(0, nudger.update(500, 0));
  TEST_ASSERT_EQUAL_INT(0, nudger.update(520, 10));
  TEST_ASSERT_EQUAL_INT(1, nudger.update(560, 20));
  TEST_ASSERT_EQUAL_INT(0, nudger.update(700, 50));
  TEST_ASSERT_EQUAL_INT(1, nudger.update(700, 200));
  TEST_ASSERT_EQUAL_INT(-1, nudger.update(100, 400));
}

int main() {
  UNITY_BEGIN();
  RUN_TEST(test_debouncer_ignores_bounce);
  RUN_TEST(test_clock_rolls_forward_over_midnight_and_month);
  RUN_TEST(test_clock_accepts_short_time_and_rejects_garbage);
  RUN_TEST(test_utf8_umlauts_map_to_cp437);
  RUN_TEST(test_marquee_window);
  RUN_TEST(test_volume_nudger_steps_and_throttles);
  return UNITY_END();
}
