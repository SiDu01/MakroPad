# MakroPad Agent Workflow

Diese Datei beschreibt, wie KI-Assistenten (Codex, Claude Code) in diesem Repository arbeiten sollen.

## Sprache und Zusammenarbeit

- Antworte auf Deutsch.
- Arbeite proaktiv weiter, wenn der naechste Schritt aus dem Kontext klar ist.
- Frage nur nach, wenn eine Entscheidung nicht sinnvoll aus dem Projektkontext ableitbar ist.
- Fasse abgeschlossene Arbeit knapp zusammen.
- Schlage nach jedem abgeschlossenen Arbeitspaket direkt sinnvolle naechste Schritte vor.
- Halte den Status in `docs/REWRITE_PLAN.md` und die betroffenen Dokus in `docs/` nach groesseren Meilensteinen aktuell.

## Entwicklung

- Bevor Code geaendert wird, kurz die relevanten Dateien lesen.
- Bestehende Projektstruktur und Patterns bevorzugen.
- Aendere nur Dateien, die fuer die Aufgabe relevant sind.
- Firmware, App und Doku zusammenhalten, wenn ein Feature das Protokoll oder OLED-Verhalten betrifft.

## Tests und Verifikation

Stand und Meilensteine: `docs/REWRITE_PLAN.md`. Der alte Stand (0.2.0) ist nur
noch ueber das Git-Tag `legacy-v0.2.0` erreichbar.

- Nach App-Aenderungen (vor dem Start der App fuer einen Test immer die ganze
  Solution bauen, `dotnet test` baut `MakroPad.App` nicht mit):

```powershell
dotnet build app\MakroPad.sln
dotnet test app\MakroPad.sln
```

- Nach Firmware-Aenderungen (PlatformIO, im Ordner `firmware/`):

```powershell
pio run
pio test -e native
```

- Hardware-Verhalten (Flash, OLED, Slider) muss am Geraet getestet werden; im Abschluss klar sagen, wenn das noch offen ist.
- Testhilfen der App (`--simulator`, `--screenshot`, `--selftest-recorder`, `--config-dir`) wirken nur mit `"debugMode": true` in der verwendeten Config.
- Release: `installer\build-release.ps1` (Installer + Firmware-Image nach `release/`).
- Bei UI-Aenderungen auf kompilierte Avalonia-Bindings achten; ein erfolgreicher Build ist Pflicht.

## Git

- Nach abgeschlossenen Arbeitspaketen committen.
- Keine fremden oder unzusammenhaengenden Aenderungen zuruecksetzen.
- Commit-Messages kurz und beschreibend halten.

## Projektfokus

Leitlinie: Taste oder Slider bedienen -> am PC passiert zuverlaessig etwas ->
das OLED bestaetigt es kurz. Features ausserhalb des aktuellen Meilensteins in
`docs/REWRITE_PLAN.md` nicht vorziehen.

- Firmware: PlatformIO, Arduino-Framework, modular, reine Logik in
  `firmware/lib/makropad_core` mit nativen Tests.
- App: C#/.NET 10 + Avalonia, nur Windows.
- Protokoll v2 (`docs/PROTOCOL.md`), Display als feste Firmware-Screens.

## Abschlussformat

Am Ende eines erledigten Arbeitspakets:

- Nenne kurz, was umgesetzt wurde.
- Nenne Build-/Testergebnis.
- Nenne den Commit-Hash, falls committed wurde.
- Schlage 2-5 konkrete naechste Schritte vor, mit einer Empfehlung.
- Erwaehne, wenn Dokus in `docs/` aktualisiert wurden.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, invoke the `skill` tool with `skill: "graphify"` before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
