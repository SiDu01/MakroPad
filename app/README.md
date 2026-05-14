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
