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

Die App-Oberflaeche hat keinen getrennten `OLED Default`- und
`Display Trigger`-Editor mehr. Beide Bereiche wurden in einem gemeinsamen
`Display`-Tab zusammengefuehrt:

- `Idle Screen` bearbeitet die dauerhafte Default-Anzeige.
- `Display Rules` listet ereignisgesteuerte Screens.
- `Selected Screen Rule` bearbeitet das ausgewaehlte Ereignis.
- Rechts stehen Idle-Preview, Rule-Preview und Runtime-Optionen zusammen.

## Aktuelle Grenzen

- Layout-Screens nutzen firmwareseitiges horizontales Textscrolling fuer ihre
  Textfelder. Der Balken und Icons bleiben statisch.
- Icons sind einfache firmwareseitig gezeichnete Primitives, keine Bitmaps.
- Freie Elementlisten wie `e1=header:...;e2=bar:...` sind noch nicht umgesetzt.
- Defaultscreen-Konfiguration nutzt weiterhin den bestehenden Zeilenmodus.

## Naechste Ausbaustufen

1. `custom`-Layout oder freie Elemente einfuehren, wenn die festen Layouts
   nicht reichen.
2. Kleine Bitmap-Icons firmwareseitig hinterlegen, falls die gezeichneten
   Primitives nicht ausreichen.
3. Defaultscreen optional ebenfalls auf das Layout-System migrieren.
