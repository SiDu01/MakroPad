# MakroPad Firmware

Arduino firmware for the first ESP32-S3 prototype.

## Target

- Board: ESP32-S3-DevKitC-1.
- Arduino IDE / Arduino ESP32 core.
- First prototype hardware:
  - Button 1: GPIO4 to GND, using `INPUT_PULLUP`.
  - Slider 1: GPIO1 / ADC1_CH0, powered from 3.3 V.
  - OLED SDA: GPIO8.
  - OLED SCL: GPIO9.

## Required Arduino Libraries

Install these through the Arduino Library Manager:

- `Adafruit SSD1306`
- `Adafruit GFX Library`

The firmware also uses the built-in `Wire` library.

## Recommended Arduino Board Settings

Exact menu names can differ between Arduino ESP32 core versions, but these are
the intended settings:

- Board: `ESP32S3 Dev Module` or the matching ESP32-S3-DevKitC-1 entry if
  available.
- USB CDC On Boot: `Enabled`.
- USB Mode: USB CDC or USB-OTG mode suitable for Serial over native USB.
- Upload Mode: use the port that works for your board, usually the USB-to-UART
  port first.
- Serial Monitor baud: `115200`.

## First Test

After flashing, open the Serial Monitor at `115200`.

The firmware writes protocol output to both Arduino `Serial` and `Serial0`.
This makes the first hardware tests easier on ESP32-S3 boards that expose both
native USB CDC and a USB-to-UART bridge. If one COM port only shows ESP-ROM boot
messages and no `hello` lines, try the other COM port.

Expected output:

```text
hello;proto=1;fw=0.1.0;device=makropad-s3;buttons=1;sliders=1;display=ssd1306-128x64
ready;uptime=...
```

You can manually acknowledge the device:

```text
hello-ack;proto=1;app=manual
```

You can manually test the display:

```text
display;mode=message;text=Hello%20MakroPad
display;mode=profile;name=Default
display;mode=media;artist=Daft%20Punk;title=Digital%20Love
display;mode=clear
```

Button and slider events should appear as:

```text
button;id=1;state=down
button;id=1;state=up
slider;id=1;value=1834;raw=1841
```
