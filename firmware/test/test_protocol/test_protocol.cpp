#include <unity.h>

#include "protocol.h"

using makropad::LineBuffer;
using makropad::LineBuilder;
using makropad::ProtocolLine;

void setUp() {}
void tearDown() {}

void test_parse_type_and_values() {
  const ProtocolLine line = ProtocolLine::parse("screen;type=volume;value=72");
  TEST_ASSERT_EQUAL_STRING("screen", line.type().c_str());
  TEST_ASSERT_EQUAL_STRING("volume", line.get("type").c_str());
  TEST_ASSERT_EQUAL_INT(72, line.getInt("value", -1));
}

void test_parse_type_without_values() {
  const ProtocolLine line = ProtocolLine::parse("  clear \r\n");
  TEST_ASSERT_EQUAL_STRING("clear", line.type().c_str());
  TEST_ASSERT_FALSE(line.has("type"));
}

void test_percent_decoding() {
  const ProtocolLine line = ProtocolLine::parse("screen;text=Volume%3D50%25%3Bx%0Ay");
  TEST_ASSERT_EQUAL_STRING("Volume=50%;x\ny", line.get("text").c_str());
}

void test_missing_and_empty_values() {
  const ProtocolLine line = ProtocolLine::parse("state;artist=;title=Song");
  TEST_ASSERT_TRUE(line.has("artist"));
  TEST_ASSERT_EQUAL_STRING("", line.get("artist", "x").c_str());
  TEST_ASSERT_FALSE(line.has("profile"));
  TEST_ASSERT_EQUAL_STRING("x", line.get("profile", "x").c_str());
}

void test_last_duplicate_key_wins_and_bad_int_falls_back() {
  const ProtocolLine line = ProtocolLine::parse("slider;value=1;value=2;id=abc");
  TEST_ASSERT_EQUAL_INT(2, line.getInt("value", -1));
  TEST_ASSERT_EQUAL_INT(-1, line.getInt("id", -1));
}

void test_builder_encodes_values() {
  const std::string line = LineBuilder("pong").add("id", "a;b").add("n", 7L).str();
  TEST_ASSERT_EQUAL_STRING("pong;id=a%3Bb;n=7", line.c_str());
}

void test_line_buffer_splits_and_trims() {
  LineBuffer buffer;
  std::string out;
  const char* input = "ping;id=1\r\n\n  hello-ack \n";
  int lines = 0;
  std::string last;
  for (const char* c = input; *c; c++) {
    if (buffer.feed(*c, out)) {
      lines++;
      last = out;
    }
  }
  TEST_ASSERT_EQUAL_INT(2, lines);
  TEST_ASSERT_EQUAL_STRING("hello-ack", last.c_str());
}

void test_line_buffer_drops_overlong_line() {
  LineBuffer buffer(8);
  std::string out;
  bool got = false;
  for (const char* c = "0123456789abc\nok\n"; *c; c++) {
    if (buffer.feed(*c, out)) {
      got = true;
      TEST_ASSERT_EQUAL_STRING("ok", out.c_str());
    }
  }
  TEST_ASSERT_TRUE(got);
  TEST_ASSERT_TRUE(buffer.takeOverflow());
  TEST_ASSERT_FALSE(buffer.takeOverflow());
}

int main() {
  UNITY_BEGIN();
  RUN_TEST(test_parse_type_and_values);
  RUN_TEST(test_parse_type_without_values);
  RUN_TEST(test_percent_decoding);
  RUN_TEST(test_missing_and_empty_values);
  RUN_TEST(test_last_duplicate_key_wins_and_bad_int_falls_back);
  RUN_TEST(test_builder_encodes_values);
  RUN_TEST(test_line_buffer_splits_and_trims);
  RUN_TEST(test_line_buffer_drops_overlong_line);
  return UNITY_END();
}
