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
daraus Header, Text, Statussymbol und bei Bedarf einen Fortschrittsbalken.

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

## Aktuelle Grenzen

- Layout-Screens scrollen noch nicht horizontal. Lange Texte werden im
  Firmware-Renderer gekuerzt.
- Icons sind einfache OLED-Labels (`OK`, `!`, `X`, `~`), keine Bitmaps.
- Freie Elementlisten wie `e1=header:...;e2=bar:...` sind noch nicht umgesetzt.
- Defaultscreen-Konfiguration nutzt weiterhin den bestehenden Zeilenmodus.

## Naechste Ausbaustufen

1. Layout-Preview in der App visuell an den Firmware-Renderer angleichen.
2. `custom`-Layout oder freie Elemente einfuehren, wenn die festen Layouts
   nicht reichen.
3. Kleine Bitmap-Icons firmwareseitig hinterlegen.
4. Layout-Screens mit kontrolliertem Textscrolling erweitern.
