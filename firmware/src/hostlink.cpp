#include "hostlink.h"

#include <Arduino.h>

#include "ble.h"
#include "config.h"
#include "display.h"
#include "hid.h"
#include "protocol.h"

using makropad::LineBuffer;
using makropad::LineBuilder;
using makropad::ProtocolLine;

namespace hostlink {

namespace {

enum class Channel { None, Usb, Ble };

LineBuffer usbBuffer(config::MaxLineLength);
LineBuffer bleBuffer(config::MaxLineLength);
// The channel the app acknowledged on; until then output goes to both.
Channel activeChannel = Channel::None;
// Set with the button combo; the other channel is ignored until the next switch or reboot.
Channel pinnedChannel = Channel::None;
bool hostConnected = false;
uint32_t lastHostSeenMs = 0;
uint32_t lastHelloMs = 0;
bool helloSent = false;

void writeUsb(const std::string& line) {
  Serial.print(line.c_str());
  Serial.print('\n');
}

std::string helloLine() {
  return LineBuilder("hello")
      .add("proto", static_cast<long>(config::ProtocolVersion))
      .add("fw", config::FirmwareVersion)
      .add("device", config::DeviceName)
      .add("buttons", static_cast<long>(config::ButtonCount))
      .add("sliders", static_cast<long>(config::ActiveSliderCount))
      .add("display", "ssd1306-128x64")
      .str();
}

// Where output goes: the acknowledged channel, else the pinned one, else both (None).
Channel outputChannel() {
  return activeChannel != Channel::None ? activeChannel : pinnedChannel;
}

void setConnected(bool value, uint32_t nowMs) {
  if (hostConnected == value) {
    return;
  }
  hostConnected = value;
  display::setHostConnected(value, nowMs);
  hid::setHostConnected(value);
}

void handleLine(const std::string& raw, Channel channel, uint32_t nowMs) {
  const ProtocolLine line = ProtocolLine::parse(raw);
  const std::string& type = line.type();

  if (type == "hello-ack") {
    if (line.getInt("proto", 0) != config::ProtocolVersion) {
      send(LineBuilder("error").add("code", "unsupported_protocol").add("message", "Expected proto=2").str());
      return;
    }
    if (pinnedChannel != Channel::None && channel != pinnedChannel) {
      return;
    }
    // USB has priority: a late Bluetooth ack must not take over an app on USB.
    if (channel == Channel::Ble && activeChannel == Channel::Usb) {
      return;
    }
    lastHostSeenMs = nowMs;
    activeChannel = channel;
    setConnected(true, nowMs);
    return;
  }

  // Everything below requires a completed handshake on this channel.
  if (!hostConnected || channel != activeChannel) {
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
  ble::setup();
}

void poll(uint32_t nowMs) {
  ble::poll(nowMs);

  std::string line;
  while (Serial.available() > 0) {
    if (usbBuffer.feed(static_cast<char>(Serial.read()), line)) {
      handleLine(line, Channel::Usb, nowMs);
    }
  }
  char c;
  while (ble::read(c)) {
    if (bleBuffer.feed(c, line)) {
      handleLine(line, Channel::Ble, nowMs);
    }
  }
  if (usbBuffer.takeOverflow() || bleBuffer.takeOverflow()) {
    send(LineBuilder("error").add("code", "line_too_long").str());
  }

  const bool bleDropped = activeChannel == Channel::Ble && !ble::connected();
  if (hostConnected && (bleDropped || nowMs - lastHostSeenMs >= config::HostTimeoutMs)) {
    activeChannel = Channel::None;
    setConnected(false, nowMs);
  }

  // Over Bluetooth the device keeps offering itself on USB, so an app there can take over.
  const bool offerHello = !hostConnected || (activeChannel == Channel::Ble && pinnedChannel == Channel::None);
  if (offerHello && (!helloSent || nowMs - lastHelloMs >= config::HelloIntervalMs)) {
    helloSent = true;
    lastHelloMs = nowMs;
    if (hostConnected) {
      writeUsb(helloLine());
    } else {
      send(helloLine());
    }
  }
}

bool connected() {
  return hostConnected;
}

void switchChannel(uint32_t nowMs) {
  const Channel current = outputChannel();
  // Without any channel yet, USB counts as current because it has priority.
  pinnedChannel = current == Channel::Ble ? Channel::Usb : Channel::Ble;
  if (activeChannel != pinnedChannel) {
    activeChannel = Channel::None;
    setConnected(false, nowMs);
    helloSent = false;  // offer the new channel right away
  }
  display::showStatus("Verbindung", pinnedChannel == Channel::Ble ? "Bluetooth" : "USB", display::Icon::Sync, nowMs);
}

void send(const std::string& line) {
  const Channel target = outputChannel();
  if (target != Channel::Ble) {
    writeUsb(line);
  }
  if (target != Channel::Usb) {
    ble::write(line + '\n');
  }
}

}  // namespace hostlink
