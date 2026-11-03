#pragma once

#include <stdint.h>

#include <string>

// Bluetooth LE link to the companion app: Nordic UART Service, bonded and encrypted.
// Pairing shows a passkey on the OLED.
namespace ble {

void setup();
void poll(uint32_t nowMs);
// True while a bonded client has subscribed to notifications.
bool connected();
// Next received byte; false when none is pending.
bool read(char& c);
void write(const std::string& data);

}  // namespace ble
