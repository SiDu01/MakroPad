#pragma once

#include <stdint.h>

#include <string>

// Connection to the companion app over USB serial or Bluetooth LE: handshake, heartbeat, message dispatch.
namespace hostlink {

void setup();
void poll(uint32_t nowMs);
bool connected();
void send(const std::string& line);

}  // namespace hostlink
