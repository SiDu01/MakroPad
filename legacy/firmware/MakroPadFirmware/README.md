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
- USB Mode: native USB/TinyUSB mode suitable for composite CDC + HID.
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

The firmware treats every known app message as a host heartbeat. If no app
message arrives for about 6 seconds, the OLED returns to `Waiting for app` and
the firmware starts sending `hello` again. Manual heartbeat test:

```text
ping;id=1
```

Expected response:

```text
pong;id=1
```

You can manually test the display:

```text
display;mode=message;text=Hello%20MakroPad
display;mode=profile;name=Default
display;mode=media;artist=Daft%20Punk;title=Digital%20Love
display;mode=media;artist=Daft%20Punk;title=Digital%20Love;preferred=false
display;mode=layout;layout=volume;durationMs=1500;title=Volume;line1=System;value=72
display;mode=layout;layout=action;durationMs=2000;title=Action;line1=Muted;icon=check
display;mode=clear
```

You can manually configure the OLED action-result timeout:

```text
config;profile=Default;actionResultTimeoutMs=3000;preferredDisplay=profile
config;profile=Default;defaultRows=3;defaultTitle=MakroPad;defaultTitleSize=2;defaultLine1=%7Bprofile%7D;defaultLine2=%7Bstatus%7D;time=18:42;date=01.06.2026;volumeSystem=40%25
config;profile=Default;defaultRows=5;defaultTitle=%7Btime%7D;defaultLine1=%7Bmedia.track%7D;defaultLine1Size=2;defaultLine2=Vol%20%7Bvolume.system%7D;defaultLine3=%7Bdate%7D;defaultLine4=Up%20%7Bdevice.uptime%7D
```

`action-result` messages are shown temporarily and then return to the idle
profile screen. `actionResultTimeoutMs=0` keeps action results on the display
until another display/config/action message arrives.
When `preferredDisplay=media` and a media screen has been received, action
results return to the last media screen instead.
The default profile screen can be customized with `defaultRows`, `defaultTitle`,
`defaultLine1`, `defaultLine2`, `defaultLine3`, and `defaultLine4`.
`defaultRows=3` uses title plus two lines. `defaultRows=5` uses all five
configured lines. The firmware supports these placeholders:
`{profile}`, `{status}`, `{time}`, `{date}`, `{media.artist}`, `{media.title}`,
`{media.track}`, `{volume.system}`, and `{device.uptime}`.
Default screen lines can use optional `defaultTitleSize` and
`defaultLine1Size`..`defaultLine4Size` values. Supported text sizes are `1` and
`2`; missing or invalid values fall back to `1`.
When the app is configured for HTML defaultscreen content, it strips the
lightweight HTML tags before sending `config` and maps `h1`/`h2` to size `2`.
Use `preferred=false` on `display;mode=media` to update media placeholder
values without switching the idle screen to the dedicated media display.
Long display lines scroll horizontally when they exceed 21 characters at the
current text size.
Temporary `display;mode=template` messages can provide optional
`line1Size`..`line5Size` values. Supported text sizes are `1` and `2`; missing
or invalid values fall back to `1`.
Structured `display;mode=layout` messages render small OLED screens with a
header, text, optional drawn icon, and for `layout=volume` a progress bar.
Supported layouts are `volume`, `media`, `action`, `profile`, and `status`.
Long layout text fields scroll horizontally on the OLED.

Button and slider events should appear as:

```text
button;id=1;state=down
button;id=1;state=up
slider;id=1;value=1834;raw=1841
```

Slider readings are filtered in firmware before they are sent. Each poll takes
multiple ADC samples, sorts them, averages the middle samples, applies a slow
low-pass filter, snaps near-endpoint values to `0`/`4095`, and only sends when
the value moved beyond the configured deadband and send interval. If a wired
slider still jitters, add a 100 nF capacitor from the slider wiper/GPIO to GND
near the ESP32 pin.

## App-disconnected HID fallback

When the companion app has not acknowledged the firmware for a short grace
period, the firmware enters a simple HID fallback mode. Serial protocol events
are still emitted, but the firmware also sends USB HID consumer-control events:

| Input | HID fallback |
| --- | --- |
| Button 1 | Play/Pause |
| Button 2 | Next track |
| Button 3 | Previous track |
| Button 4 | Stop |
| Button 5 | Mute |
| Button 6 | Volume down |
| Button 7 | Volume up |
| Slider 1 | Relative system volume |

Slider 1 uses throttled relative volume up/down HID nudges, so it cannot set an
exact OS volume percentage or mirror the physical slider position without the
app. Per-application volume, active-application volume, and "all except active
application" volume require host OS process and audio-session information and
therefore remain app-side features.

For fallback debugging, every HID fallback action also writes a
`hid-fallback;...` protocol line and shows a short OLED status. If the OLED/log
changes but the operating system does not react, check that the board was
flashed in a USB/TinyUSB mode that enumerates composite CDC + HID.

Additional buttons use the same protocol with their numeric IDs:

```text
button;id=12;state=down
button;id=12;state=up
```
