# MakroPad

MakroPad is a custom ESP32-S3 based macro and media controller with mechanical
keyboard switches, analog sliders, and a 128x64 I2C OLED display.

The project is split into two main parts:

- ESP32-S3 firmware for reading hardware inputs, driving the OLED, and
  communicating with the host PC over USB.
- A desktop companion app for profile management, complex actions, media
  integration, and tray/background operation.

See [docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md) for the current requirements,
architecture, and proposed project structure.

The first Arduino firmware prototype lives in
[firmware/MakroPadFirmware](firmware/MakroPadFirmware).

The first desktop app prototype lives in [app](app).
