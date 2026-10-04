#include "protocol.h"

#include <stdlib.h>

namespace makropad {

namespace {

int hexValue(char c) {
  if (c >= '0' && c <= '9') return c - '0';
  if (c >= 'a' && c <= 'f') return c - 'a' + 10;
  if (c >= 'A' && c <= 'F') return c - 'A' + 10;
  return -1;
}

std::string trim(const std::string& value) {
  const size_t first = value.find_first_not_of(" \t\r\n");
  if (first == std::string::npos) {
    return "";
  }
  const size_t last = value.find_last_not_of(" \t\r\n");
  return value.substr(first, last - first + 1);
}

}  // namespace

std::string percentEncode(const std::string& value) {
  std::string encoded;
  encoded.reserve(value.size());
  for (const char c : value) {
    switch (c) {
      case '%': encoded += "%25"; break;
      case ';': encoded += "%3B"; break;
      case '=': encoded += "%3D"; break;
      case '\r': encoded += "%0D"; break;
      case '\n': encoded += "%0A"; break;
      default: encoded += c; break;
    }
  }
  return encoded;
}

std::string percentDecode(const std::string& value) {
  std::string decoded;
  decoded.reserve(value.size());
  for (size_t i = 0; i < value.size(); i++) {
    if (value[i] == '%' && i + 2 < value.size()) {
      const int high = hexValue(value[i + 1]);
      const int low = hexValue(value[i + 2]);
      if (high >= 0 && low >= 0) {
        decoded += static_cast<char>(high * 16 + low);
        i += 2;
        continue;
      }
    }
    decoded += value[i];
  }
  return decoded;
}

ProtocolLine ProtocolLine::parse(const std::string& line) {
  ProtocolLine result;
  const std::string trimmed = trim(line);
  size_t start = 0;
  bool first = true;

  while (start <= trimmed.size()) {
    size_t end = trimmed.find(';', start);
    if (end == std::string::npos) {
      end = trimmed.size();
    }
    const std::string segment = trimmed.substr(start, end - start);

    if (first) {
      result.type_ = segment;
      first = false;
    } else if (!segment.empty()) {
      const size_t separator = segment.find('=');
      if (separator != std::string::npos && separator > 0) {
        result.values_.emplace_back(segment.substr(0, separator), percentDecode(segment.substr(separator + 1)));
      }
    }

    start = end + 1;
  }

  return result;
}

bool ProtocolLine::has(const char* key) const {
  for (const auto& entry : values_) {
    if (entry.first == key) {
      return true;
    }
  }
  return false;
}

std::string ProtocolLine::get(const char* key, const std::string& fallback) const {
  for (auto it = values_.rbegin(); it != values_.rend(); ++it) {
    if (it->first == key) {
      return it->second;
    }
  }
  return fallback;
}

int ProtocolLine::getInt(const char* key, int fallback) const {
  const std::string value = get(key);
  if (value.empty()) {
    return fallback;
  }
  char* end = nullptr;
  const long parsed = strtol(value.c_str(), &end, 10);
  return (end != value.c_str() && *end == '\0') ? static_cast<int>(parsed) : fallback;
}

LineBuilder& LineBuilder::add(const char* key, const std::string& value) {
  line_ += ';';
  line_ += key;
  line_ += '=';
  line_ += percentEncode(value);
  return *this;
}

LineBuilder& LineBuilder::add(const char* key, long value) {
  return add(key, std::to_string(value));
}

bool LineBuffer::feed(char c, std::string& out) {
  if (c == '\r') {
    return false;
  }

  if (c == '\n') {
    const bool wasDiscarding = discarding_;
    discarding_ = false;
    std::string line = trim(buffer_);
    buffer_.clear();
    if (wasDiscarding || line.empty()) {
      return false;
    }
    out = line;
    return true;
  }

  if (discarding_) {
    return false;
  }

  if (buffer_.size() >= maxLength_) {
    buffer_.clear();
    discarding_ = true;
    overflow_ = true;
    return false;
  }

  buffer_ += c;
  return false;
}

bool LineBuffer::takeOverflow() {
  const bool overflow = overflow_;
  overflow_ = false;
  return overflow;
}

}  // namespace makropad
