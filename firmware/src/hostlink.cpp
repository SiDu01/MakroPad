#include "hostlink.h"

#include <Arduino.h>

#include "config.h"
#include "display.h"
#include "hid.h"
#include "protocol.h"

using makropad::LineBuffer;
using makropad::LineBuilder;
using makropad::ProtocolLine;

namespace hostlink {

namespace {

LineBuffer lineBuffer(config::MaxLineLength);
bool hostConnected = false;
uint32_t lastHostSeenMs = 0;
uint32_t lastHelloMs = 0;
bool helloSent = false;

void sendHello() {
  send(LineBuilder("hello")
           .add("proto", static_cast<long>(config::ProtocolVersion))
           .add("fw", config::FirmwareVersion)
           .add("device", config::DeviceName)
           .add("buttons", static_cast<long>(config::ButtonCount))
           .add("sliders", static_cast<long>(config::ActiveSliderCount))
           .add("display", "ssd1306-128x64")
           .str());
}

void setConnected(bool value, uint32_t nowMs) {
  if (hostConnected == value) {
    return;
  }
  hostConnected = value;
  display::setHostConnected(value, nowMs);
  hid::setHostConnected(value);
}

void handleLine(const std::string& raw, uint32_t nowMs) {
  const ProtocolLine line = ProtocolLine::parse(raw);
  const std::string& type = line.type();

  if (type == "hello-ack") {
    if (line.getInt("proto", 0) != config::ProtocolVersion) {
      send(LineBuilder("error").add("code", "unsupported_protocol").add("message", "Expected proto=2").str());
      return;
    }
    lastHostSeenMs = nowMs;
    setConnected(true, nowMs);
    return;
  }

  // Everything below requires a completed handshake.
  if (!hostConnected) {
    return;
  }

  if (type == "ping") {
    LineBuilder pong("pong");
    if (line.has("id")) {
      pong.add("id", line.get("id"));
    }
    send(pong.str());
  } else if (type == "state") {
    display::applyState(line, nowMs);
  } else if (type == "screen") {
    display::showScreen(line, nowMs);
  } else if (type == "clear") {
    display::clearOverlay(nowMs);
  } else if (type == "display") {
    display::setEnabled(line.get("on") != "0");
  } else {
    return;  // unknown types do not count as a heartbeat
  }

  lastHostSeenMs = nowMs;
}

}  // namespace

void setup() {
  Serial.begin(config::SerialBaud);
}

void poll(uint32_t nowMs) {
  std::string line;
  while (Serial.available() > 0) {
    if (lineBuffer.feed(static_cast<char>(Serial.read()), line)) {
      handleLine(line, nowMs);
    }
  }
  if (lineBuffer.takeOverflow()) {
    send(LineBuilder("error").add("code", "line_too_long").str());
  }

  if (hostConnected && nowMs - lastHostSeenMs >= config::HostTimeoutMs) {
    setConnected(false, nowMs);
  }

  if (!hostConnected && (!helloSent || nowMs - lastHelloMs >= config::HelloIntervalMs)) {
    helloSent = true;
    lastHelloMs = nowMs;
    sendHello();
  }
}

bool connected() {
  return hostConnected;
}

void send(const std::string& line) {
  Serial.print(line.c_str());
  Serial.print('\n');
}

}  // namespace hostlink
