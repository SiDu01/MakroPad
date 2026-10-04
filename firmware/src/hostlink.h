#pragma once

#include <stdint.h>

#include <string>

// Serial connection to the companion app: handshake, heartbeat, message dispatch.
namespace hostlink {

void setup();
void poll(uint32_t nowMs);
bool connected();
void send(const std::string& line);

}  // namespace hostlink
