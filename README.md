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
- Multiple configurable idle screens that switch by activation: a default
  screen, app/process-specific screens that take priority while their process
  runs, and rotation through several active screens at an adjustable interval
  (driven app-side, no firmware change).
- Display content can be authored as plain text, a real HTML subset, or a
  Markdown-like pseudocode, including a visual block builder ("Baukasten").
  Markdown/block content supports blank lines and per-line alignment
  (left/center/right); the firmware centers/right-aligns short lines.
- Configurable OLED display triggers for buttons and sliders with conditions,
  preview, and text/HTML/Markdown content rendered by the app.
- Configurable action-result display triggers replace the default hard-coded
  Action/Action failed screens while keeping firmware fallback compatibility.
- Configurable media-changed display triggers replace the default visible
  Now Playing screen while preserving firmware media-state synchronization.
- Configurable connection-changed display triggers can react to connected,
  connecting, unresponsive, and disconnected states.
- Configurable profile-changed display triggers can show custom profile switch
  screens.
- Display trigger presets, editable preview values, match diagnostics, and
  independently accumulated slider thresholds make trigger authoring testable
  without generating every real device event.
- Initial Spotify Now Playing OLED display via the Spotify window title.
- Device-centric UI rebuilt around the physical pad: a Pad screen with a
  20-key grid and 3 slider strips, an Edit/Test mode toggle, live input
  highlighting, an inline profile bar, and a contextual binding inspector.
  Workflow-ordered navigation (Pad, Display, Verbindung, Einstellungen,
  Diagnose), a persistent live OLED preview in the Display designer, a runtime
  light/dark theme toggle, and a MakroPad app icon.
- Debug view for active Windows audio sessions.
- Graphify project graph in `graphify-out/` for architecture navigation.
- Unit tests for protocol and action behavior.

Useful commands:

```powershell
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
dotnet run --project app\MakroPad.App\MakroPad.App.csproj
```
