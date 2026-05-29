# MakroPad Firmware

Arduino firmware for the first ESP32-S3 prototype.

## Target

- Board: ESP32-S3-DevKitC-1.
- Arduino IDE / Arduino ESP32 core.
- Supported prototype hardware:
  - Up to 20 buttons, GPIO to GND, using `INPUT_PULLUP`.
  - Up to 3 sliders on GPIO1/GPIO2/GPIO3, powered from 3.3 V.
  - OLED SDA: GPIO8.
  - OLED SCL: GPIO9.

The firmware currently enables all 20 button inputs. Only slider 1 is enabled
by default because disconnected ADC pins can float and spam noisy events. Once
slider 2 and slider 3 are wired, change `ActiveSliderCount` in the sketch from
`1` to `3`.

## Pinout

Buttons:

| Button | GPIO |
| --- | ---: |
| B01 | GPIO4 |
| B02 | GPIO5 |
| B03 | GPIO6 |
| B04 | GPIO7 |
| B05 | GPIO10 |
| B06 | GPIO11 |
| B07 | GPIO12 |
| B08 | GPIO13 |
| B09 | GPIO14 |
| B10 | GPIO15 |
| B11 | GPIO16 |
| B12 | GPIO17 |
| B13 | GPIO18 |
| B14 | GPIO21 |
| B15 | GPIO39 |
| B16 | GPIO40 |
| B17 | GPIO41 |
| B18 | GPIO42 |
| B19 | GPIO47 |
| B20 | GPIO48 |

Sliders:

| Slider | GPIO |
| --- | ---: |
| Slider 1 | GPIO1 |
| Slider 2 | GPIO2 |
| Slider 3 | GPIO3 |

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
hello;proto=1;fw=0.2.0;device=makropad-s3;buttons=20;sliders=1;display=ssd1306-128x64
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

You can manually configure the OLED action-result timeout:

```text
config;profile=Default;actionResultTimeoutMs=3000
```

`action-result` messages are shown temporarily and then return to the idle
profile screen. `actionResultTimeoutMs=0` keeps action results on the display
until another display/config/action message arrives.

Button and slider events should appear as:

```text
button;id=1;state=down
button;id=1;state=up
slider;id=1;value=1834;raw=1841
```

Additional buttons use the same protocol with their numeric IDs:

```text
button;id=12;state=down
button;id=12;state=up
```
