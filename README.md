# MakroPad

MakroPad is a custom ESP32-S3 based macro and media controller with mechanical
keyboard switches, analog sliders, and a 128x64 I2C OLED display.

The project is split into two main parts:

- ESP32-S3 firmware for reading hardware inputs, driving the OLED, and
  communicating with the host PC over USB.
- A desktop companion app for profile management, complex actions, media
  integration, and tray/background operation.

See [docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md) for the current requirements
and architecture. The current continuation notes live in
[docs/HANDOFF.md](docs/HANDOFF.md).

The first Arduino firmware prototype lives in
[firmware/MakroPadFirmware](firmware/MakroPadFirmware).

The desktop companion app lives in [app](app). It already includes a simulator,
real serial-port support, tray/background behavior, JSON configuration, action
execution, Windows media controls, system/app volume control, Now Playing OLED
updates, and an editable configuration UI.

## Current Status

Implemented:

- Arduino firmware prototype for up to 20 buttons, up to 3 configured sliders,
  OLED, USB Serial, heartbeat, action-result timeout, and configurable default
  screen placeholders.
- Avalonia desktop app prototype.
- Simulator connection for app development without electronics.
- Real COM-port connection using the documented serial protocol.
- Serial heartbeat with `ping`/`pong` and unresponsive connection status.
- Tray icon with open and exit commands.
- JSON config at `%APPDATA%\MakroPad\config.json`.
- Editable profile and binding overview in the UI.
- Windows media actions.
- Windows `system.volume` action.
- Windows `app.volume` action for active audio sessions.
- `system.openUrl`, `system.openApp`, `keyboard.shortcut`, and `log` actions.
- Configurable OLED default screen with preview and placeholders.
- Configurable OLED display triggers for buttons and sliders with conditions,
  preview, and normal text or lightweight HTML-like content rendered by the app.
- Configurable action-result display triggers replace the default hard-coded
  Action/Action failed screens while keeping firmware fallback compatibility.
- Configurable media-changed display triggers replace the default visible
  Now Playing screen while preserving firmware media-state synchronization.
- Initial Spotify Now Playing OLED display via the Spotify window title.
- Dark sidebar UI with sections for macros, device, simulation, settings, and
  debug.
- Debug view for active Windows audio sessions.
- Graphify project graph in `graphify-out/` for architecture navigation.
- Unit tests for protocol and action behavior.

Useful commands:

```powershell
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
dotnet run --project app\MakroPad.App\MakroPad.App.csproj
```
