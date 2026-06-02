# MakroPad App

Desktop companion app for MakroPad.

Current continuation notes are in `../docs/HANDOFF.md`.

## Projects

- `MakroPad.App`: Avalonia UI application.
- `MakroPad.Core`: shared protocol and configuration logic.
- `MakroPad.Device`: device communication abstractions.
- `MakroPad.Actions`: action execution abstractions.
- `MakroPad.Core.Tests`: protocol and core tests.
- `MakroPad.Actions.Tests`: action pipeline and executor tests.

## Current Prototype

The app currently uses a simulated MakroPad connection, so it can be developed
before the electronics are ready.

Implemented:

- Device panel with a simulator port and real serial ports.
- Connect and disconnect flow.
- Protocol log.
- Display message command using the serial protocol format.
- Simulated button and slider events.
- Protocol parser/formatter in `MakroPad.Core`.
- Serial connection implementation using `System.IO.Ports`.
- Tray icon with open and exit commands.
- JSON configuration model with a default profile.
- First action pipeline that matches device inputs to profile bindings.
- Slider action throttling to reduce noisy repeated execution.
- Unit tests for protocol encoding and action pipeline behavior.
- Windows media key actions.
- Windows `system.volume` action via CoreAudio.
- Windows `app.volume` action for per-process audio sessions via CoreAudio.
- Basic editable profile binding overview in the main UI.
- Action-specific config editor hints, placeholders, and basic validation.
- Known input selection for `button:1..20` and `slider:1..3`.
- Profile add, rename, and delete controls.
- Simulator input selection for button and slider IDs.
- Focused-window shortcut recorder for `keyboard.shortcut`.
- App/file picker for `system.openApp`.
- OLED Now Playing display for Spotify track changes using the Spotify window
  title.
- Dark blue sidebar UI with separate Makros, Geraet, Simulation, Einstellungen,
  and Debug sections.
- Structured main window with device, profile, simulator, bindings, and log
  sections.
- Auto-connect scanning for `hello;device=makropad-s3`.
- Configurable OLED action-result timeout.
- Configurable OLED default screen with profile, status, time, date, media,
  volume, and uptime placeholders.
- OLED default screen preview in the settings UI.
- OLED default screen layout can use either 3 or 5 lines.
- Configurable OLED display trigger list for buttons and sliders with normal
  text or lightweight HTML-like input rendered to OLED text lines.
- Debug view lists active Windows audio sessions for `app.volume` process
  discovery.
- Config editor shows structured parameter fields for common actions while
  keeping the raw parameter field available.
- Active audio sessions refresh automatically every few seconds and can still
  be refreshed manually from the Debug tab.
- Action-specific helper controls are shown only for matching actions, e.g.
  shortcut recording for `keyboard.shortcut` and file browsing for
  `system.openApp`.
- Serial heartbeat with `ping`/`pong` and `Unresponsive` connection state.

Supported actions:

- `media.playPause`
- `media.nextTrack`
- `media.previousTrack`
- `media.stop`
- `media.volumeUp`
- `media.volumeDown`
- `media.mute`
- `system.volume`
- `app.volume` with `process=Spotify`
- `system.openUrl` with `url=https://example.com`
- `system.openApp` with `path=notepad.exe`
- `keyboard.shortcut` with `keys=Ctrl+Alt+T`
- `log` with `message=Hello`

Default bindings:

- `button:1` -> `media.playPause`
- `slider:1` -> `system.volume`

Closing the main window hides it to the tray. Use the tray menu's `Exit` command
to terminate the app.

On first run, the app creates a default config at:

```text
%APPDATA%\MakroPad\config.json
```

When a real device sends `hello;proto=1;...`, the app automatically responds
with:

```text
hello-ack;proto=1;app=0.1.0
```

Incoming slider values are expected as `0..4095`. `system.volume` maps them to
Windows master volume `0..100%`. `app.volume` maps them to the volume of active
Windows audio sessions for the configured process name, for example
`process=Spotify`. Volume slider actions use a configurable percentage
deadband, defaulting to 2 percentage points, so small analog jitter does not
bounce between adjacent values like 99% and 100%.

The OLED default screen editor shows a small 128x64-style preview. Long lines
are clipped to match the firmware's current text fitting behavior. The editor
supports 3-line and 5-line layouts.

The settings UI also includes a display trigger editor similar to the macro
binding editor. It can manage multiple button and slider triggers, require a
configurable percentage change for slider triggers, and then show either normal
text or lightweight HTML-like content for a configured duration. The editor's
Test button sends the current editor content to the connected OLED immediately.
When multiple enabled triggers match the same input, the app sends their display
screens in list order and waits each trigger's duration before sending the next
screen. The editor provides Up/Down controls to change this sequence order.
Supported trigger placeholders are `{slider.id}`, `{slider.value}`,
`{slider.percent}`, `{input}`, `{profile}`, and `{status}`. HTML-like input is
rendered app-side to plain OLED text lines before it is sent to the firmware.

The app sends
`config;profile=...;actionResultTimeoutMs=...;defaultRows=...;defaultTitle=...;defaultLine1=...;defaultLine2=...;defaultLine3=...;defaultLine4=...;time=...;date=...;volumeSystem=...;preferredDisplay=...`
to the device after connecting and when saving the config. `0` disables the
automatic OLED reset for action results. The default screen supports
`{profile}`, `{status}`, `{time}`, `{date}`, `{media.artist}`, `{media.title}`,
`{media.track}`, `{volume.system}`, and `{device.uptime}` placeholders.

Now Playing is an initial implementation: it polls the Spotify desktop window
title on Windows and sends `display;mode=media;artist=...;title=...` when the
track changes. A later implementation should use Windows Media Session metadata
directly.
After temporary action-result messages, the firmware returns to the last media
screen while Now Playing is active. If the default screen template contains a
`{media.*}` placeholder, Now Playing updates the placeholder values instead of
switching to the dedicated media screen.

## Next Work

- Split the large `MainWindowViewModel` into smaller services/view models.
- Windows Media Session metadata for richer OLED media display updates.
- Linux implementations for media and volume actions.
- Richer config editor controls backed by shared action metadata.

## Run

```powershell
dotnet run --project app\MakroPad.App\MakroPad.App.csproj
```

## Build

```powershell
dotnet build app\MakroPad.sln
```

## Test

```powershell
dotnet test app\MakroPad.sln
```
