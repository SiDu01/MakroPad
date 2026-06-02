# MakroPad Agent Workflow

Diese Datei beschreibt, wie Codex in diesem Repository arbeiten soll.

## Sprache und Zusammenarbeit

- Antworte auf Deutsch.
- Arbeite proaktiv weiter, wenn der naechste Schritt aus dem Kontext klar ist.
- Frage nur nach, wenn eine Entscheidung nicht sinnvoll aus dem Projektkontext ableitbar ist.
- Fasse abgeschlossene Arbeit knapp zusammen.
- Schlage nach jedem abgeschlossenen Arbeitspaket direkt sinnvolle naechste Schritte vor.

## Entwicklung

- Bevor Code geaendert wird, kurz die relevanten Dateien lesen.
- Bestehende Projektstruktur und Patterns bevorzugen.
- Aendere nur Dateien, die fuer die Aufgabe relevant sind.
- Firmware, App und Doku zusammenhalten, wenn ein Feature das Protokoll oder OLED-Verhalten betrifft.
- Fuer manuelle Datei-Edits `apply_patch` verwenden.

## Tests und Verifikation

- Nach App-/Core-/Action-Aenderungen ausfuehren:

```powershell
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
```

- Firmware kann in dieser Umgebung meist nicht mit der Arduino-Toolchain kompiliert werden. Wenn Firmware geaendert wurde, im Abschluss klar sagen, dass ein Flash-/Hardwaretest noetig ist.
- Bei UI-Aenderungen auf kompilierte Avalonia-Bindings achten; ein erfolgreicher Build ist Pflicht.

## Git

- Nach abgeschlossenen Arbeitspaketen committen.
- Keine fremden oder unzusammenhaengenden Aenderungen zuruecksetzen.
- Commit-Messages kurz und beschreibend halten.

## Projektfokus

Aktuelle Schwerpunkte:

- ESP32-S3 Firmware fuer Buttons, Slider und OLED.
- Avalonia Companion App fuer Windows, perspektivisch Linux.
- Serial-Protokoll zwischen App und MakroPad.
- Konfigurierbare Makros, App-/System-Actions und OLED-Defaultscreen.
- Gute Debugbarkeit fuer Audio-Sessions, Device-Verbindung und Display-Zustand.

## Abschlussformat

Am Ende eines erledigten Arbeitspakets:

- Nenne kurz, was umgesetzt wurde.
- Nenne Build-/Testergebnis.
- Nenne den Commit-Hash, falls committed wurde.
- Schlage 2-5 konkrete naechste Schritte vor, mit einer Empfehlung.
