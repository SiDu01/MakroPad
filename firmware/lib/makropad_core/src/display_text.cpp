#include "display_text.h"

#include <stdint.h>

namespace makropad {

namespace {

char cp437For(uint32_t codepoint) {
  switch (codepoint) {
    case 0x00C4: return static_cast<char>(0x8E);  // Ä
    case 0x00D6: return static_cast<char>(0x99);  // Ö
    case 0x00DC: return static_cast<char>(0x9A);  // Ü
    case 0x00E4: return static_cast<char>(0x84);  // ä
    case 0x00F6: return static_cast<char>(0x94);  // ö
    case 0x00FC: return static_cast<char>(0x81);  // ü
    case 0x00DF: return static_cast<char>(0xE1);  // ß
    case 0x00E9: return static_cast<char>(0x82);  // é
    case 0x00E8: return static_cast<char>(0x8A);  // è
    case 0x00EA: return static_cast<char>(0x88);  // ê
    case 0x00E1: return static_cast<char>(0xA0);  // á
    case 0x00E0: return static_cast<char>(0x85);  // à
    case 0x00ED: return static_cast<char>(0xA1);  // í
    case 0x00F3: return static_cast<char>(0xA2);  // ó
    case 0x00FA: return static_cast<char>(0xA3);  // ú
    case 0x00F1: return static_cast<char>(0xA4);  // ñ
    case 0x00D1: return static_cast<char>(0xA5);  // Ñ
    case 0x00E7: return static_cast<char>(0x87);  // ç
    case 0x00C9: return static_cast<char>(0x90);  // É
    case 0x00B0: return static_cast<char>(0xF8);  // °
    case 0x00B5: return static_cast<char>(0xE6);  // µ
    case 0x2013:                                   // –
    case 0x2014: return '-';                       // —
    case 0x2018:
    case 0x2019: return '\'';
    case 0x201C:
    case 0x201D:
    case 0x201E: return '"';
    case 0x2026: return '.';                       // …
    default: return '?';
  }
}

}  // namespace

std::string utf8ToCp437(const std::string& utf8) {
  std::string out;
  out.reserve(utf8.size());

  for (size_t i = 0; i < utf8.size();) {
    const uint8_t lead = static_cast<uint8_t>(utf8[i]);
    if (lead < 0x80) {
      out += static_cast<char>(lead);
      i++;
      continue;
    }

    size_t length = 0;
    uint32_t codepoint = 0;
    if ((lead & 0xE0) == 0xC0) {
      length = 2;
      codepoint = lead & 0x1F;
    } else if ((lead & 0xF0) == 0xE0) {
      length = 3;
      codepoint = lead & 0x0F;
    } else if ((lead & 0xF8) == 0xF0) {
      length = 4;
      codepoint = lead & 0x07;
    } else {
      out += '?';
      i++;
      continue;
    }

    if (i + length > utf8.size()) {
      out += '?';
      break;
    }

    bool valid = true;
    for (size_t k = 1; k < length; k++) {
      const uint8_t next = static_cast<uint8_t>(utf8[i + k]);
      if ((next & 0xC0) != 0x80) {
        valid = false;
        break;
      }
      codepoint = (codepoint << 6) | (next & 0x3F);
    }

    out += valid ? cp437For(codepoint) : '?';
    i += valid ? length : 1;
  }

  return out;
}

size_t marqueeCycle(const std::string& text, size_t visible, size_t gap) {
  return text.size() > visible ? text.size() + gap : 0;
}

std::string marqueeWindow(const std::string& text, size_t visible, size_t step, size_t gap) {
  const size_t cycle = marqueeCycle(text, visible, gap);
  if (cycle == 0) {
    return text;
  }

  const size_t start = step % cycle;
  std::string window;
  window.reserve(visible);
  for (size_t offset = 0; offset < visible; offset++) {
    const size_t index = (start + offset) % cycle;
    window += index < text.size() ? text[index] : ' ';
  }
  return window;
}

}  // namespace makropad
