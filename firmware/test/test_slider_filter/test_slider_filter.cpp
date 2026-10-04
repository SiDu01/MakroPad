#include <unity.h>

#include "slider_filter.h"

using makropad::SliderFilter;
using makropad::SliderFilterConfig;

void setUp() {}
void tearDown() {}

void test_trimmed_mean_drops_outliers() {
  int samples[] = { 4000, 100, 101, 99, 0, 102, 98, 2000, 100 };
  TEST_ASSERT_EQUAL_INT(100, makropad::trimmedMean(samples, 9, 5));
}

void test_first_reading_is_reported() {
  SliderFilter filter;
  TEST_ASSERT_TRUE(filter.update(2048, 0));
  TEST_ASSERT_EQUAL_INT(500, filter.normalized());
}

void test_small_changes_stay_inside_deadband() {
  SliderFilter filter;
  filter.update(2000, 0);
  for (uint32_t t = 100; t < 2000; t += 100) {
    TEST_ASSERT_FALSE(filter.update(2010, t));
  }
}

void test_large_change_is_reported_after_settling() {
  SliderFilter filter;
  filter.update(1000, 0);
  bool reported = false;
  for (uint32_t t = 100; t < 5000 && !reported; t += 50) {
    reported = filter.update(3000, t);
  }
  TEST_ASSERT_TRUE(reported);
}

void test_min_send_interval_is_respected() {
  SliderFilterConfig config;
  config.smoothingDivisor = 1;  // no low-pass, isolate the interval check
  SliderFilter filter(config);
  filter.update(1000, 0);
  TEST_ASSERT_FALSE(filter.update(3000, 10));
  TEST_ASSERT_TRUE(filter.update(3000, 40));
}

void test_endpoints_snap_and_bypass_deadband() {
  SliderFilterConfig config;
  config.smoothingDivisor = 1;
  SliderFilter filter(config);
  filter.update(40, 0);
  TEST_ASSERT_TRUE(filter.update(20, 100));
  TEST_ASSERT_EQUAL_INT(0, filter.normalized());

  filter.update(4060, 200);
  TEST_ASSERT_TRUE(filter.update(4080, 300));
  TEST_ASSERT_EQUAL_INT(1000, filter.normalized());
}

int main() {
  UNITY_BEGIN();
  RUN_TEST(test_trimmed_mean_drops_outliers);
  RUN_TEST(test_first_reading_is_reported);
  RUN_TEST(test_small_changes_stay_inside_deadband);
  RUN_TEST(test_large_change_is_reported_after_settling);
  RUN_TEST(test_min_send_interval_is_respected);
  RUN_TEST(test_endpoints_snap_and_bypass_deadband);
  return UNITY_END();
}
