# MakroPad App

Desktop companion app for MakroPad.

## Projects

- `MakroPad.App`: Avalonia UI application.
- `MakroPad.Core`: shared protocol and configuration logic.
- `MakroPad.Device`: device communication abstractions.
- `MakroPad.Actions`: action execution abstractions.

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
- Windows `media.playPause` action via the system media key.
- Windows `system.volume` action via CoreAudio.
- Read-only profile binding overview in the main UI.

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
