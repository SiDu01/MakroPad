# Display Design

Last updated: 2026-06-04

## Ziel

Das OLED soll nicht nur lose Textzeilen anzeigen, sondern kleine, erkennbare
Screens. Die App entscheidet weiterhin, welcher Inhalt gerade wichtig ist; die
Firmware rendert daraus robuste OLED-Primitives.

## Prinzip

Version 1 bleibt absichtlich text- und key-value-basiert. Neu ist ein
Layout-Modus:

```text
display;mode=layout;layout=volume;durationMs=1500;title=Volume;line1=System;value=72
```

Der Modus ist kein Bitmap-Streaming. Die App sendet semantische Felder wie
`layout`, `title`, `line1`, `line2`, `value` und `icon`. Die Firmware zeichnet
daraus Header, Text, kleine Symbol-Primitives und bei Bedarf einen
Fortschrittsbalken.

## Unterstuetzte Layouts

| Layout | Felder | Verhalten |
| --- | --- | --- |
| `volume` | `title`, `line1`, `value` | Header, Label, grosse Prozentzahl und Balken |
| `media` | `title`, `line1`, `line2` | Header plus zwei Inhaltszeilen |
| `action` | `title`, `line1`, `line2`, `icon` | Header, Symbol und Meldung |
| `profile` | `title`, `line1`, `line2`, `icon` | Header, Symbol und Profiltext |
| `status` | `title`, `line1`, `line2`, `icon` | Header, Symbol und Statusmeldung |

Unbekannte Layouts fallen firmwareseitig auf das Status-Layout zurueck.

## App-Verhalten

Display-Trigger haben nun drei Template-Modi:

- `normal`: bestehende Textzeilen.
- `html`: leichtes HTML wird appseitig zu Textzeilen und Groessen gerendert.
- `layout`: die App rendert aus Trigger-Kontext und Template einen
  `display;mode=layout` Screen.

Bei `layout` bleibt das Trigger-Template als inhaltliche Vorlage erhalten:

```text
Volume
System
```

Die App leitet den Screen-Typ aus dem Event ab:

- Slider-Input mit Prozentwert -> `volume`.
- `actionResult` -> `action`.
- `mediaChanged` -> `media`.
- `connectionChanged` -> `status`.
- `profileChanged` -> `profile`.

Der Display-Trigger-Editor zeigt fuer `templateMode=layout` eine strukturierte
Preview mit Header, Iconbereich, Text und Prozentbalken. Der Test-Button sendet
Layout-Trigger ebenfalls als `display;mode=layout`.

Der `Display`-Bereich ist eine Uebersicht mit zwei Listen plus je einer
dedizierten Editor-Seite:

- `Idle-Screens`: mehrere Idle-Screens, die abhaengig von Bedingungen wechseln
  (siehe „Idle-Screen-Aktivierung") und bei mehreren aktiven rotieren (globales
  Intervall). Die App rendert den aktiven Screen app-seitig und pusht ihn per
  `config` an die Firmware (kein Firmware-Wechsel noetig).
- `Anzeige-Regeln`: ereignisgesteuerte, temporaere Screens.
- „Erstellen"/„Bearbeiten" oeffnet jeweils eine eigene Editor-Seite mit
  Inhalts-Editor und Live-OLED-Vorschau.

Inhalte (Idle-Screens und Regeln) lassen sich als Klartext, echtes HTML-Subset
oder Markdown-Pseudocode schreiben, inklusive visuellem Baukasten, Leerzeilen
und zeilenweiser Ausrichtung (links/mittig/rechts).

## Idle-Screen-Aktivierung

Jeder Idle-Screen traegt eine Liste von Bedingungen plus Verhaltensregeln. Der
`IdleScreenOrchestrator` wertet sie sekuendlich gegen einen `SystemStateSnapshot`
aus (Auswertung komplett app-seitig; die Firmware sieht nur den fertigen Screen).

Bedingungstypen (`IdleScreenCondition.Type`):

- `processRunning` — Prozess `Target` laeuft.
- `processForeground` — Prozess `Target` ist das fokussierte Fenster.
- `fullscreen` — das Vordergrundfenster fuellt den Monitor (optional auf `Target`
  eingeschraenkt; Desktop/Shell zaehlen nicht als Vollbild).
- `mediaActive` — Prozess `Target` gibt gerade Audio aus (aktive Audio-Session).
- `systemMetric` — `Metric` (cpu/memory/battery/network) vergleicht via
  `Comparison` (gt/gte/lt/lte) gegen `Threshold`. `network` ist Kbit/s; `battery`
  meldet auf Geraeten ohne Akku 100.

Jede Bedingung kann per `Negate` invertiert werden. Mehrere Bedingungen werden
ueber `ConditionLogic` (`and`/`or`) verknuepft; eine leere Liste bedeutet „immer
aktiv" (klassischer Standard-Screen).

Auswahl-Praezedenz pro Tick:

1. Ein Screen ist *berechtigt*, wenn er aktiviert ist und seine Bedingungen
   gelten. Die Bedingungen muessen `ActivationDelaySeconds` lang durchgehend
   gelten, bevor sie zaehlen (unterdrueckt kurze Blips).
2. Anti-Flacker: Der aktuell gezeigte Screen wird bis zum Ablauf seiner
   `MinDwellSeconds` gehalten, auch wenn seine Bedingung wegfaellt — ausser ein
   hoeher priorisierter `Exclusive`-Screen verdraengt ihn.
3. Ist ein berechtigter Screen `Exclusive`, wird der mit der hoechsten
   `Priority` allein gezeigt.
4. Sonst bilden die berechtigten Screens mit der hoechsten `Priority` das aktive
   Set und rotieren im globalen Intervall.

Migration: Aeltere Configs mit `Activation`/`ProcessName` werden einmalig in
Bedingungen ueberfuehrt (`process` -> eine `processRunning`-Bedingung mit
`Priority` 10, `default` -> leere Bedingungsliste), sodass sich das Verhalten
nicht aendert. Die Legacy-Felder bleiben fuer die Deserialisierung erhalten.

## Aktuelle Grenzen

- Layout-Screens nutzen firmwareseitiges horizontales Textscrolling fuer ihre
  Textfelder. Der Balken und Icons bleiben statisch.
- Icons sind einfache firmwareseitig gezeichnete Primitives, keine Bitmaps.
- Freie Elementlisten wie `e1=header:...;e2=bar:...` sind noch nicht umgesetzt.
- Zentrierte/rechtsbuendige Ausrichtung greift nur bei kurzen, nicht
  scrollenden Zeilen; zu lange Zeilen scrollen weiterhin linksbuendig.

## Naechste Ausbaustufen

1. `custom`-Layout oder freie Elemente einfuehren, wenn die festen Layouts
   nicht reichen.
2. Kleine Bitmap-Icons firmwareseitig hinterlegen, falls die gezeichneten
   Primitives nicht ausreichen.
3. Defaultscreen optional ebenfalls auf das Layout-System migrieren.
