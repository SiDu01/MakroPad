#pragma once

#include <stddef.h>
#include <stdint.h>

namespace makropad {

// "Hold `holdId`, then press `triggerId`". The hold button is held back until release so a
// combo sends nothing; without a combo it is forwarded as down + up on release.
class ButtonCombo {
 public:
  struct Event {
    uint8_t id;
    bool pressed;
  };

  ButtonCombo(uint8_t holdId, uint8_t triggerId) : holdId_(holdId), triggerId_(triggerId) {}

  // Feeds a debounced change. `out` receives up to two events to forward, `count` how many.
  // Returns true when the combo fired.
  bool update(uint8_t id, bool pressed, Event out[2], size_t& count) {
    count = 0;
    if (id == holdId_) {
      if (pressed) {
        holding_ = true;
        used_ = false;
      } else {
        holding_ = false;
        if (!used_) {
          out[count++] = {id, true};
          out[count++] = {id, false};
        }
      }
      return false;
    }
    if (id == triggerId_ && pressed && holding_) {
      used_ = true;
      swallowTriggerUp_ = true;
      return true;
    }
    if (id == triggerId_ && !pressed && swallowTriggerUp_) {
      swallowTriggerUp_ = false;
      return false;
    }
    out[count++] = {id, pressed};
    return false;
  }

 private:
  uint8_t holdId_;
  uint8_t triggerId_;
  bool holding_ = false;
  bool used_ = false;
  bool swallowTriggerUp_ = false;
};

}  // namespace makropad
