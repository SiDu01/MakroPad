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

The desktop companion app lives in [app](app). It already includes a simulator,
real serial-port support, tray/background behavior, JSON configuration, a first
action pipeline, Windows media play/pause, and Windows master-volume control.

## Current Status

Implemented:

- Arduino firmware prototype for 1 button, 1 slider, OLED, and USB Serial.
- Avalonia desktop app prototype.
- Simulator connection for app development without electronics.
- Real COM-port connection using the documented serial protocol.
- Tray icon with open and exit commands.
- JSON config at `%APPDATA%\MakroPad\config.json`.
- Read-only profile binding overview in the UI.
- Windows `media.playPause` action.
- Windows `system.volume` action.
- Unit tests for protocol and action behavior.

Useful commands:

```powershell
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
dotnet run --project app\MakroPad.App\MakroPad.App.csproj
```
