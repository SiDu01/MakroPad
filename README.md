# MakroPad

Selbstgebautes Makro- und Mediapad auf Basis eines ESP32-S3-DevKitC-1:
16 mechanische Tasten, 3 analoge Slider und ein 128×64-OLED, per USB mit einer
Windows-Companion-App verbunden.

> Taste oder Slider bedienen → am PC passiert zuverlässig etwas → das OLED
> bestätigt es kurz.

## Status

Das Projekt wird neu aufgebaut (Branch `rewrite`). Plan und Meilensteine:
[docs/REWRITE_PLAN.md](docs/REWRITE_PLAN.md), Bestandsaufnahme des alten
Stands: [docs/REWRITE_NOTES.md](docs/REWRITE_NOTES.md).

Der alte Stand (App/Firmware 0.2.0) liegt zum Nachschlagen in `legacy/` und ist
als Git-Tag `legacy-v0.2.0` markiert.

## Aufbau

| Ordner | Inhalt |
| --- | --- |
| `firmware/` | PlatformIO-Projekt (Arduino-Framework) |
| `app/` | .NET-10-/Avalonia-App für Windows |
| `hardware/case/` | Parametrisches OpenSCAD-Gehäuse |
| `docs/` | [Protokoll v2](docs/PROTOCOL.md), [Hardware/Pinout](docs/HARDWARE.md) |

## Bauen und testen

```powershell
# Firmware (im Ordner firmware/)
pio run
pio run -t upload
pio test -e native

# App
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
dotnet run --project app\MakroPad.App
```

Voraussetzungen: PlatformIO Core, .NET SDK 10, für die nativen Firmware-Tests
ein Host-gcc (z. B. WinLibs).
