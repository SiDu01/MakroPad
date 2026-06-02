# MakroPad Handoff

Last updated: 2026-06-02

## Current State

MakroPad has a working vertical prototype across firmware and desktop app:

- ESP32-S3 firmware reads 20 button inputs, supports up to 3 sliders, drives the
  SSD1306 OLED, speaks the serial protocol, and tracks host heartbeat state.
- The desktop app is an Avalonia/.NET app with tray behavior, simulator mode,
  real serial connections, auto-connect, config persistence, action execution,
  OLED config sync, Now Playing updates, and a dark sidebar UI.
- Config is stored at `%APPDATA%\MakroPad\config.json`.
- The serial protocol is documented in `docs/PROTOCOL.md`.
- Firmware wiring and pin choices are documented in `docs/HARDWARE.md`.
- Graphify outputs exist in `graphify-out/` and can be used for architecture
  orientation.

## Recent Milestones

- Added Windows `app.volume` action for setting the volume of a configured
  process/audio session.
- Added configurable OLED default screen with 3-line or 5-line layout.
- Added placeholders: `{profile}`, `{status}`, `{time}`, `{date}`,
  `{media.artist}`, `{media.title}`, `{media.track}`, `{volume.system}`, and
  `{device.uptime}`.
- Added OLED preview in settings.
- Added active audio session list in the debug view and automatic refresh every
  few seconds.
- Specialized the action editor for common actions while keeping raw parameter
  editing available.
- Added Graphify project graph and agent workflow guidance.

## Supported Actions

| Action | Parameters | Notes |
| --- | --- | --- |
| `media.playPause` | none | Windows media key |
| `media.nextTrack` | none | Windows media key |
| `media.previousTrack` | none | Windows media key |
| `media.stop` | none | Windows media key |
| `media.volumeUp` | none | Windows media key |
| `media.volumeDown` | none | Windows media key |
| `media.mute` | none | Windows media key |
| `system.volume` | none | Uses slider value `0..4095` |
| `app.volume` | `process=Spotify` | Uses slider value and active audio sessions |
| `system.openUrl` | `url=https://example.com` | Opens default browser |
| `system.openApp` | `path=notepad.exe` | Opens app/file via OS shell |
| `keyboard.shortcut` | `keys=Ctrl+Shift+Esc` | Sends supported key combinations |
| `log` | `message=Hello` | Writes to app log |

## OLED Behavior

- The app sends `config` messages after connect and when saving settings.
- `action-result` messages are shown temporarily.
- `actionResultTimeoutMs=0` disables automatic reset.
- With Now Playing active, action-result messages return to the media screen
  unless the default template itself contains `{media.*}` placeholders.
- If the default template contains `{media.*}`, Now Playing updates placeholder
  values without forcing the dedicated media screen.

## Known Limitations

- Now Playing currently reads the Spotify desktop window title. It works for the
  current Spotify test case, but Windows Media Session metadata would be more
  robust and app-agnostic.
- Linux implementations for media keys, volume, app volume, and Now Playing are
  still missing.
- `MainWindowViewModel` is the largest graph node and now owns UI state,
  connection flow, config editing, audio session refresh, and Now Playing.
  Splitting it will likely reduce future friction.
- Firmware is still a single Arduino sketch. That is fine for prototyping, but
  display/protocol/input modules should be split once behavior stabilizes.
- Slider quality is still being validated with replacement hardware.
- App UI is functional after the first redesign, but not yet a polished final
  editor experience.

## Verification Baseline

Use these commands after app/core/action changes:

```powershell
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
```

Firmware changes still need Arduino IDE compile/upload and a hardware test on
the ESP32-S3-DevKitC-1.

## Recommended Next Steps

1. Refactor `MainWindowViewModel` into smaller services/view models.
   Start with audio sessions, Now Playing, and OLED default screen state.
2. Replace Spotify-window-title Now Playing with Windows Media Session metadata.
3. Improve the config editor UX with action definitions shared between UI,
   validation, and future docs generation.
4. Split the firmware sketch into protocol, display, buttons, and sliders once
   the new sliders have been tested.
5. Add a small diagnostics screen for current device config, last heartbeat,
   last input, and last OLED message.

Recommendation: do the `MainWindowViewModel` split next. The project graph shows
it as the largest hub, and reducing that pressure will make the next UI and
Now Playing work easier.

## Graphify Notes

The graph artifacts are committed under `graphify-out/`:

- `graphify-out/graph.json`
- `graphify-out/GRAPH_REPORT.md`
- `graphify-out/GRAPH_TREE.html`

Useful graph entry points:

- `MainWindowViewModel` is the biggest god node.
- `SerialMakroPadConnection`, `MakroPad Firmware`, and `ActionExecutor` are the
  next most important implementation hubs.
- The most useful architecture question right now is: how does an input event
  flow from firmware through serial protocol into the app action pipeline?
