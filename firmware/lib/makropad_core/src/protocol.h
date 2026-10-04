#pragma once

#include <stddef.h>

#include <string>
#include <utility>
#include <vector>

// Line protocol v2 (docs/PROTOCOL.md): "type;key=value;key=value".

namespace makropad {

std::string percentEncode(const std::string& value);
std::string percentDecode(const std::string& value);

class ProtocolLine {
 public:
  static ProtocolLine parse(const std::string& line);

  const std::string& type() const { return type_; }
  bool has(const char* key) const;
  // Decoded value; `fallback` when the key is missing. Duplicate keys: last wins.
  std::string get(const char* key, const std::string& fallback = "") const;
  int getInt(const char* key, int fallback) const;

 private:
  std::string type_;
  std::vector<std::pair<std::string, std::string>> values_;
};

// Builds an outgoing line; values are percent-encoded.
class LineBuilder {
 public:
  explicit LineBuilder(const char* type) : line_(type) {}
  LineBuilder& add(const char* key, const std::string& value);
  LineBuilder& add(const char* key, long value);
  const std::string& str() const { return line_; }

 private:
  std::string line_;
};

// Collects bytes into lines. Strips '\r', trims whitespace, skips empty lines.
class LineBuffer {
 public:
  explicit LineBuffer(size_t maxLength = 256) : maxLength_(maxLength) {}

  // Returns true when `out` holds a complete, non-empty line.
  bool feed(char c, std::string& out);
  // True once after a line was dropped for exceeding maxLength.
  bool takeOverflow();

 private:
  std::string buffer_;
  size_t maxLength_;
  bool overflow_ = false;
  bool discarding_ = false;
};

}  // namespace makropad
