#pragma once

#include <stdint.h>

#include <string>

// Connection to the companion app over USB serial or Bluetooth LE: handshake, heartbeat, message dispatch.
namespace hostlink {

void setup();
void poll(uint32_t nowMs);
bool connected();
// Pins the connection to the other channel (USB <-> Bluetooth) until called again or reboot.
void switchChannel(uint32_t nowMs);
void send(const std::string& line);

}  // namespace hostlink
