#include <Arduino.h>

#include "display.h"
#include "hid.h"
#include "hostlink.h"
#include "input.h"

// Wiring only; behaviour lives in the modules. See docs/PROTOCOL.md.

void setup() {
  hostlink::setup();
  hid::setup();
  input::setup();
  display::setup();
}

void loop() {
  const uint32_t now = millis();
  hostlink::poll(now);
  input::poll(now);
  hid::poll(now);
  display::poll(now);
  delay(1);
}
