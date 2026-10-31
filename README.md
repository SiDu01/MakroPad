# MakroPad

Selbstgebautes Makro- und Mediapad auf Basis eines ESP32-S3-DevKitC-1:
16 mechanische Tasten, 3 analoge Slider und ein 128×64-OLED, per USB mit einer
Windows-Companion-App verbunden.

> Taste oder Slider bedienen → am PC passiert zuverlässig etwas → das OLED
> bestätigt es kurz.

## Status

Version 0.4.1. Funktioniert auf der Hardware:

- 16 Tasten und 3 Slider mit Aktionen (Medien, System-/Programm-/Vordergrund-
  Lautstärke, Tastenkombinationen, Webseiten, Programme, Profile)
- OLED mit Uhr, Profil, laufendem Titel und kurzen Einblendungen
- Profile mit automatischem Wechsel nach Programm im Vordergrund
- Medientasten und Lautstärke auch ohne laufende App (USB-HID)
- Windows-App mit Pad-Editor, Live-Spiegel des Displays, Tray und Autostart

Plan und nächste Meilensteine: [docs/REWRITE_PLAN.md](docs/REWRITE_PLAN.md).
Der alte Stand (0.2.0) ist über das Git-Tag `legacy-v0.2.0` erreichbar.

## Aufbau

| Ordner | Inhalt |
| --- | --- |
| `firmware/` | PlatformIO-Projekt (Arduino-Framework) |
| `app/` | .NET-10-/Avalonia-App für Windows |
| `hardware/case/` | Parametrisches OpenSCAD-Gehäuse mit STL-Exporten ([Anleitung](hardware/case/README.md)) |
| `installer/` | Inno-Setup-Installer und Release-Skript |
| `docs/` | [Konfiguration](docs/CONFIG.md), [Protokoll v2](docs/PROTOCOL.md), [Hardware/Pinout](docs/HARDWARE.md), [Gestaltung](docs/UI_DESIGN.md) |

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
dotnet run --project app\MakroPad.App -- --simulator   # ohne Hardware, nur mit debugMode

# Release: Installer + Firmware-Image nach release/
.\installer\build-release.ps1
```

Voraussetzungen: PlatformIO Core, .NET SDK 10, für die nativen Firmware-Tests
ein Host-gcc (z. B. WinLibs), für den Installer Inno Setup 6.
