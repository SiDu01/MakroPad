# Gestaltung der App

Stand: M3 (2026-10-04).

## Idee

Das Fenster ist die Frontplatte des MakroPads. Das Gerät wird flach von oben in
seinen gedruckten Farben gezeichnet; alles drumherum bleibt ruhig. Das eine
auffällige Element ist der **OLED-Spiegel**: dieselbe 5×7-Pixelschrift und
dieselben Layouts wie die Firmware (`Core/Display/OledMirror.cs` ↔
`firmware/src/display.cpp`), gezeichnet als Punktmatrix.

## Farben

Aus `hardware/case/makropad_case.scad`, Tokens in `app/MakroPad.App/Themes/Tokens.axaml`.

| Rolle | Hell | Dunkel |
| --- | --- | --- |
| Deckel | `#8FB3AD` | `#7A9E98` |
| Keycap | `#D9D4C7` | `#D2CDC0` |
| Fader-Kappe, Auswahl | `#E0662B` | `#E86F33` / `#F07A3E` |
| OLED-Glas / Pixel | `#101820` / `#D4E6F4` | `#0B1117` / `#D4E6F4` |
| OLED-Rahmen | `#C9CCD1` | `#B9BCC2` |
| Arbeitsfläche / Panel | `#E4E9E7` / `#F4F6F5` | `#1B2221` / `#222B2A` |

Orange erscheint nur dort, wo es am Gerät ist (Fader) und für die aktuelle
Auswahl. Standard-Steuerelemente (Schalter, Häkchen) nutzen ein dunkles Salbei.

## Schrift

Bahnschrift (DIN 1451, in Windows enthalten) für alles; Größen 12 / 13,5 / 15 /
18 / 26. Die Pixelschrift gibt es nur im OLED-Spiegel. Protokollzeilen in
Cascadia Mono, weil sie echter Protokolltext sind.

## Regeln

- Bewegung nur als Antwort auf echte Eingaben: gedrückte Taste sinkt ein,
  Fader-Kappe folgt dem Slider.
- Keine Großbuchstaben-Labels, keine Kartenraster, keine Icon-Leiste.
- Texte aus Sicht des Nutzers: „Was soll passieren?“, „Beschriftung“,
  „Jetzt auslösen“. Fehler nennen Ort und Abhilfe.
- Änderungen werden sofort gespeichert; die App schreibt nur bei Nutzeraktionen
  in `config.json`.

## Screenshots für die Gestaltungsprüfung

Nur mit `"debugMode": true` in der verwendeten `config.json` (Einstellungen →
Entwicklermodus); ohne ihn ignoriert die App diese Startoptionen.

```powershell
MakroPad.App.exe --simulator --config-dir <Ordner mit Beispiel-config.json> `
  --screenshot pad.png [--page diagnose|einstellungen] [--select button:5|slider:2|display]
```
