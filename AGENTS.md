# MakroPad Agent Workflow

Diese Datei beschreibt, wie Codex in diesem Repository arbeiten soll.

## Sprache und Zusammenarbeit

- Antworte auf Deutsch.
- Arbeite proaktiv weiter, wenn der naechste Schritt aus dem Kontext klar ist.
- Frage nur nach, wenn eine Entscheidung nicht sinnvoll aus dem Projektkontext ableitbar ist.
- Fasse abgeschlossene Arbeit knapp zusammen.
- Schlage nach jedem abgeschlossenen Arbeitspaket direkt sinnvolle naechste Schritte vor.
- Halte `docs/HANDOFF.md` nach groesseren Meilensteinen aktuell.

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
- Handoff und Graphify als Orientierung fuer laengere Entwicklungsabschnitte.

## Abschlussformat

Am Ende eines erledigten Arbeitspakets:

- Nenne kurz, was umgesetzt wurde.
- Nenne Build-/Testergebnis.
- Nenne den Commit-Hash, falls committed wurde.
- Schlage 2-5 konkrete naechste Schritte vor, mit einer Empfehlung.
- Erwaehne, wenn `docs/HANDOFF.md` oder andere Dokus aktualisiert wurden.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, invoke the `skill` tool with `skill: "graphify"` before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
